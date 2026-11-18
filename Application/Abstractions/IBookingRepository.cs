using CampusFlow.Domain;

namespace CampusFlow.Application.Abstractions;

/// <summary>Хранилище броней.</summary>
public interface IBookingRepository
{
    /// <summary>Брони аудитории на дату; при <paramref name="activeOnly" /> отменённые не возвращаются.</summary>
    Task<IReadOnlyList<Booking>> ListForRoomAndDateAsync(
        int roomId,
        DateOnly date,
        bool activeOnly,
        CancellationToken cancellationToken);

    /// <summary>Брони пользователя с необязательным фильтром по статусу.</summary>
    Task<IReadOnlyList<Booking>> ListForUserAsync(int userId, BookingStatus? status,
        CancellationToken cancellationToken);

    /// <summary>Брони с произвольными фильтрами (админ-просмотр), отсортированные по дате и времени.</summary>
    Task<IReadOnlyList<Booking>> ListAsync(BookingQuery query, CancellationToken cancellationToken);

    /// <summary>Брони за период по набору аудиторий (статистика загрузки).</summary>
    Task<IReadOnlyList<Booking>> ListForPeriodAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<int>? roomIds,
        CancellationToken cancellationToken);

    /// <summary>Бронь по идентификатору вместе с аудиторией и автором.</summary>
    Task<Booking?> GetAsync(int id, CancellationToken cancellationToken);

    /// <summary>Есть ли действующая бронь с пересекающимся интервалом.</summary>
    Task<bool> HasConflictAsync(
        int roomId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        int? excludeBookingId,
        CancellationToken cancellationToken);

    /// <summary>Количество действующих броней аудитории на все даты.</summary>
    Task<int> CountActiveForRoomAsync(int roomId, CancellationToken cancellationToken);

    /// <summary>Добавить бронь и сохранить изменения.</summary>
    Task<Booking> AddAsync(Booking booking, CancellationToken cancellationToken);

    /// <summary>Сохранить изменения.</summary>
    Task SaveAsync(CancellationToken cancellationToken);
}