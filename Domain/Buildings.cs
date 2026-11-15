namespace CampusFlow.Domain;

/// <summary>Корпуса, доступные в справочнике аудиторий.</summary>
public static class Buildings
{
    /// <summary>Допустимые корпуса.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { "A", "B", "C", "D" };

    /// <summary>
    /// Приводит корпус к каноническому виду (одна заглавная буква) и проверяет, что он допустим.
    /// </summary>
    public static bool TryNormalize(string? value, out string building)
    {
        building = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim().ToUpperInvariant();
        if (!All.Contains(candidate, StringComparer.Ordinal))
        {
            return false;
        }

        building = candidate;
        return true;
    }
}
