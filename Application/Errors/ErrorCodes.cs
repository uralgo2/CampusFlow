namespace CampusFlow.Application.Errors;

/// <summary>Машинные коды ошибок API (см. README, раздел «Ошибки»).</summary>
public static class ErrorCodes
{
    public const string Validation = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string RoomNotFound = "ROOM_NOT_FOUND";
    public const string BookingNotFound = "BOOKING_NOT_FOUND";
    public const string BookingConflict = "BOOKING_CONFLICT";
    public const string AlreadyCancelled = "ALREADY_CANCELLED";
    public const string RoomInUse = "ROOM_IN_USE";
    public const string NotFound = "NOT_FOUND";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string Internal = "INTERNAL_ERROR";
}
