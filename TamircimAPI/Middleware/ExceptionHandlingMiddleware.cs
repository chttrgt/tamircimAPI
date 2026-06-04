using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TamircimAPI.Exceptions;

namespace TamircimAPI.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var response = context.Response;
            response.ContentType = "application/json; charset=utf-8";

            var errorResponse = new ErrorResponse();

            switch (exception)
            {
                case BusinessRuleException businessEx:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = businessEx.Message;
                    errorResponse.Code = businessEx.RuleCode;
                    _logger.LogWarning("Business rule: {Code} - {Message}. Path: {Path}",
                        businessEx.RuleCode, businessEx.Message, context.Request.Path);
                    break;

                case ArgumentException argEx:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = argEx.Message;
                    errorResponse.Code = "INVALID_ARGUMENT";
                    _logger.LogWarning("Invalid argument: {Message}. Path: {Path}",
                        argEx.Message, context.Request.Path);
                    break;

                case UnauthorizedAccessException:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    errorResponse.Message = "Bu işlem için yetkiniz bulunmamaktadır.";
                    errorResponse.Code = "UNAUTHORIZED";
                    _logger.LogWarning("Unauthorized. Path: {Path}", context.Request.Path);
                    break;

                case KeyNotFoundException:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    errorResponse.Message = "İstenen kaynak bulunamadı.";
                    errorResponse.Code = "NOT_FOUND";
                    _logger.LogWarning("Not found. Path: {Path}", context.Request.Path);
                    break;

                // Optimistic concurrency: kayıt biz işlerken başkası değiştirdi/sildi.
                case DbUpdateConcurrencyException:
                    response.StatusCode = (int)HttpStatusCode.Conflict;
                    errorResponse.Message = "Bu kayıt başka bir işlem tarafından değiştirildi. Lütfen yenileyip tekrar deneyin.";
                    errorResponse.Code = "CONCURRENCY_CONFLICT";
                    _logger.LogWarning("Concurrency conflict. Path: {Path}", context.Request.Path);
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.Message = "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                    errorResponse.Code = "INTERNAL_ERROR";
                    _logger.LogError(exception, "Unhandled exception. Path: {Path}", context.Request.Path);
                    break;
            }

            var result = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await response.WriteAsync(result);
        }
    }

    public class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public object? Details { get; set; }
    }

    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder) =>
            builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
