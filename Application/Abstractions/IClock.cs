namespace CampusFlow.Application.Abstractions;

/// <summary>Источник текущего времени (подменяется в загрузочных сценариях и тестах).</summary>
public interface IClock
{
    /// <summary>Сегодняшняя дата по локальному времени сервера.</summary>
    DateOnly Today { get; }

    /// <summary>Текущий момент в UTC.</summary>
    DateTime UtcNow { get; }
}