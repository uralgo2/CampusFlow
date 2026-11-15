using CampusFlow.Application.Errors;
using CampusFlow.Application.Parsing;
using CampusFlow.Domain;

namespace CampusFlow.Application.Validation;

/// <summary>Проверка даты и интервала брони по единым правилам MVP.</summary>
public static class SlotParser
{
    /// <summary>
    /// Проверяет <c>date</c>/<c>startTime</c>/<c>endTime</c> и возвращает разобранные значения.
    /// При нарушении любого правила бросает 400 VALIDATION_ERROR с деталями по полям.
    /// </summary>
    public static (DateOnly Date, TimeOnly Start, TimeOnly End) Parse(
        DateOnly today,
        string? date,
        string? startTime,
        string? endTime)
    {
        var errors = new Dictionary<string, string>();

        DateOnly parsedDate = default;
        if (string.IsNullOrWhiteSpace(date))
        {
            errors["date"] = "Поле date обязательно (YYYY-MM-DD)";
        }
        else if (!ApiParsing.TryParseDate(date, out parsedDate))
        {
            errors["date"] = "Неверный формат даты, ожидается YYYY-MM-DD";
        }

        TimeOnly start = default;
        var startParsed = false;
        if (string.IsNullOrWhiteSpace(startTime))
        {
            errors["startTime"] = "Поле startTime обязательно (HH:MM)";
        }
        else if (!ApiParsing.TryParseTime(startTime, out start))
        {
            errors["startTime"] = "Неверный формат времени, ожидается HH:MM";
        }
        else
        {
            startParsed = true;
        }

        TimeOnly end = default;
        var endParsed = false;
        if (string.IsNullOrWhiteSpace(endTime))
        {
            errors["endTime"] = "Поле endTime обязательно (HH:MM)";
        }
        else if (!ApiParsing.TryParseTime(endTime, out end))
        {
            errors["endTime"] = "Неверный формат времени, ожидается HH:MM";
        }
        else
        {
            endParsed = true;
        }

        if (errors.Count > 0)
        {
            throw ApiException.Validation(errors);
        }

        if (parsedDate < today)
        {
            errors.TryAdd("date", "Дата не может быть в прошлом");
        }

        if (startParsed && endParsed)
        {
            if (start >= end)
            {
                errors.TryAdd("endTime", "Время окончания должно быть позже времени начала");
            }

            if (!SchedulePolicy.IsOnGrid(start))
            {
                errors.TryAdd("startTime", "Время начала должно быть кратно 30 минутам");
            }

            if (!SchedulePolicy.IsOnGrid(end))
            {
                errors.TryAdd("endTime", "Время окончания должно быть кратно 30 минутам");
            }

            if (!SchedulePolicy.IsWithinWorkingDay(start))
            {
                errors.TryAdd("startTime", "Время начала должно быть в диапазоне 09:00–18:00");
            }

            if (!SchedulePolicy.IsWithinWorkingDay(end))
            {
                errors.TryAdd("endTime", "Время окончания должно быть в диапазоне 09:00–18:00");
            }
        }

        if (errors.Count > 0)
        {
            throw ApiException.Validation(errors);
        }

        return (parsedDate, start, end);
    }

    /// <summary>Проверяет дату из query-параметра: обязательна, формат <c>YYYY-MM-DD</c>, не в прошлом.</summary>
    public static DateOnly ParseDate(DateOnly today, string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            throw ApiException.Validation("date", "Параметр date обязателен (YYYY-MM-DD)");
        }

        if (!ApiParsing.TryParseDate(date, out var parsed))
        {
            throw ApiException.Validation("date", "Неверный формат даты, ожидается YYYY-MM-DD");
        }

        if (parsed < today)
        {
            throw ApiException.Validation("date", "Дата не может быть в прошлом");
        }

        return parsed;
    }
}
