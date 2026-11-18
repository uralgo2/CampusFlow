using CampusFlow.Domain;

namespace CampusFlow.Application.Abstractions;

/// <summary>Хранилище справочника аудиторий.</summary>
public interface IRoomRepository
{
    /// <summary>Аудитории с учётом фильтров, отсортированные по корпусу, этажу и названию.</summary>
    Task<IReadOnlyList<Room>> ListAsync(RoomQuery query, CancellationToken cancellationToken);

    /// <summary>Аудитория по идентификатору (отслеживается EF Core для последующего изменения).</summary>
    Task<Room?> GetAsync(int id, CancellationToken cancellationToken);

    /// <summary>Занято ли название другой аудиторией.</summary>
    Task<bool> NameExistsAsync(string name, int? excludeRoomId, CancellationToken cancellationToken);

    /// <summary>Добавить аудиторию.</summary>
    Task AddAsync(Room room, CancellationToken cancellationToken);

    /// <summary>Удалить аудиторию.</summary>
    Task DeleteAsync(Room room, CancellationToken cancellationToken);

    /// <summary>Сохранить изменения.</summary>
    Task SaveAsync(CancellationToken cancellationToken);
}