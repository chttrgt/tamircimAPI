using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TamircimAPI.Authorization;
using TamircimAPI.Data;
using TamircimAPI.Middleware;
using TamircimAPI.Services.Auth;
using TamircimAPI.Services.Common;
using TamircimAPI.Services.Dashboard;
using TamircimAPI.Services.Customer;
using TamircimAPI.Services.Device;
using TamircimAPI.Services.Repair;
using TamircimAPI.Services.Token;
using TamircimAPI.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        new RenderedCompactJsonFormatter(),
        path: "logs/log-.clef",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

Env.Load();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? builder.Configuration["Jwt:Key"];

if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("JWT_SECRET_KEY en az 32 karakter olmalıdır.");

var envConnStr = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
var connectionString = (!string.IsNullOrWhiteSpace(envConnStr) ? envConnStr : null)
    ?? BuildConnectionString()
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Veritabanı bağlantı dizesi yapılandırılmamış.");

static string? BuildConnectionString()
{
    var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "postgres";
    var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
    var db   = Environment.GetEnvironmentVariable("POSTGRES_DB");
    var user = Environment.GetEnvironmentVariable("POSTGRES_USER");
    var pass = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

    if (string.IsNullOrEmpty(db) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        return null;

    // NpgsqlConnectionStringBuilder şifredeki özel karakterleri (; , @ vb.) doğru escape eder
    var builder = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = host,
        Port = int.Parse(port),
        Database = db,
        Username = user,
        Password = pass,
        ClientEncoding = "UTF8"
    };
    return builder.ConnectionString;
}

var allowedHosts = Environment.GetEnvironmentVariable("ALLOWED_HOSTS")
    ?? builder.Configuration["AllowedHosts"]
    ?? "*";
builder.Services.Configure<HostFilteringOptions>(options =>
{
    options.AllowedHosts = allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
});

var corsOriginsEnv = Environment.GetEnvironmentVariable("CORS_ORIGINS");
if (string.IsNullOrEmpty(corsOriginsEnv))
    throw new InvalidOperationException("CORS_ORIGINS environment variable zorunludur.");
var corsOrigins = corsOriginsEnv.Split(';', StringSplitOptions.RemoveEmptyEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        }));

// Servis Kayıtları
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ICodeGenerator, CodeGenerator>();
builder.Services.AddScoped<ICustomerQueryService, CustomerQueryService>();
builder.Services.AddScoped<ICustomerCommandService, CustomerCommandService>();
builder.Services.AddScoped<IDeviceQueryService, DeviceQueryService>();
builder.Services.AddScoped<IDeviceCommandService, DeviceCommandService>();
builder.Services.AddScoped<IRepairQueryService, RepairQueryService>();
builder.Services.AddScoped<IRepairCommandService, RepairCommandService>();

// Cihaz fotoğrafları (depolama + servis + 30 gün GC görevi)
builder.Services.AddSingleton<TamircimAPI.Services.Storage.IPhotoStorage, TamircimAPI.Services.Storage.LocalPhotoStorage>();
builder.Services.AddScoped<IDevicePhotoService, DevicePhotoService>();
builder.Services.AddHostedService<TamircimAPI.Services.Storage.PhotoCleanupService>();

// Authorization
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Profil güncellemede şifre değişim denemelerini sınırla
    options.AddPolicy("profile", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerDTOValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new DateTimeUtcConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableDateTimeUtcConverter());
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Tamircim API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT token. Örnek: \"Bearer {token}\""
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Veritabanı şemasını migration'larla güncel tutar: boş DB → tüm şema (tablolar,
// turkish_lower fonksiyonu, sequence'ler) sıfırdan kurulur; dolu DB → yalnızca
// bekleyen migration'lar uygulanır. Her container başlangıcında çalışır →
// sunucu güncellemelerinde elle SQL/ALTER gerekmez.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    Log.Information("Veritabanı migration'ları uygulandı.");
}

app.UseExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<UserEnrichmentMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => "Tamircim API v1.0.0");

app.Run();

// UTC DateTime Converter'ları
public class DateTimeUtcConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        if (string.IsNullOrEmpty(str)) return default;
        var parsed = DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (parsed.Kind == DateTimeKind.Local) return parsed.ToUniversalTime();
        if (parsed.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        return parsed;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime()
            : value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value;
        writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}

public class NullableDateTimeUtcConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        if (string.IsNullOrEmpty(str)) return null;
        var parsed = DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (parsed.Kind == DateTimeKind.Local) return parsed.ToUniversalTime();
        if (parsed.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        return parsed;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (!value.HasValue) { writer.WriteNullValue(); return; }
        var v = value.Value;
        var utc = v.Kind == DateTimeKind.Local ? v.ToUniversalTime()
            : v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc)
            : v;
        writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}
