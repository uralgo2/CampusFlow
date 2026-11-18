using CampusFlow.Application.Abstractions;
using CampusFlow.Application.Errors;
using CampusFlow.Application.Mapping;
using CampusFlow.Contracts.Auth;

namespace CampusFlow.Application.Services;

/// <inheritdoc />
public sealed class AuthService : IAuthService
{
    private readonly ICurrentUser _currentUser;
    private readonly ITokenService _tokens;
    private readonly IUserRepository _users;

    public AuthService(IUserRepository users, ITokenService tokens, ICurrentUser currentUser)
    {
        _users = users;
        _tokens = tokens;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(LoginRequest? request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();
        var login = request?.Login?.Trim();
        var password = request?.Password;

        if (string.IsNullOrEmpty(login)) errors["login"] = "Поле login обязательно";

        if (string.IsNullOrEmpty(password)) errors["password"] = "Поле password обязательно";

        if (errors.Count > 0) throw ApiException.Validation(errors);

        var user = await _users.GetByLoginAsync(login!, cancellationToken);

        // MVP: пароль совпадает с логином, отдельного хранилища паролей нет.
        if (user is null || !string.Equals(password, user.Login, StringComparison.OrdinalIgnoreCase))
            throw ApiException.Unauthorized("Неверный логин или пароль");

        return new LoginResponse(_tokens.CreateToken(user), DtoMapper.ToDto(user));
    }

    /// <inheritdoc />
    public async Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var user = await _users.GetByIdAsync(userId, cancellationToken)
                   ?? throw ApiException.Unauthorized("Пользователь из токена не найден");

        return DtoMapper.ToDto(user);
    }
}