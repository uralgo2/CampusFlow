using CampusFlow.Application.Abstractions;
using CampusFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace CampusFlow.Infrastructure.Persistence.Repositories;

/// <inheritdoc />
public sealed class RoomRepository : IRoomRepository
{
    private readonly CampusFlowDbContext _db;

    public RoomRepository(CampusFlowDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Room>> ListAsync(RoomQuery query, CancellationToken cancellationToken)
    {
        var rooms = _db.Rooms.AsNoTracking().AsQueryable();

        if (query.Building is not null)
        {
            rooms = rooms.Where(room => room.Building == query.Building);
        }

        if (query.MinCapacity is not null)
        {
            var minCapacity = query.MinCapacity.Value;
            rooms = rooms.Where(room => room.Capacity >= minCapacity);
        }

        var list = await rooms
            .OrderBy(room => room.Building)
            .ThenBy(room => room.Floor)
            .ThenBy(room => room.Name)
            .ToListAsync(cancellationToken);

        if (query.Equipment.Count == 0)
        {
            return list;
        }

        // Оборудование хранится строкой, поэтому пересечение множеств проверяется в памяти.
        return list.Where(room => query.Equipment.All(room.Equipment.Contains)).ToList();
    }

    /// <inheritdoc />
    public Task<Room?> GetAsync(int id, CancellationToken cancellationToken) =>
        _db.Rooms.FirstOrDefaultAsync(room => room.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(string name, int? excludeRoomId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var excluded = excludeRoomId ?? 0;
        return _db.Rooms.AnyAsync(
            room => room.Id != excluded && room.Name.ToLower() == normalized,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(Room room, CancellationToken cancellationToken) =>
        await _db.Rooms.AddAsync(room, cancellationToken);

    /// <inheritdoc />
    public async Task DeleteAsync(Room room, CancellationToken cancellationToken)
    {
        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}
