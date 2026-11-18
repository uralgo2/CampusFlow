using System.Globalization;
using System.Security.Claims;
using System.Text;
using CampusFlow.Application.Abstractions;
using CampusFlow.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CampusFlow.Infrastructure.Security;

/// <summary>
///     Выпуск и проверка JWT на базе <see cref="JsonWebTokenHandler" /> (пакет Microsoft.IdentityModel.JsonWebTokens).
///     Подпись — HMAC-SHA256; проверяются алгоритм, подпись, срок действия, издатель и получатель.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private const string SubjectClaim = "sub";
    private const string LoginClaim = "login";
    private const string DisplayNameClaim = "displayName";
    private const string RoleClaim = "role";
    private readonly IClock _clock;

    private readonly JsonWebTokenHandler _handler = new();
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;

    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _options = options.Value;
        _clock = clock;

        if (string.IsNullOrWhiteSpace(_options.Key) || _options.Key.Length < JwtOptions.MinimumKeyLength)
            throw new InvalidOperationException(
                $"Jwt:Key должен содержать минимум {JwtOptions.MinimumKeyLength} символов.");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));

        _signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.Zero,
            NameClaimType = LoginClaim,
            RoleClaimType = RoleClaim,
            LifetimeValidator = ValidateLifetime
        };
    }

    /// <inheritdoc />
    public string CreateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAt = _clock.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.Add(_options.Lifetime),
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [SubjectClaim] = user.Id.ToString(CultureInfo.InvariantCulture),
                [LoginClaim] = user.Login,
                [DisplayNameClaim] = user.DisplayName,
                [RoleClaim] = UserRoles.ToCode(user.Role),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N")
            }
        };

        return _handler.CreateToken(descriptor);
    }

    /// <inheritdoc />
    public async Task<TokenValidationOutcome> ValidateAsync(
        string? token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return TokenValidationOutcome.Failure("Токен отсутствует");

        try
        {
            var result = await _handler
                .ValidateTokenAsync(token.Trim(), _validationParameters)
                .WaitAsync(cancellationToken);

            if (!result.IsValid || result.ClaimsIdentity is null)
                return TokenValidationOutcome.Failure(Describe(result.Exception));

            var identity = result.ClaimsIdentity;

            var subject = FindClaim(identity, SubjectClaim) ?? FindClaim(identity, ClaimTypes.NameIdentifier);
            if (!int.TryParse(subject, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
                return TokenValidationOutcome.Failure("В токене нет идентификатора пользователя");

            var login = FindClaim(identity, LoginClaim) ?? FindClaim(identity, ClaimTypes.Name);
            var roleCode = FindClaim(identity, RoleClaim);
            if (string.IsNullOrEmpty(login) || !UserRoles.TryParse(roleCode, out var role))
                return TokenValidationOutcome.Failure("В токене нет роли пользователя");

            var displayName = FindClaim(identity, DisplayNameClaim) ?? login;
            var expiresAt = result.SecurityToken is JsonWebToken jsonWebToken
                ? jsonWebToken.ValidTo
                : _clock.UtcNow.Add(_options.Lifetime);

            return TokenValidationOutcome.Success(new AuthenticationToken(
                userId,
                login!,
                displayName!,
                role,
                DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)));
        }
        catch (SecurityTokenException exception)
        {
            return TokenValidationOutcome.Failure(Describe(exception));
        }
        catch (ArgumentException)
        {
            return TokenValidationOutcome.Failure("Неверный формат токена");
        }
    }

    private static string? FindClaim(ClaimsIdentity identity, string claimType)
    {
        return identity.FindFirst(claimType)?.Value;
    }

    private bool ValidateLifetime(
        DateTime? notBefore,
        DateTime? expires,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters)
    {
        var now = DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc);

        if (expires is null || expires.Value <= now) return false;

        return notBefore is null || notBefore.Value <= now;
    }

    private static string Describe(Exception? exception)
    {
        return exception switch
        {
            SecurityTokenExpiredException => "Срок действия токена истёк",
            SecurityTokenInvalidLifetimeException => "Срок действия токена истёк",
            SecurityTokenNoExpirationException => "В токене нет срока действия",
            SecurityTokenInvalidSignatureException => "Подпись токена недействительна",
            SecurityTokenInvalidIssuerException => "Неверный издатель токена",
            SecurityTokenInvalidAudienceException => "Неверный получатель токена",
            SecurityTokenInvalidAlgorithmException => "Неподдерживаемый алгоритм подписи",
            SecurityTokenMalformedException => "Неверный формат токена",
            _ => "Токен недействителен"
        };
    }
}