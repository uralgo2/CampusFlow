using CampusFlow.Application.Abstractions;
using CampusFlow.Domain;

namespace CampusFlow.Tests.Testing.Fakes;

/// <summary>Репозиторий аудиторий в памяти: повторяет поведение EF-реализации для тестов сервисов.</summary>
public sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly FakeDatabase _db;

    public InMemoryRoomRepository(FakeDatabase db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<Room>> ListAsync(RoomQuery query, CancellationToken cancellationToken)
    {
        IEnumerable<Room> rooms = _db.Rooms;

        if (query.Building is not null) rooms = rooms.Where(room => room.Building == query.Building);

        if (query.MinCapacity is not null) rooms = rooms.Where(room => room.Capacity >= query.MinCapacity.Value);

        if (query.Equipment.Count > 0) rooms = rooms.Where(room => query.Equipment.All(room.Equipment.Contains));

        IReadOnlyList<Room> result = rooms
            .OrderBy(room => room.Building)
            .ThenBy(room => room.Floor)
            .ThenBy(room => room.Name)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<Room?> GetAsync(int id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_db.Rooms.FirstOrDefault(room => room.Id == id));
    }

    public Task<bool> NameExistsAsync(string name, int? excludeRoomId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim();
        var exists = _db.Rooms.Any(room =>
            room.Id != (excludeRoomId ?? 0)
            && string.Equals(room.Name, normalized, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }

    public Task AddAsync(Room room, CancellationToken cancellationToken)
    {
        room.Id = _db.Rooms.Count == 0 ? 1 : _db.Rooms.Max(candidate => candidate.Id) + 1;
        _db.Rooms.Add(room);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Room room, CancellationToken cancellationToken)
    {
        _db.Rooms.Remove(room);
        _db.Bookings.RemoveAll(booking => booking.RoomId == room.Id);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}