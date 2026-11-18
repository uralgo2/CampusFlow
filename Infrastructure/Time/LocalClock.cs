using CampusFlow.Application.Abstractions;

namespace CampusFlow.Infrastructure.Time;

/// <inheritdoc />
public sealed class LocalClock : IClock
{
    /// <inheritdoc />
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}