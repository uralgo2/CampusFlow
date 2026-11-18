using CampusFlow.Application.Abstractions;
using CampusFlow.Domain;

namespace CampusFlow.Tests.Testing.Fakes;

/// <summary>Репозиторий броней в памяти. Как EF с <c>Include</c>, подставляет навигации Room/User.</summary>
public sealed class InMemoryBookingRepository : IBookingRepository
{
    private readonly FakeDatabase _db;

    public InMemoryBookingRepository(FakeDatabase db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<Booking>> ListForRoomAndDateAsync(
        int roomId,
        DateOnly date,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var bookings = _db.Bookings
            .Where(booking => booking.RoomId == roomId && booking.Date == date)
            .Where(booking => !activeOnly || booking.Status == BookingStatus.Active)
            .OrderBy(booking => booking.StartTime)
            .Select(Attach)
            .ToList();

        return Task.FromResult<IReadOnlyList<Booking>>(bookings);
    }

    public Task<IReadOnlyList<Booking>> ListForUserAsync(
        int userId,
        BookingStatus? status,
        CancellationToken cancellationToken)
    {
        var bookings = _db.Bookings
            .Where(booking => booking.UserId == userId)
            .Where(booking => status is null || booking.Status == status.Value)
            .OrderBy(booking => booking.Date)
            .ThenBy(booking => booking.StartTime)
            .ThenBy(booking => booking.Id)
            .Select(Attach)
            .ToList();

        return Task.FromResult<IReadOnlyList<Booking>>(bookings);
    }

    public Task<IReadOnlyList<Booking>> ListAsync(BookingQuery query, CancellationToken cancellationToken)
    {
        var bookings = _db.Bookings
            .Where(booking => query.Date is null || booking.Date == query.Date.Value)
            .Where(booking => query.RoomId is null || booking.RoomId == query.RoomId.Value)
            .Where(booking => query.UserId is null || booking.UserId == query.UserId.Value)
            .Where(booking => query.Status is null || booking.Status == query.Status.Value)
            .OrderBy(booking => booking.Date)
            .ThenBy(booking => booking.StartTime)
            .ThenBy(booking => booking.Id)
            .Select(Attach)
            .ToList();

        return Task.FromResult<IReadOnlyList<Booking>>(bookings);
    }

    public Task<IReadOnlyList<Booking>> ListForPeriodAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<int>? roomIds,
        CancellationToken cancellationToken)
    {
        var bookings = _db.Bookings
            .Where(booking => booking.Date >= from && booking.Date <= to)
            .Where(booking => roomIds is null || roomIds.Contains(booking.RoomId))
            .Select(Attach)
            .ToList();

        return Task.FromResult<IReadOnlyList<Booking>>(bookings);
    }

    public Task<Booking?> GetAsync(int id, CancellationToken cancellationToken)
    {
        var booking = _db.Bookings.FirstOrDefault(candidate => candidate.Id == id);
        return Task.FromResult(booking is null ? null : Attach(booking));
    }

    public Task<bool> HasConflictAsync(
        int roomId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        int? excludeBookingId,
        CancellationToken cancellationToken)
    {
        var hasConflict = _db.Bookings.Any(booking =>
            booking.RoomId == roomId
            && booking.Date == date
            && booking.Status == BookingStatus.Active
            && booking.Id != (excludeBookingId ?? 0)
            && booking.StartTime < endTime
            && startTime < booking.EndTime);

        return Task.FromResult(hasConflict);
    }

    public Task<int> CountActiveForRoomAsync(int roomId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_db.Bookings.Count(booking =>
            booking.RoomId == roomId && booking.Status == BookingStatus.Active));
    }

    public Task<Booking> AddAsync(Booking booking, CancellationToken cancellationToken)
    {
        booking.Id = _db.Bookings.Count == 0 ? 1 : _db.Bookings.Max(candidate => candidate.Id) + 1;
        _db.Bookings.Add(booking);
        return Task.FromResult(Attach(booking));
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private Booking Attach(Booking booking)
    {
        booking.Room ??= _db.Rooms.FirstOrDefault(room => room.Id == booking.RoomId);
        booking.User ??= _db.Users.FirstOrDefault(user => user.Id == booking.UserId);
        return booking;
    }
}