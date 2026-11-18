using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CampusFlow.Domain;

namespace CampusFlow.Infrastructure.Persistence;

/// <summary>
///     Загрузка тестовых данных из JSON-файла в базу данных.
/// </summary>
public static class DbSeeder
{
    private const string SeedFileName = "SeedData.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    /// <summary>
    ///     Загружает тестовые данные из JSON и сохраняет их в SQLite.
    /// </summary>
    public static async Task SeedAsync(
        CampusFlowDbContext db,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(
            AppContext.BaseDirectory,
            SeedFileName);

        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                $"Файл тестовых данных не найден: {filePath}");

        await using var stream = File.OpenRead(filePath);

        var seedData = await JsonSerializer.DeserializeAsync<SeedData>(
            stream,
            JsonOptions,
            cancellationToken);

        if (seedData is null)
            throw new InvalidDataException(
                "Не удалось прочитать JSON seed-файл.");

        // Проверяем наличие всех разделов.
        if (seedData.Users.Count == 0 ||
            seedData.Rooms.Count == 0 ||
            seedData.Bookings.Count == 0)
            throw new InvalidDataException(
                "JSON seed-файл содержит пустые разделы.");

        // Загружаем пользователей.
        db.Users.AddRange(
            seedData.Users.Select(user => new User
            {
                Id = user.Id,
                Login = user.Login,
                DisplayName = user.DisplayName,
                Role = user.Role
            }));

        // Загружаем аудитории.
        db.Rooms.AddRange(
            seedData.Rooms.Select(room => new Room
            {
                Id = room.Id,
                Name = room.Name,
                Building = room.Building,
                Floor = room.Floor,
                Capacity = room.Capacity,
                Equipment = room.Equipment
            }));

        // Сначала сохраняем пользователей и аудитории,
        // так как на них ссылаются бронирования.
        await db.SaveChangesAsync(cancellationToken);

        // Загружаем бронирования.
        db.Bookings.AddRange(
            seedData.Bookings.Select(booking => new Booking
            {
                Id = booking.Id,
                RoomId = booking.RoomId,
                UserId = booking.UserId,
                Date = today.AddDays(booking.DateOffset),
                StartTime = TimeOnly.ParseExact(
                    booking.StartTime,
                    "HH:mm",
                    CultureInfo.InvariantCulture),
                EndTime = TimeOnly.ParseExact(
                    booking.EndTime,
                    "HH:mm",
                    CultureInfo.InvariantCulture),
                Status = booking.Status,
                Comment = booking.Comment
            }));

        await db.SaveChangesAsync(cancellationToken);
    }

    // Модели структуры JSON-файла.

    public sealed class SeedData
    {
        public List<UserSeed> Users { get; set; } = new();

        public List<RoomSeed> Rooms { get; set; } = new();

        public List<BookingSeed> Bookings { get; set; } = new();
    }

    public sealed class UserSeed
    {
        public int Id { get; set; }

        public string Login { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public UserRole Role { get; set; }
    }

    public sealed class RoomSeed
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Building { get; set; } = string.Empty;

        public int Floor { get; set; }

        public int Capacity { get; set; }

        public List<Equipment> Equipment { get; set; } = new();
    }

    public sealed class BookingSeed
    {
        public int Id { get; set; }

        public int RoomId { get; set; }

        public int UserId { get; set; }

        public int DateOffset { get; set; }

        public string StartTime { get; set; } = string.Empty;

        public string EndTime { get; set; } = string.Empty;

        public BookingStatus Status { get; set; }

        public string? Comment { get; set; }
    }
}