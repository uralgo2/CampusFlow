using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace CampusFlow.Api.Serialization;

/// <summary>Единые настройки JSON: camelCase, UTF-8 без экранирования кириллицы.</summary>
public static class JsonDefaults
{
    /// <summary>Общие настройки для ответов вне MVC (middleware, обработчик аутентификации).</summary>
    public static readonly JsonSerializerOptions Options = Create();

    /// <summary>Создать настройки сериализации.</summary>
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }

    /// <summary>Применить настройки к существующим опциям MVC.</summary>
    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
    }
}
