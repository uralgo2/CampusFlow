using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CampusFlow.Api.Http;
using CampusFlow.Application.Abstractions;
using CampusFlow.Application.Errors;
using CampusFlow.Contracts.Common;
using CampusFlow.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace CampusFlow.Api.Authentication;

/// <summary>
/// Аутентификация по JWT в заголовке <c>Authorization: Bearer &lt;token&gt;</c>.
/// Токен проверяется по подписи и сроку действия, после чего пользователь перечитывается из базы:
/// роль всегда актуальная, а удалённый пользователь не может пользоваться старым токеном.
/// </summary>
public sealed class JwtAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>Имя схемы аутентификации.</summary>
    public const string SchemeName = "Bearer";

    private const string BearerPrefix = "Bearer ";

    private readonly ITokenService _tokens;
    private readonly IUserRepository _users;

    public JwtAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITokenService tokens,
        IUserRepository users)
        : base(options, logger, encoder)
    {
        _tokens = tokens;
        _users = users;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderNames.Authorization, out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var header = headerValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header))
        {
            return AuthenticateResult.NoResult();
        }

        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Ожидается заголовок Authorization: Bearer <JWT>");
        }

        var outcome = await _tokens.ValidateAsync(header[BearerPrefix.Length..].Trim(), Context.RequestAborted);
        if (!outcome.IsValid || outcome.Identity is null)
        {
            return AuthenticateResult.Fail(outcome.Error ?? "Токен недействителен");
        }

        var user = await _users.GetByIdAsync(outcome.Identity.UserId, Context.RequestAborted);
        if (user is null)
        {
            return AuthenticateResult.Fail("Пользователь из токена не найден");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, user.Login),
            new Claim(ClaimTypes.Role, UserRoles.ToCode(user.Role)),
            new Claim(AuthClaimTypes.DisplayName, user.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        await HttpErrorWriter.WriteAsync(
            Context,
            StatusCodes.Status401Unauthorized,
            new ErrorResponse(
                ErrorCodes.Unauthorized,
                "Требуется авторизация: передайте заголовок Authorization: Bearer <JWT>"));
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        await HttpErrorWriter.WriteAsync(
            Context,
            StatusCodes.Status403Forbidden,
            new ErrorResponse(ErrorCodes.Forbidden, "Недостаточно прав для выполнения операции"));
    }
}
