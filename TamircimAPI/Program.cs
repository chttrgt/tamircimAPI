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
using TamircimAPI.Services.Staff;
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

var sentryDsn = Environment.GetEnvironmentVariable("SENTRY_DSN");
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(sentryDsn);
}

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

// Tenant izolasyonu altyapısı (scoped): istek başına tenant bağlamı + RLS oturum
// değişkenini ayarlayan connection interceptor.
builder.Services.AddScoped<TamircimAPI.Services.Tenant.ITenantContext, TamircimAPI.Services.Tenant.TenantContext>();
builder.Services.AddScoped<TamircimAPI.Data.Interceptors.TenantConnectionInterceptor>();

// DbContext: scoped interceptor'ı (sp, options) overload'u ile ekliyoruz → interceptor
// istek-scoped ITenantContext'i görür.
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        })
    .AddInterceptors(sp.GetRequiredService<TamircimAPI.Data.Interceptors.TenantConnectionInterceptor>()));

// Servis Kayıtları
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<TamircimAPI.Services.Email.IEmailSender, TamircimAPI.Services.Email.SmtpEmailSender>();
// Bot koruması: Cloudflare Turnstile doğrulaması (siteverify HTTP çağrısı için HttpClient).
builder.Services.AddHttpClient();
builder.Services.AddScoped<TamircimAPI.Services.Captcha.ICaptchaVerifier, TamircimAPI.Services.Captcha.TurnstileCaptchaVerifier>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStaffService, StaffService>();
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

// X-Forwarded-* başlıklarına yalnızca GÜVENİLEN reverse proxy'den gelince güven.
// Güvenilen ağ(lar) FORWARDED_KNOWN_NETWORKS (CIDR; ';' ayraçlı) ve/veya tekil
// proxy IP'leri FORWARDED_KNOWN_PROXIES env'den okunur. Tanımsızsa varsayılan
// (yalnızca loopback) korunur → X-Forwarded-For spoof edilip IP-bazlı rate limit
// aşılamaz. Docker'da proxy farklı IP'de olduğundan üretimde bu env ayarlanmalı,
// aksi halde istemci IP'si proxy IP'sine düşer (rate limit/log proxy IP'sine kayar).
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 2,
};
var fwdNetworks = Environment.GetEnvironmentVariable("FORWARDED_KNOWN_NETWORKS");
if (!string.IsNullOrWhiteSpace(fwdNetworks))
{
    foreach (var cidr in fwdNetworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var parts = cidr.Split('/');
        if (parts.Length == 2
            && System.Net.IPAddress.TryParse(parts[0], out var prefix)
            && int.TryParse(parts[1], out var prefixLen))
        {
            forwardedOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLen));
        }
    }
}
var fwdProxies = Environment.GetEnvironmentVariable("FORWARDED_KNOWN_PROXIES");
if (!string.IsNullOrWhiteSpace(fwdProxies))
{
    foreach (var ipStr in fwdProxies.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        if (System.Net.IPAddress.TryParse(ipStr, out var addr))
            forwardedOptions.KnownProxies.Add(addr);
}
app.UseForwardedHeaders(forwardedOptions);

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<UserEnrichmentMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => "Tamircim API v1.0.0");
app.MapGet("/health", async (ApplicationDbContext db) =>
{
    SentrySdk.CaptureMessage("Sentry test - tamircim-backend");
    try
    {
        await db.Database.CanConnectAsync();
        return Results.Ok(new { status = "ok", db = "ok", ts = DateTime.UtcNow });
    }
    catch
    {
        return Results.Json(new { status = "degraded", db = "error", ts = DateTime.UtcNow }, statusCode: 503);
    }
}).AllowAnonymous();

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
