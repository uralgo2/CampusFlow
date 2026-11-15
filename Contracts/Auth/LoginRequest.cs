namespace CampusFlow.Contracts.Auth;

/// <summary>Запрос входа. В MVP пароль совпадает с логином.</summary>
/// <param name="Login">Логин пользователя, например <c>student1</c>.</param>
/// <param name="Password">Пароль; для seed-пользователей равен логину.</param>
public sealed record LoginRequest(string? Login, string? Password);
