namespace CampusFlow.Domain;

/// <summary>Аудитория: единица справочника, которую занимают брони.</summary>
public sealed class Room
{
    public int Id { get; set; }

    /// <summary>Номер или название аудитории, например <c>A-101</c>. Уникально.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Корпус: A, B, C или D.</summary>
    public string Building { get; set; } = "A";

    /// <summary>Этаж, начиная с 1.</summary>
    public int Floor { get; set; } = 1;

    /// <summary>Вместимость, больше 0.</summary>
    public int Capacity { get; set; }

    /// <summary>Оборудование аудитории.</summary>
    public List<Equipment> Equipment { get; set; } = new();

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}