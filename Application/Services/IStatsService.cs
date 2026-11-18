using CampusFlow.Contracts.Admin;

namespace CampusFlow.Application.Services;

/// <summary>Простая статистика загрузки для администратора</summary>
public interface IStatsService
{
    /// <summary>
    ///     Загрузка по дням, корпусам и аудиториям за период. По умолчанию — ближайшая неделя, начиная с сегодня.
    /// </summary>
    Task<AdminStatsResponse> GetStatsAsync(string? from, string? to, string? building,
        CancellationToken cancellationToken);
}