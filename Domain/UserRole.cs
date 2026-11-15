namespace CampusFlow.Domain;

/// <summary>Роль пользователя сервиса.</summary>
public enum UserRole
{
    /// <summary>Студент или староста: ищет аудитории и бронирует слоты.</summary>
    Student,

    /// <summary>Администратор: управляет справочником и всеми бронями.</summary>
    Admin
}

/// <summary>Коды ролей в контракте API.</summary>
public static class UserRoles
{
    public const string Student = "student";
    public const string Admin = "admin";

    /// <summary>Код роли для ответа API.</summary>
    public static string ToCode(UserRole role) => role switch
    {
        UserRole.Student => Student,
        UserRole.Admin => Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Неизвестная роль")
    };

    /// <summary>Разбор кода роли без учёта регистра.</summary>
    public static bool TryParse(string? code, out UserRole role)
    {
        role = default;
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        switch (code.Trim().ToLowerInvariant())
        {
            case Student:
                role = UserRole.Student;
                return true;
            case Admin:
                role = UserRole.Admin;
                return true;
            default:
                return false;
        }
    }
}
