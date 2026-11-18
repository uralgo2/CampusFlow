namespace CampusFlow.Contracts.Bookings;

/// <summary>
///     Создание брони. Автор берётся из токена, поэтому <c>userId</c> в теле не принимается.
/// </summary>
/// <param name="RoomId">Идентификатор аудитории.</param>
/// <param name="Date">Дата, <c>YYYY-MM-DD</c>, не в прошлом.</param>
/// <param name="StartTime">Начало, <c>HH:MM</c>, кратно 30 минутам, в диапазоне 09:00–18:00.</param>
/// <param name="EndTime">Окончание, <c>HH:MM</c>, больше начала, кратно 30 минутам, не позже 18:00.</param>
/// <param name="Comment">Необязательный комментарий до 500 символов.</param>
public sealed record CreateBookingRequest(
    int? RoomId,
    string? Date,
    string? StartTime,
    string? EndTime,
    string? Comment);