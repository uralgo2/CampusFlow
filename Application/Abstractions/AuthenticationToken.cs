using CampusFlow.Domain;

namespace CampusFlow.Application.Abstractions;

/// <summary>Проверенная полезная нагрузка JWT-токена.</summary>
/// <param name="UserId">Идентификатор пользователя.</param>
/// <param name="Login">Логин.</param>
/// <param name="DisplayName">Отображаемое имя.</param>
/// <param name="Role">Роль.</param>
/// <param name="ExpiresAtUtc">Момент истечения срока действия (UTC).</param>
public sealed record AuthenticationToken(
    int UserId,
    string Login,
    string DisplayName,
    UserRole Role,
    DateTime ExpiresAtUtc);

/// <summary>Результат проверки токена: либо данные пользователя, либо причина отказа.</summary>
public sealed class TokenValidationOutcome
{
    private TokenValidationOutcome(AuthenticationToken? identity, string? error)
    {
        Identity = identity;
        Error = error;
    }

    /// <summary>Токен прошёл проверку подписи, срока действия, издателя и получателя.</summary>
    public bool IsValid => Error is null;

    /// <summary>Данные пользователя из токена или <c>null</c>, если токен не прошёл проверку.</summary>
    public AuthenticationToken? Identity { get; }

    /// <summary>Причина отказа или <c>null</c> для валидного токена.</summary>
    public string? Error { get; }

    /// <summary>Успешная проверка.</summary>
    public static TokenValidationOutcome Success(AuthenticationToken identity) => new(identity, null);

    /// <summary>Отказ с причиной.</summary>
    public static TokenValidationOutcome Failure(string error) => new(null, error);
}
