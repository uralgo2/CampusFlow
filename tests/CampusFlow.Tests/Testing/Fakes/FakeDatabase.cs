using System.Globalization;
using CampusFlow.Domain;

namespace CampusFlow.Tests.Testing.Fakes;

/// <summary>Общее «хранилище» для in-memory репозиториев: списки вместо таблиц EF Core.</summary>
public sealed class FakeDatabase
{
    private int _nextRoomId = 1;
    private int _nextBookingId = 1;

    public List<User> Users { get; } = new();

    public List<Room> Rooms { get; } = new();

    public List<Booking> Bookings { get; } = new();

    public User AddUser(int id, string login, string displayName, UserRole role)
    {
        var user = new User
        {
            Id = id,
            Login = login,
            DisplayName = displayName,
            Role = role
        };

        Users.Add(user);
        return user;
    }

    public Room AddRoom(string name, string building, int floor, int capacity, params Equipment[] equipment)
    {
        var room = new Room
        {
            Id = _nextRoomId++,
            Name = name,
            Building = building,
            Floor = floor,
            Capacity = capacity,
            Equipment = equipment.ToList()
        };

        Rooms.Add(room);
        return room;
    }

    public Booking AddBooking(
        int roomId,
        int userId,
        DateOnly date,
        string startTime,
        string endTime,
        BookingStatus status = BookingStatus.Active,
        string? comment = null)
    {
        var booking = new Booking
        {
            Id = _nextBookingId++,
            RoomId = roomId,
            UserId = userId,
            Date = date,
            StartTime = TimeOnly.Parse(startTime, CultureInfo.InvariantCulture),
            EndTime = TimeOnly.Parse(endTime, CultureInfo.InvariantCulture),
            Status = status,
            Comment = comment
        };

        Bookings.Add(booking);
        return booking;
    }
}
