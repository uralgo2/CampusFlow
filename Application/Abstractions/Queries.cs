using CampusFlow.Domain;

namespace CampusFlow.Application.Abstractions;

/// <summary>Фильтр списка аудиторий.</summary>
/// <param name="Building">Корпус или <c>null</c>.</param>
/// <param name="MinCapacity">Минимальная вместимость или <c>null</c>.</param>
/// <param name="Equipment">Оборудование, которое должно присутствовать целиком; пустой список — без фильтра.</param>
public sealed record RoomQuery(string? Building, int? MinCapacity, IReadOnlyList<Equipment> Equipment)
{
    /// <summary>Пустой фильтр.</summary>
    public static RoomQuery Empty { get; } = new(null, null, Array.Empty<Equipment>());
}

/// <summary>Фильтр списка броней.</summary>
/// <param name="Date">Дата или <c>null</c>.</param>
/// <param name="RoomId">Идентификатор аудитории или <c>null</c>.</param>
/// <param name="UserId">Идентификатор автора или <c>null</c>.</param>
/// <param name="Status">Статус или <c>null</c> — все брони.</param>
public sealed record BookingQuery(DateOnly? Date, int? RoomId, int? UserId, BookingStatus? Status);