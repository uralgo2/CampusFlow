using System.Text.Json.Serialization;

namespace CampusFlow.Contracts.Common;

/// <summary>Единый формат ошибки API: <c>{code, message, details}</c>.</summary>
/// <param name="Code">Машинный код ошибки, например <c>BOOKING_CONFLICT</c>.</param>
/// <param name="Message">Понятное человеку сообщение на русском языке.</param>
/// <param name="Details">Дополнительные сведения об ошибке (поля, комната, слот). Может отсутствовать.</param>
public sealed record ErrorResponse(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Details = null);
