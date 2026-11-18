namespace CampusFlow.Domain;

/// <summary>Оборудование, которое может быть в аудитории.</summary>
public enum Equipment
{
    /// <summary>Проектор.</summary>
    Projector,

    /// <summary>Доска.</summary>
    Whiteboard,

    /// <summary>Компьютеры.</summary>
    Computers,

    /// <summary>Аудиосистема.</summary>
    Audio,

    /// <summary>Видеосистема.</summary>
    Video
}

/// <summary>Справочник кодов оборудования: контракт API работает со строками в нижнем регистре.</summary>
public static class EquipmentCatalog
{
    private static readonly Dictionary<string, Equipment> ByCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["projector"] = Equipment.Projector,
        ["whiteboard"] = Equipment.Whiteboard,
        ["computers"] = Equipment.Computers,
        ["audio"] = Equipment.Audio,
        ["video"] = Equipment.Video
    };

    /// <summary>Все допустимые коды оборудования в порядке объявления.</summary>
    public static IReadOnlyList<string> Codes { get; } = ByCode.Keys.ToList();

    /// <summary>Получить код оборудования для контракта API.</summary>
    public static string ToCode(Equipment equipment)
    {
        foreach (var pair in ByCode)
            if (pair.Value == equipment)
                return pair.Key;

        throw new ArgumentOutOfRangeException(nameof(equipment), equipment, "Неизвестный тип оборудования");
    }

    /// <summary>Разобрать код оборудования без учёта регистра.</summary>
    public static bool TryParse(string? code, out Equipment equipment)
    {
        equipment = default;
        if (string.IsNullOrWhiteSpace(code)) return false;

        return ByCode.TryGetValue(code.Trim(), out equipment);
    }

    /// <summary>Разобрать код оборудования, бросив исключение при ошибке.</summary>
    public static Equipment Parse(string code)
    {
        return TryParse(code, out var equipment)
            ? equipment
            : throw new FormatException($"Неизвестное оборудование: {code}");
    }
}