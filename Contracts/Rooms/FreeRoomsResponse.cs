namespace CampusFlow.Contracts.Rooms;

/// <summary>Ответ поиска: аудитории, свободные на заданный интервал.</summary>
/// <param name="Date">Дата в формате <c>YYYY-MM-DD</c>.</param>
/// <param name="StartTime">Начало интервала, <c>HH:MM</c>.</param>
/// <param name="EndTime">Окончание интервала, <c>HH:MM</c>.</param>
/// <param name="Rooms">Подходящие аудитории, отсортированные по корпусу, этажу и названию.</param>
public sealed record FreeRoomsResponse(
    string Date,
    string StartTime,
    string EndTime,
    IReadOnlyList<RoomDto> Rooms);
