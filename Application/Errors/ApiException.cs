namespace CampusFlow.Application.Errors;

/// <summary>
/// Ошибка уровня приложения, которая превращается в JSON <c>{code, message, details}</c>
/// с заданным HTTP-статусом. Обрабатывается <c>ExceptionHandlingMiddleware</c>.
/// </summary>
public sealed class ApiException : Exception
{
    private ApiException(int statusCode, string code, string message, object? details, Exception? innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }

    /// <summary>HTTP-статус ответа.</summary>
    public int StatusCode { get; }

    /// <summary>Машинный код ошибки.</summary>
    public string Code { get; }

    /// <summary>Дополнительные сведения об ошибке.</summary>
    public object? Details { get; }

    /// <summary>400 VALIDATION_ERROR с разбивкой по полям.</summary>
    public static ApiException Validation(
        IReadOnlyDictionary<string, string> details,
        string message = "Некорректные данные запроса") =>
        new(
            StatusCodes.Status400BadRequest,
            ErrorCodes.Validation,
            // Для ошибки в одном поле отдаём его текст в message: клиенту так понятнее.
            details.Count == 1 ? details.Values.First() : message,
            details,
            null);

    /// <summary>400 VALIDATION_ERROR для одного поля.</summary>
    public static ApiException Validation(string field, string message) =>
        Validation(new Dictionary<string, string> { [field] = message });

    /// <summary>401 UNAUTHORIZED.</summary>
    public static ApiException Unauthorized(string message = "Требуется авторизация") =>
        new(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, message, null, null);

    /// <summary>403 FORBIDDEN.</summary>
    public static ApiException Forbidden(string message = "Недостаточно прав для выполнения операции") =>
        new(StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, message, null, null);

    /// <summary>404 ROOM_NOT_FOUND.</summary>
    public static ApiException RoomNotFound(int roomId) =>
        new(
            StatusCodes.Status404NotFound,
            ErrorCodes.RoomNotFound,
            "Аудитория не найдена",
            new { roomId },
            null);

    /// <summary>404 BOOKING_NOT_FOUND.</summary>
    public static ApiException BookingNotFound(int bookingId) =>
        new(
            StatusCodes.Status404NotFound,
            ErrorCodes.BookingNotFound,
            "Бронь не найдена",
            new { bookingId },
            null);

    /// <summary>409 BOOKING_CONFLICT: интервал уже занят действующей бронью.</summary>
    public static ApiException BookingConflict(int roomId, string date, string startTime, string endTime) =>
        new(
            StatusCodes.Status409Conflict,
            ErrorCodes.BookingConflict,
            "Аудитория уже забронирована на это время",
            new { roomId, date, startTime, endTime },
            null);

    /// <summary>409 ALREADY_CANCELLED: бронь уже отменена.</summary>
    public static ApiException AlreadyCancelled(int bookingId) =>
        new(
            StatusCodes.Status409Conflict,
            ErrorCodes.AlreadyCancelled,
            "Бронь уже отменена",
            new { bookingId, status = Domain.BookingStatuses.Cancelled },
            null);

    /// <summary>409 ROOM_IN_USE: нельзя удалить аудиторию с действующими бронями.</summary>
    public static ApiException RoomInUse(int roomId, int activeBookings) =>
        new(
            StatusCodes.Status409Conflict,
            ErrorCodes.RoomInUse,
            "Нельзя удалить аудиторию с действующими бронями: сначала отмените их",
            new { roomId, activeBookings },
            null);
}
