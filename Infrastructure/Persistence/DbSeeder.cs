using CampusFlow.Domain;

namespace CampusFlow.Infrastructure.Persistence;

/// <summary>
/// Тестовые данные из раздаточного материала: 3 пользователя, 12 аудиторий, 7 броней.
/// Даты броней задаются относительно дня запуска (0 = сегодня, +1 = завтра, +2 = послезавтра).
/// </summary>
public static class DbSeeder
{
    /// <summary>Заполняет пустую базу seed-данными.</summary>
    public static async Task SeedAsync(CampusFlowDbContext db, DateOnly today, CancellationToken cancellationToken = default)
    {
        var tomorrow = today.AddDays(1);
        var afterTomorrow = today.AddDays(2);

        db.Users.AddRange(
            new User { Id = 1, Login = "student1", DisplayName = "Иван Студентов", Role = UserRole.Student },
            new User { Id = 2, Login = "student2", DisplayName = "Мария Студентова", Role = UserRole.Student },
            new User { Id = 3, Login = "admin", DisplayName = "Администратор Пупкин", Role = UserRole.Admin });

        db.Rooms.AddRange(
            Room(1, "A-101", "A", 1, 30, Equipment.Projector, Equipment.Whiteboard),
            Room(2, "A-102", "A", 1, 20, Equipment.Whiteboard),
            Room(3, "A-203", "A", 2, 15, Equipment.Computers, Equipment.Projector),
            Room(4, "B-101", "B", 1, 40, Equipment.Projector, Equipment.Audio),
            Room(5, "B-204", "B", 2, 25, Equipment.Projector, Equipment.Whiteboard, Equipment.Audio),
            Room(6, "B-305", "B", 3, 10, Equipment.Whiteboard),
            Room(7, "C-110", "C", 1, 50, Equipment.Projector, Equipment.Audio, Equipment.Video),
            Room(8, "C-210", "C", 2, 35, Equipment.Computers),
            Room(9, "C-301", "C", 3, 12, Equipment.Whiteboard, Equipment.Projector),
            Room(10, "D-101", "D", 1, 60, Equipment.Projector, Equipment.Audio, Equipment.Video),
            Room(11, "D-202", "D", 2, 18, Equipment.Whiteboard),
            Room(12, "D-303", "D", 3, 24, Equipment.Projector, Equipment.Computers));

        // Аудитории и пользователи сохраняются первыми: на них ссылаются внешние ключи броней.
        await db.SaveChangesAsync(cancellationToken);

        db.Bookings.AddRange(
            Booking(1, 1, 1, tomorrow, "10:00", "11:30", BookingStatus.Active, "Консультация"),
            Booking(2, 1, 2, tomorrow, "14:00", "15:30", BookingStatus.Active, "Подготовка к семинару"),
            Booking(3, 4, 2, tomorrow, "09:00", "10:30", BookingStatus.Active, "Собрание группы"),
            Booking(4, 7, 3, tomorrow, "12:00", "14:00", BookingStatus.Active, "Встреча с деканом"),
            Booking(5, 1, 1, today, "15:00", "16:00", BookingStatus.Cancelled, "Отменили"),
            Booking(6, 10, 2, afterTomorrow, "13:00", "15:00", BookingStatus.Active, "Лекция"),
            Booking(7, 5, 1, tomorrow, "16:00", "17:30", BookingStatus.Active, "Кружок"));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Room Room(
        int id,
        string name,
        string building,
        int floor,
        int capacity,
        params Equipment[] equipment) =>
        new()
        {
            Id = id,
            Name = name,
            Building = building,
            Floor = floor,
            Capacity = capacity,
            Equipment = equipment.ToList()
        };

    private static Booking Booking(
        int id,
        int roomId,
        int userId,
        DateOnly date,
        string startTime,
        string endTime,
        BookingStatus status,
        string? comment) =>
        new()
        {
            Id = id,
            RoomId = roomId,
            UserId = userId,
            Date = date,
            StartTime = TimeOnly.Parse(startTime, System.Globalization.CultureInfo.InvariantCulture),
            EndTime = TimeOnly.Parse(endTime, System.Globalization.CultureInfo.InvariantCulture),
            Status = status,
            Comment = comment
        };
}
