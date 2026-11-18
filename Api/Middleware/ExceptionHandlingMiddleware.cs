using CampusFlow.Api.Http;
using CampusFlow.Application.Errors;
using CampusFlow.Contracts.Common;

namespace CampusFlow.Api.Middleware;

/// <summary>Превращает исключения приложения в единый JSON-ответ об ошибке.</summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly RequestDelegate _next;

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
        catch (ApiException exception)
        {
            // Ответ отправляется до записи в журнал: сбой логирования не должен ломать ответ клиенту.
            await HttpErrorWriter.WriteAsync(
                context,
                exception.StatusCode,
                new ErrorResponse(exception.Code, exception.Message, exception.Details));

            LogSafely(() => _logger.LogWarning(
                "Ошибка API {Code} ({Status}) на {Method} {Path}: {Message}",
                exception.Code,
                exception.StatusCode,
                context.Request.Method,
                context.Request.Path,
                exception.Message));
        }
        catch (BadHttpRequestException exception)
        {
            await HttpErrorWriter.WriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                new ErrorResponse(
                    ErrorCodes.Validation,
                    "Некорректные данные запроса",
                    new { message = exception.Message }));

            LogSafely(() => _logger.LogWarning(
                "Некорректный HTTP-запрос на {Method} {Path}: {Message}",
                context.Request.Method,
                context.Request.Path,
                exception.Message));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Клиент закрыл соединение — отвечать некому.
        }
        catch (Exception exception)
        {
            LogSafely(() => _logger.LogError(
                exception,
                "Необработанная ошибка на {Method} {Path}",
                context.Request.Method,
                context.Request.Path));

            if (context.Response.HasStarted) throw;

            await HttpErrorWriter.WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                new ErrorResponse(ErrorCodes.Internal, "Внутренняя ошибка сервера"));
        }
    }

    /// <summary>
    ///     Выполняет запись в журнал, не позволяя ошибке логирования выбросить исключение
    ///     и прервать уже формируемый HTTP-ответ.
    /// </summary>
    private static void LogSafely(Action logAction)
    {
        try
        {
            logAction();
        }
        catch
        {
            // Намеренно проглатываем: недоступный журнал не должен ломать API.
        }
    }
}