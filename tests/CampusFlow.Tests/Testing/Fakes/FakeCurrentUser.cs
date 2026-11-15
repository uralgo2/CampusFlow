using CampusFlow.Application.Abstractions;
using CampusFlow.Application.Errors;
using CampusFlow.Domain;

namespace CampusFlow.Tests.Testing.Fakes;

/// <summary>Текущий пользователь запроса, задаваемый тестом.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public int? UserId { get; set; }

    public string? Login { get; set; }

    public string? DisplayName { get; set; }

    public UserRole? Role { get; set; }

    public bool IsAdmin => Role == UserRole.Admin;

    public int RequireUserId() => UserId ?? throw ApiException.Unauthorized();

    public static FakeCurrentUser Student(int userId = 1, string login = "student1", string displayName = "Иван Студентов") =>
        new() { UserId = userId, Login = login, DisplayName = displayName, Role = UserRole.Student };

    public static FakeCurrentUser Admin(int userId = 3, string login = "admin", string displayName = "Администратор Пупкин") =>
        new() { UserId = userId, Login = login, DisplayName = displayName, Role = UserRole.Admin };

    public static FakeCurrentUser Anonymous() => new();
}
