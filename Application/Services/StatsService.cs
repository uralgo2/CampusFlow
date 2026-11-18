using CampusFlow.Application.Abstractions;
using CampusFlow.Application.Errors;
using CampusFlow.Application.Parsing;
using CampusFlow.Contracts.Admin;
using CampusFlow.Domain;

namespace CampusFlow.Application.Services;

/// <inheritdoc />
public sealed class StatsService : IStatsService
{
    private const int MaxPeriodDays = 366;

    private readonly IBookingRepository _bookings;
    private readonly IClock _clock;
    private readonly IRoomRepository _rooms;

    public StatsService(IBookingRepository bookings, IRoomRepository rooms, IClock clock)
    {
        _bookings = bookings;
        _rooms = rooms;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<AdminStatsResponse> GetStatsAsync(
        string? from,
        string? to,
        string? building,
        CancellationToken cancellationToken)
    {
        var today = _clock.Today;
        var errors = new Dictionary<string, string>();

        var fromDate = today;
        var toDate = today.AddDays(6);

        if (!string.IsNullOrWhiteSpace(from) && !ApiParsing.TryParseDate(from, out fromDate))
            errors["from"] = "Неверный формат даты, ожидается YYYY-MM-DD";

        if (!string.IsNullOrWhiteSpace(to) && !ApiParsing.TryParseDate(to, out toDate))
            errors["to"] = "Неверный формат даты, ожидается YYYY-MM-DD";

        string? normalizedBuilding = null;
        if (!string.IsNullOrWhiteSpace(building) && !Buildings.TryNormalize(building, out normalizedBuilding))
            errors["building"] = "Допустимые значения: A, B, C, D";

        if (errors.Count > 0) throw ApiException.Validation(errors);

        if (toDate < fromDate) throw ApiException.Validation("to", "Дата окончания не может быть раньше даты начала");

        if (toDate.DayNumber - fromDate.DayNumber > MaxPeriodDays)
            throw ApiException.Validation("to", $"Период не должен превышать {MaxPeriodDays} дней");

        var rooms = await _rooms.ListAsync(
            new RoomQuery(normalizedBuilding, null, Array.Empty<Equipment>()),
            cancellationToken);

        var roomIds = rooms.Select(room => room.Id).ToHashSet();
        var dayCount = toDate.DayNumber - fromDate.DayNumber + 1;

        var allBookings = await _bookings.ListForPeriodAsync(fromDate, toDate, roomIds, cancellationToken);
        var relevant = allBookings
            .Where(booking => booking.Date >= fromDate && booking.Date <= toDate && roomIds.Contains(booking.RoomId))
            .ToList();

        var totals = BuildBucket(relevant, rooms.Count, dayCount);

        var byDay = new List<DailyLoadDto>();
        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            var dayBookings = relevant.Where(booking => booking.Date == day).ToList();
            byDay.Add(new DailyLoadDto(ApiParsing.FormatDate(day), BuildBucket(dayBookings, rooms.Count, 1)));
        }

        var byBuilding = rooms
            .GroupBy(room => room.Building)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var buildingRoomIds = group.Select(room => room.Id).ToHashSet();
                var buildingBookings = relevant.Where(booking => buildingRoomIds.Contains(booking.RoomId)).ToList();
                return new BuildingLoadDto(
                    group.Key,
                    group.Count(),
                    BuildBucket(buildingBookings, group.Count(), dayCount));
            })
            .ToList();

        var topRooms = relevant
            .Where(booking => booking.Status == BookingStatus.Active)
            .GroupBy(booking => booking.RoomId)
            .Select(group =>
            {
                var room = rooms.FirstOrDefault(candidate => candidate.Id == group.Key);
                return new RoomLoadDto(
                    group.Key,
                    room?.Name ?? string.Empty,
                    room?.Building ?? string.Empty,
                    group.Count(),
                    group.Sum(BookedMinutes));
            })
            .OrderByDescending(room => room.BookedMinutes)
            .ThenBy(room => room.RoomName, StringComparer.Ordinal)
            .Take(10)
            .ToList();

        return new AdminStatsResponse(
            ApiParsing.FormatDate(fromDate),
            ApiParsing.FormatDate(toDate),
            totals,
            byDay,
            byBuilding,
            topRooms);
    }

    private static LoadBucketDto BuildBucket(IEnumerable<Booking> bookings, int roomCount, int dayCount)
    {
        var items = bookings as IReadOnlyCollection<Booking> ?? bookings.ToList();
        var active = items.Where(booking => booking.Status == BookingStatus.Active).ToList();
        var cancelled = items.Count - active.Count;
        var bookedMinutes = active.Sum(BookedMinutes);
        var totalMinutes = roomCount * dayCount * SchedulePolicy.WorkingDayMinutes;
        var utilization = totalMinutes == 0
            ? 0
            : Math.Round(bookedMinutes * 100d / totalMinutes, 1, MidpointRounding.AwayFromZero);

        return new LoadBucketDto(active.Count, cancelled, bookedMinutes, totalMinutes, utilization);
    }

    private static int BookedMinutes(Booking booking)
    {
        return (int)(booking.EndTime - booking.StartTime).TotalMinutes;
    }
}