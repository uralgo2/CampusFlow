using System.Security.Claims;
using CampusFlow.Application.Abstractions;
using CampusFlow.Application.Errors;
using CampusFlow.Domain;

namespace CampusFlow.Api.Http;

/// <inheritdoc />
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    /// <inheritdoc />
    public int? UserId =>
        int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <inheritdoc />
    public string? Login => Principal?.FindFirstValue(ClaimTypes.Name);

    /// <inheritdoc />
    public string? DisplayName => Principal?.FindFirstValue(AuthClaimTypes.DisplayName);

    /// <inheritdoc />
    public UserRole? Role =>
        UserRoles.TryParse(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;

    /// <inheritdoc />
    public bool IsAdmin => Role == UserRole.Admin;

    /// <inheritdoc />
    public int RequireUserId() => UserId ?? throw ApiException.Unauthorized();
}

public static class AuthClaimTypes
{
    /// <summary>Отображаемое имя пользователя.</summary>
    public const string DisplayName = "displayName";
}
