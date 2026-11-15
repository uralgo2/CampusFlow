namespace CampusFlow.Infrastructure.Security;

/// <summary>Настройки JWT из секции <c>Jwt</c> (appsettings.json или переменные окружения).</summary>
public sealed class JwtOptions
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Минимальная длина секрета для HMAC-SHA256 (32 байта).</summary>
    public const int MinimumKeyLength = 32;

    /// <summary>Издатель токена.</summary>
    public string Issuer { get; set; } = "CampusFlow";

    /// <summary>Получатель токена.</summary>
    public string Audience { get; set; } = "CampusFlow";

    /// <summary>Секрет подписи. Для demo-запуска задан в appsettings.json, в production — только через окружение.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Срок действия токена в минутах.</summary>
    public int LifetimeMinutes { get; set; } = 480;

    /// <summary>Срок действия в виде интервала.</summary>
    public TimeSpan Lifetime => TimeSpan.FromMinutes(LifetimeMinutes > 0 ? LifetimeMinutes : 480);
}
