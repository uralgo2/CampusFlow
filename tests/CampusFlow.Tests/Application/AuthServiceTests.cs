using CampusFlow.Application.Errors;
using CampusFlow.Application.Services;
using CampusFlow.Contracts.Auth;
using CampusFlow.Domain;
using CampusFlow.Infrastructure.Security;
using CampusFlow.Tests.Testing;
using CampusFlow.Tests.Testing.Fakes;
using Microsoft.Extensions.Options;
using static CampusFlow.Tests.Testing.Assert;

namespace CampusFlow.Tests.Application;

/// <summary>Вход: проверка пароля, выдача JWT и данные текущего пользователя.</summary>
public sealed class AuthServiceTests
{
    [Test]
    public async Task Login_ValidCredentials_ReturnsJwtToken()
    {
        var fixture = CreateFixture();

        var response =
            await fixture.Service.LoginAsync(new LoginRequest("student1", "student1"), CancellationToken.None);

        Equal(3, response.Token.Split('.').Length, "должен вернуться JWT из трёх частей");
        Equal("student1", response.User.Login);
        Equal("Иван Студентов", response.User.DisplayName);
        Equal("student", response.User.Role);

        var validation = await fixture.Tokens.ValidateAsync(response.Token);
        True(validation.IsValid, $"выданный токен должен проходить проверку: {validation.Error}");
        Equal(1, validation.Identity!.UserId);
        Equal(UserRole.Student, validation.Identity.Role);
    }

    [Test]
    public async Task Login_LoginIsCaseInsensitive()
    {
        var fixture = CreateFixture();

        var response =
            await fixture.Service.LoginAsync(new LoginRequest("Student1", "STUDENT1"), CancellationToken.None);

        Equal(1, response.User.Id);
    }

    [Test]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(
            () => fixture.Service.LoginAsync(new LoginRequest("student1", "wrong"), CancellationToken.None),
            401,
            ErrorCodes.Unauthorized);
    }

    [Test]
    public async Task Login_UnknownUser_ThrowsUnauthorized()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(
            () => fixture.Service.LoginAsync(new LoginRequest("nobody", "nobody"), CancellationToken.None),
            401,
            ErrorCodes.Unauthorized);
    }

    [Test]
    public async Task Login_MissingFields_ThrowValidationError()
    {
        var fixture = CreateFixture();

        var exception = await ThrowsApiErrorAsync(
            () => fixture.Service.LoginAsync(new LoginRequest(null, null), CancellationToken.None),
            400,
            ErrorCodes.Validation);

        var details = (IDictionary<string, string>)exception.Details!;
        True(details.ContainsKey("login"), "должна быть ошибка по login");
        True(details.ContainsKey("password"), "должна быть ошибка по password");
    }

    [Test]
    public async Task GetCurrentUser_ReturnsUserFromToken()
    {
        var fixture = CreateFixture();

        var user = await fixture.Service.GetCurrentUserAsync(CancellationToken.None);

        Equal(1, user.Id);
        Equal("student1", user.Login);
        Equal("student", user.Role);
    }

    [Test]
    public async Task GetCurrentUser_Anonymous_ThrowsUnauthorized()
    {
        var fixture = CreateFixture(FakeCurrentUser.Anonymous());

        await ThrowsApiErrorAsync(
            () => fixture.Service.GetCurrentUserAsync(CancellationToken.None),
            401,
            ErrorCodes.Unauthorized);
    }

    private static AuthFixture CreateFixture(FakeCurrentUser? currentUser = null)
    {
        var data = Fixtures.Standard();
        var clock = Fixtures.Clock();
        var tokens = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                Issuer = "CampusFlow",
                Audience = "CampusFlow",
                Key = "campusflow-unit-test-signing-key-0123456789",
                LifetimeMinutes = 60
            }),
            clock);

        var user = currentUser ?? FakeCurrentUser.Student();
        var service = new AuthService(new InMemoryUserRepository(data.Db), tokens, user);

        return new AuthFixture(service, tokens);
    }

    private sealed record AuthFixture(AuthService Service, JwtTokenService Tokens);
}