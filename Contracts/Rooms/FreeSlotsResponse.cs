namespace CampusFlow.Contracts.Rooms;

/// <summary>Свободный интервал внутри рабочего дня.</summary>
/// <param name="StartTime">Начало, <c>HH:MM</c>.</param>
/// <param name="EndTime">Окончание, <c>HH:MM</c>.</param>
public sealed record FreeSlotDto(string StartTime, string EndTime);

/// <summary>Ответ со свободными слотами аудитории на дату.</summary>
/// <param name="RoomId">Идентификатор аудитории.</param>
/// <param name="Date">Дата в формате <c>YYYY-MM-DD</c>.</param>
/// <param name="FreeSlots">Максимальные непрерывные свободные интервалы в порядке возрастания.</param>
public sealed record FreeSlotsResponse(int RoomId, string Date, IReadOnlyList<FreeSlotDto> FreeSlots);
