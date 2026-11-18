using System.Text.Json;
using CampusFlow.Api.Serialization;
using CampusFlow.Contracts.Common;

namespace CampusFlow.Api.Http;

/// <summary>Запись ответа об ошибке в едином формате <c>{code, message, details}</c>.</summary>
public static class HttpErrorWriter
{
    /// <summary>Сериализует ошибку и отправляет её клиенту.</summary>
    public static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        ErrorResponse error,
        CancellationToken cancellationToken = default)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = JsonSerializer.Serialize(error, JsonDefaults.Options);
        await context.Response.WriteAsync(payload, cancellationToken);
    }
}