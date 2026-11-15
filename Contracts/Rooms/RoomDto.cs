namespace CampusFlow.Contracts.Rooms;

/// <summary>Аудитория в ответах API.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Name">Название, например <c>A-101</c>.</param>
/// <param name="Building">Корпус: A, B, C или D.</param>
/// <param name="Floor">Этаж.</param>
/// <param name="Capacity">Вместимость.</param>
/// <param name="Equipment">Оборудование: <c>projector</c>, <c>whiteboard</c>, <c>computers</c>, <c>audio</c>, <c>video</c>.</param>
public sealed record RoomDto(
    int Id,
    string Name,
    string Building,
    int Floor,
    int Capacity,
    IReadOnlyList<string> Equipment);
