using CampusFlow.Application.Abstractions;

namespace CampusFlow.Tests.Testing.Fakes;

/// <summary>Часы, которые можно двигать в тестах.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateOnly today, DateTime utcNow)
    {
        Today = today;
        UtcNow = utcNow;
    }

    public DateOnly Today { get; set; }

    public DateTime UtcNow { get; set; }

    public void Advance(TimeSpan delta)
    {
        UtcNow = UtcNow.Add(delta);
    }
}