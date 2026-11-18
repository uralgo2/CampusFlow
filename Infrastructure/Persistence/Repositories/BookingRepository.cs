using CampusFlow.Application.Abstractions;
using CampusFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace CampusFlow.Infrastructure.Persistence.Repositories;

/// <inheritdoc />
public sealed class BookingRepository : IBookingRepository
{
    private readonly CampusFlowDbContext _db;

    public BookingRepository(CampusFlowDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Booking>> ListForRoomAndDateAsync(
        int roomId,
        DateOnly date,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .Where(booking => booking.RoomId == roomId && booking.Date == date);

        if (activeOnly) query = query.Where(booking => booking.Status == BookingStatus.Active);

        return await query.OrderBy(booking => booking.StartTime).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Booking>> ListForUserAsync(
        int userId,
        BookingStatus? status,
        CancellationToken cancellationToken)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .Include(booking => booking.Room)
            .Include(booking => booking.User)
            .Where(booking => booking.UserId == userId);

        if (status is not null)
        {
            var statusValue = status.Value;
            query = query.Where(booking => booking.Status == statusValue);
        }

        return await query
            .OrderBy(booking => booking.Date)
            .ThenBy(booking => booking.StartTime)
            .ThenBy(booking => booking.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Booking>> ListAsync(BookingQuery query, CancellationToken cancellationToken)
    {
        var bookings = _db.Bookings
            .AsNoTracking()
            .Include(booking => booking.Room)
            .Include(booking => booking.User)
            .AsQueryable();

        if (query.Date is not null)
        {
            var date = query.Date.Value;
            bookings = bookings.Where(booking => booking.Date == date);
        }

        if (query.RoomId is not null)
        {
            var roomId = query.RoomId.Value;
            bookings = bookings.Where(booking => booking.RoomId == roomId);
        }

        if (query.UserId is not null)
        {
            var userId = query.UserId.Value;
            bookings = bookings.Where(booking => booking.UserId == userId);
        }

        if (query.Status is not null)
        {
            var status = query.Status.Value;
            bookings = bookings.Where(booking => booking.Status == status);
        }

        return await bookings
            .OrderBy(booking => booking.Date)
            .ThenBy(booking => booking.StartTime)
            .ThenBy(booking => booking.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Booking>> ListForPeriodAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<int>? roomIds,
        CancellationToken cancellationToken)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .Include(booking => booking.Room)
            .Where(booking => booking.Date >= from && booking.Date <= to);

        if (roomIds is not null)
        {
            var ids = roomIds.ToList();
            query = query.Where(booking => ids.Contains(booking.RoomId));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Booking?> GetAsync(int id, CancellationToken cancellationToken)
    {
        return _db.Bookings
            .Include(booking => booking.Room)
            .Include(booking => booking.User)
            .FirstOrDefaultAsync(booking => booking.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasConflictAsync(
        int roomId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        int? excludeBookingId,
        CancellationToken cancellationToken)
    {
        var query = _db.Bookings
            .Where(booking => booking.RoomId == roomId
                              && booking.Date == date
                              && booking.Status == BookingStatus.Active
                              && booking.StartTime < endTime
                              && startTime < booking.EndTime);

        if (excludeBookingId is not null)
        {
            var excluded = excludeBookingId.Value;
            query = query.Where(booking => booking.Id != excluded);
        }

        return await query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountActiveForRoomAsync(int roomId, CancellationToken cancellationToken)
    {
        return _db.Bookings.CountAsync(
            booking => booking.RoomId == roomId && booking.Status == BookingStatus.Active,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Booking> AddAsync(Booking booking, CancellationToken cancellationToken)
    {
        await _db.Bookings.AddAsync(booking, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return booking;
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}