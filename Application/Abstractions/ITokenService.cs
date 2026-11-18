using CampusFlow.Domain;

namespace CampusFlow.Application.Abstractions;

/// <summary>Выпуск и проверка JWT-токенов.</summary>
public interface ITokenService
{
    /// <summary>Создать JWT для пользователя.</summary>
    string CreateToken(User user);

    /// <summary>Проверить подпись, срок действия, издателя и получателя токена.</summary>
    Task<TokenValidationOutcome> ValidateAsync(string? token, CancellationToken cancellationToken = default);
}