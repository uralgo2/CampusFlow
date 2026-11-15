namespace CampusFlow.Contracts.Auth;

/// <summary>Пользователь в ответах API.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Login">Логин.</param>
/// <param name="DisplayName">Отображаемое имя.</param>
/// <param name="Role">Роль: <c>student</c> или <c>admin</c>.</param>
public sealed record UserDto(int Id, string Login, string DisplayName, string Role);
