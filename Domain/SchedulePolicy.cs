namespace CampusFlow.Domain;

/// <summary>Правила рабочего дня и сетки слотов: 09:00–18:00 с шагом 30 минут.</summary>
public static class SchedulePolicy
{
    /// <summary>Шаг сетки слотов в минутах.</summary>
    public const int GridMinutes = 30;

    /// <summary>Длина рабочего дня в минутах (540).</summary>
    public const int WorkingDayMinutes = 9 * 60;

    /// <summary>Начало рабочего дня.</summary>
    public static readonly TimeOnly DayStart = new(9, 0);

    /// <summary>Окончание рабочего дня.</summary>
    public static readonly TimeOnly DayEnd = new(18, 0);

    /// <summary>Время лежит на сетке: минуты кратны 30, секунд и долей секунды нет.</summary>
    public static bool IsOnGrid(TimeOnly time)
    {
        return time.Second == 0 && time.Millisecond == 0 && time.Minute % GridMinutes == 0;
    }

    /// <summary>Время попадает в рабочий день включительно: 09:00 ≤ t ≤ 18:00.</summary>
    public static bool IsWithinWorkingDay(TimeOnly time)
    {
        return time >= DayStart && time <= DayEnd;
    }

    /// <summary>
    ///     Максимальные непрерывные свободные интервалы внутри рабочего дня.
    ///     Занятые интервалы не обязаны быть отсортированы; пересекающиеся и выходящие за день обрезаются.
    /// </summary>
    public static IReadOnlyList<(TimeOnly Start, TimeOnly End)> FreeSlots(
        IEnumerable<(TimeOnly Start, TimeOnly End)> busy)
    {
        ArgumentNullException.ThrowIfNull(busy);

        var clamped = busy
            .Select(slot => (Start: slot.Start < DayStart ? DayStart : slot.Start,
                End: slot.End > DayEnd ? DayEnd : slot.End))
            .Where(slot => slot.Start < slot.End)
            .OrderBy(slot => slot.Start)
            .ThenBy(slot => slot.End)
            .ToList();

        var free = new List<(TimeOnly Start, TimeOnly End)>();
        var cursor = DayStart;

        foreach (var slot in clamped)
        {
            if (slot.Start > cursor) free.Add((cursor, slot.Start));

            if (slot.End > cursor) cursor = slot.End;
        }

        if (cursor < DayEnd) free.Add((cursor, DayEnd));

        return free;
    }
}