namespace CampusFlow.Domain;

/// <summary>Пользователь сервиса.</summary>
public sealed class User
{
    public int Id { get; set; }

    public string Login { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
