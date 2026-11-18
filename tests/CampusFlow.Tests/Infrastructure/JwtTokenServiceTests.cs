using System.Text;
using System.Text.Json.Nodes;
using CampusFlow.Domain;
using CampusFlow.Infrastructure.Security;
using CampusFlow.Tests.Testing;
using CampusFlow.Tests.Testing.Fakes;
using Microsoft.Extensions.Options;
using static CampusFlow.Tests.Testing.Assert;

namespace CampusFlow.Tests.Infrastructure;

/// <summary>JWT на Microsoft.IdentityModel.JsonWebTokens: выпуск, подпись, срок, издатель, получатель.</summary>
public sealed class JwtTokenServiceTests
{
    private const string SigningKey = "campusflow-unit-test-signing-key-0123456789";

    [Test]
    public async Task Create_ThenValidate_ReturnsIdentity()
    {
        var (service, _) = Create();

        var token = service.CreateToken(Student());
        var outcome = await service.ValidateAsync(token);

        True(outcome.IsValid, outcome.Error ?? "токен должен быть валиден");
        Equal(1, outcome.Identity!.UserId);
        Equal("student1", outcome.Identity.Login);
        Equal("Иван Студентов", outcome.Identity.DisplayName);
        Equal(UserRole.Student, outcome.Identity.Role);
        True(outcome.Identity.ExpiresAtUtc > Fixtures.NowUtc, "срок действия должен быть в будущем");
    }

    [Test]
    public void Create_TokenContainsStandardClaims()
    {
        var (service, _) = Create();

        var parts = service.CreateToken(Student()).Split('.');

        Equal(3, parts.Length, "JWT состоит из трёх частей");
        var header = JsonNode.Parse(Decode(parts[0]))!;
        Equal("HS256", header["alg"]!.GetValue<string>());

        var payload = JsonNode.Parse(Decode(parts[1]))!;
        Equal("1", payload["sub"]!.GetValue<string>());
        Equal("student1", payload["login"]!.GetValue<string>());
        Equal("student", payload["role"]!.GetValue<string>());
        Equal("CampusFlow", payload["iss"]!.GetValue<string>());
        Equal("CampusFlow", payload["aud"]!.GetValue<string>());
        True(payload["exp"]!.GetValue<long>() > payload["iat"]!.GetValue<long>(), "exp должен быть позже iat");
        True(!string.IsNullOrEmpty(payload["jti"]!.GetValue<string>()), "у токена должен быть jti");
    }

    [Test]
    public void Create_EachTokenIsUnique()
    {
        var (service, _) = Create();

        NotEqual(service.CreateToken(Student()), service.CreateToken(Student()), "jti делает токены разными");
    }

    [Test]
    public async Task Validate_ExpiredToken_IsRejected()
    {
        var (service, clock) = Create(60);
        var token = service.CreateToken(Student());

        clock.Advance(TimeSpan.FromMinutes(61));
        var outcome = await service.ValidateAsync(token);

        False(outcome.IsValid, "просроченный токен не должен проходить проверку");
        Contains("истёк", outcome.Error!);
    }

    [Test]
    public async Task Validate_TamperedPayload_IsRejected()
    {
        var (service, _) = Create();
        var parts = service.CreateToken(Student()).Split('.');

        var payload = JsonNode.Parse(Decode(parts[1]))!.AsObject();
        payload["role"] = "admin";
        var tampered = $"{parts[0]}.{Encode(Encoding.UTF8.GetBytes(payload.ToJsonString()))}.{parts[2]}";

        var outcome = await service.ValidateAsync(tampered);

        False(outcome.IsValid, "изменённые данные ломают подпись");
        Contains("Подпись", outcome.Error!);
    }

    [Test]
    public async Task Validate_AnotherSigningKey_IsRejected()
    {
        var (issuer, _) = Create();
        var (another, _) = Create(key: "another-signing-key-for-tests-0123456789ab");

        var outcome = await another.ValidateAsync(issuer.CreateToken(Student()));

        False(outcome.IsValid, "токен, подписанный другим ключом, не принимается");
        Contains("Подпись", outcome.Error!);
    }

    [Test]
    public async Task Validate_WrongIssuerOrAudience_IsRejected()
    {
        var (service, _) = Create();
        var token = service.CreateToken(Student());

        var (wrongIssuer, _) = Create(issuer: "AnotherIssuer");
        var issuerOutcome = await wrongIssuer.ValidateAsync(token);
        False(issuerOutcome.IsValid, "чужой издатель не принимается");
        Contains("издатель", issuerOutcome.Error!);

        var (wrongAudience, _) = Create(audience: "AnotherAudience");
        var audienceOutcome = await wrongAudience.ValidateAsync(token);
        False(audienceOutcome.IsValid, "чужой получатель не принимается");
        Contains("получатель", audienceOutcome.Error!);
    }

    [Test]
    public async Task Validate_MalformedToken_IsRejected()
    {
        var (service, _) = Create();

        False((await service.ValidateAsync(null)).IsValid, "null не токен");
        False((await service.ValidateAsync(string.Empty)).IsValid, "пустая строка не токен");
        False((await service.ValidateAsync("not-a-jwt")).IsValid, "одна часть — не JWT");
        False((await service.ValidateAsync("a.b")).IsValid, "две части — не JWT");
        False((await service.ValidateAsync("a.b.c.d")).IsValid, "четыре части — не JWT");
        False((await service.ValidateAsync("a.b.c")).IsValid, "не base64 — не JWT");
    }

    [Test]
    public async Task Validate_UnsignedAlgNoneToken_IsRejected()
    {
        var (service, _) = Create();

        var header = Encode(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"));
        var payload = Encode(Encoding.UTF8.GetBytes(
            "{\"sub\":\"1\",\"login\":\"student1\",\"role\":\"admin\",\"iss\":\"CampusFlow\",\"aud\":\"CampusFlow\",\"exp\":9999999999}"));

        False((await service.ValidateAsync($"{header}.{payload}.")).IsValid, "пустая подпись не принимается");
        False((await service.ValidateAsync($"{header}.{payload}.AAAA")).IsValid, "alg=none не поддерживается");
    }

    [Test]
    public void Constructor_TooShortKey_Throws()
    {
        var clock = new FakeClock(Fixtures.Today, Fixtures.NowUtc);

        Throws<InvalidOperationException>(() => new JwtTokenService(
            Options.Create(new JwtOptions { Key = "short" }),
            clock));
    }

    private static User Student()
    {
        return new User
        {
            Id = 1,
            Login = "student1",
            DisplayName = "Иван Студентов",
            Role = UserRole.Student
        };
    }

    private static (JwtTokenService Service, FakeClock Clock) Create(
        int lifetimeMinutes = 60,
        string key = SigningKey,
        string issuer = "CampusFlow",
        string audience = "CampusFlow")
    {
        var clock = new FakeClock(Fixtures.Today, Fixtures.NowUtc);
        var service = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                Issuer = issuer,
                Audience = audience,
                Key = key,
                LifetimeMinutes = lifetimeMinutes
            }),
            clock);

        return (service, clock);
    }

    private static string Encode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Decode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }

        return Convert.FromBase64String(base64);
    }
}