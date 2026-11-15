namespace CampusFlow.Contracts.Auth;

/// <summary>Ответ на успешный вход.</summary>
/// <param name="Token">JWT-токен; передаётся в заголовке <c>Authorization: Bearer</c>.</param>
/// <param name="User">Данные пользователя.</param>
public sealed record LoginResponse(string Token, UserDto User);
