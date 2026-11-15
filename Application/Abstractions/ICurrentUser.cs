using CampusFlow.Domain;

namespace CampusFlow.Application.Abstractions;

/// <summary>Текущий пользователь запроса, восстановленный из токена.</summary>
public interface ICurrentUser
{
    /// <summary>Идентификатор пользователя или <c>null</c> для анонимного запроса.</summary>
    int? UserId { get; }

    /// <summary>Логин пользователя.</summary>
    string? Login { get; }

    /// <summary>Отображаемое имя пользователя.</summary>
    string? DisplayName { get; }

    /// <summary>Роль пользователя.</summary>
    UserRole? Role { get; }

    /// <summary>Признак администратора.</summary>
    bool IsAdmin { get; }

    /// <summary>Идентификатор пользователя или 401 UNAUTHORIZED, если запрос анонимный.</summary>
    int RequireUserId();
}
