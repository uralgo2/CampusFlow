namespace CampusFlow.Contracts.Rooms;

/// <summary>Полное обновление аудитории (только администратор). Все поля обязательны.</summary>
/// <param name="Name">Название, непустое и уникальное.</param>
/// <param name="Building">Корпус: A, B, C или D.</param>
/// <param name="Floor">Этаж, не меньше 1.</param>
/// <param name="Capacity">Вместимость, больше 0.</param>
/// <param name="Equipment">Оборудование: подмножество допустимых кодов.</param>
public sealed record UpdateRoomRequest(
    string? Name,
    string? Building,
    int? Floor,
    int? Capacity,
    IReadOnlyList<string>? Equipment);
