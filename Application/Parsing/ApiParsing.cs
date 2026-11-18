using System.Globalization;
using CampusFlow.Domain;

namespace CampusFlow.Application.Parsing;

/// <summary>Разбор и форматирование значений контракта API: даты <c>YYYY-MM-DD</c> и времени <c>HH:MM</c>.</summary>
public static class ApiParsing
{
    /// <summary>Формат даты в контракте.</summary>
    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>Формат времени в контракте.</summary>
    public const string TimeFormat = "HH:mm";

    /// <summary>Дата в формате контракта.</summary>
    public static string FormatDate(DateOnly date)
    {
        return date.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>Время в формате контракта.</summary>
    public static string FormatTime(TimeOnly time)
    {
        return time.ToString(TimeFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>Строгий разбор даты <c>YYYY-MM-DD</c>.</summary>
    public static bool TryParseDate(string? value, out DateOnly date)
    {
        return DateOnly.TryParseExact(
            value?.Trim(),
            DateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    /// <summary>Разбор времени <c>HH:MM</c>; допускается <c>H:MM</c>, секунды не допускаются.</summary>
    public static bool TryParseTime(string? value, out TimeOnly time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();
        return TimeOnly.TryParseExact(trimmed, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
               || TimeOnly.TryParseExact(trimmed, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    /// <summary>Нормализует необязательную строку: обрезает пробелы и превращает пустую строку в <c>null</c>.</summary>
    public static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Код статуса брони из query-параметра или <c>null</c>.</summary>
    public static BookingStatus? ParseStatusOrNull(string? value)
    {
        return BookingStatuses.TryParse(value, out var status) ? status : null;
    }
}