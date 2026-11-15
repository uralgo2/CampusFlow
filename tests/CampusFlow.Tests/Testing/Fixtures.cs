using System.Globalization;
using CampusFlow.Domain;

namespace CampusFlow.Tests.Testing;

/// <summary>Общие данные для тестов: «сегодня» фиксировано, чтобы тесты не зависели от календаря.</summary>
public static class Fixtures
{
    public static readonly DateOnly Today = new(2026, 11, 14);

    public static readonly DateTime NowUtc = new(2026, 11, 14, 8, 0, 0, DateTimeKind.Utc);

    public static Fakes.FakeClock Clock() => new(Today, NowUtc);

    /// <summary>Дата в формате контракта.</summary>
    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Завтрашняя дата в формате контракта — основная для тестов бронирования.</summary>
    public static string Tomorrow() => Iso(Today.AddDays(1));

    /// <summary>Три пользователя и две аудитории — минимальный набор для сервисных тестов.</summary>
    public static StandardData Standard()
    {
        var db = new Fakes.FakeDatabase();

        var student1 = db.AddUser(1, "student1", "Иван Студентов", UserRole.Student);
        var student2 = db.AddUser(2, "student2", "Мария Студентова", UserRole.Student);
        var admin = db.AddUser(3, "admin", "Администратор Пупкин", UserRole.Admin);

        var a101 = db.AddRoom("A-101", "A", 1, 30, Equipment.Projector, Equipment.Whiteboard);
        var b101 = db.AddRoom("B-101", "B", 1, 40, Equipment.Projector, Equipment.Audio);

        return new StandardData(db, student1, student2, admin, a101, b101);
    }

    public sealed record StandardData(
        Fakes.FakeDatabase Db,
        User Student1,
        User Student2,
        User Admin,
        Room A101,
        Room B101);
}
