using CampusFlow.Contracts.Auth;

namespace CampusFlow.Application.Services;

/// <summary>Вход в систему и данные текущего пользователя.</summary>
public interface IAuthService
{
    /// <summary>Проверяет логин/пароль (password = login) и выпускает JWT-токен.</summary>
    Task<LoginResponse> LoginAsync(LoginRequest? request, CancellationToken cancellationToken);

    /// <summary>Данные пользователя из токена.</summary>
    Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken);
}