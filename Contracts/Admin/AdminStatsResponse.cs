namespace CampusFlow.Contracts.Admin;

/// <summary>Показатель загрузки за отрезок времени.</summary>
/// <param name="ActiveBookings">Количество действующих броней.</param>
/// <param name="CancelledBookings">Количество отменённых броней.</param>
/// <param name="BookedMinutes">Суммарно занятых минут действующими бронями.</param>
/// <param name="TotalMinutes">Доступный фонд минут (аудитории × 540 минут в день).</param>
/// <param name="UtilizationPercent">Загрузка в процентах, округлённая до одного знака.</param>
public sealed record LoadBucketDto(
    int ActiveBookings,
    int CancelledBookings,
    int BookedMinutes,
    int TotalMinutes,
    double UtilizationPercent);

/// <summary>Загрузка по конкретному дню.</summary>
public sealed record DailyLoadDto(string Date, LoadBucketDto Load);

/// <summary>Загрузка по корпусу.</summary>
public sealed record BuildingLoadDto(string Building, int Rooms, LoadBucketDto Load);

/// <summary>Топ аудиторий по числу занятых минут.</summary>
public sealed record RoomLoadDto(int RoomId, string RoomName, string Building, int ActiveBookings, int BookedMinutes);

/// <summary>Сводка загрузки для администратора.</summary>
/// <param name="From">Начало периода, <c>YYYY-MM-DD</c>.</param>
/// <param name="To">Конец периода включительно, <c>YYYY-MM-DD</c>.</param>
/// <param name="Totals">Итоги по периоду.</param>
/// <param name="ByDay">Разбивка по дням.</param>
/// <param name="ByBuilding">Разбивка по корпусам.</param>
/// <param name="TopRooms">Самые загруженные аудитории (до 10).</param>
public sealed record AdminStatsResponse(
    string From,
    string To,
    LoadBucketDto Totals,
    IReadOnlyList<DailyLoadDto> ByDay,
    IReadOnlyList<BuildingLoadDto> ByBuilding,
    IReadOnlyList<RoomLoadDto> TopRooms);
