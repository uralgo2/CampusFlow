using System.Globalization;
using CampusFlow.Domain;
using CampusFlow.Tests.Testing;
using static CampusFlow.Tests.Testing.Assert;

namespace CampusFlow.Tests.Domain;

/// <summary>Правило пересечения интервалов: полуоткрытый интервал [start, end), касание границ не конфликт.</summary>
public sealed class BookingTests
{
    private static Booking Create(string start, string end, BookingStatus status = BookingStatus.Active) => new()
    {
        Id = 1,
        RoomId = 1,
        UserId = 1,
        Date = Fixtures.Today,
        StartTime = TimeOnly.Parse(start, CultureInfo.InvariantCulture),
        EndTime = TimeOnly.Parse(end, CultureInfo.InvariantCulture),
        Status = status
    };

    [Test]
    public void Overlaps_PartialOverlapAtStart_IsConflict()
    {
        True(Create("14:00", "15:30").Overlaps(new TimeOnly(13, 0), new TimeOnly(14, 30)), "13:00–14:30 пересекается с 14:00–15:30");
    }

    [Test]
    public void Overlaps_PartialOverlapAtEnd_IsConflict()
    {
        True(Create("14:00", "15:30").Overlaps(new TimeOnly(15, 0), new TimeOnly(16, 0)), "15:00–16:00 пересекается с 14:00–15:30");
    }

    [Test]
    public void Overlaps_NewIntervalInsideBooking_IsConflict()
    {
        True(Create("14:00", "16:00").Overlaps(new TimeOnly(14, 30), new TimeOnly(15, 0)), "интервал внутри брони");
    }

    [Test]
    public void Overlaps_NewIntervalCoversBooking_IsConflict()
    {
        True(Create("14:00", "15:00").Overlaps(new TimeOnly(13, 0), new TimeOnly(16, 0)), "интервал покрывает бронь");
    }

    [Test]
    public void Overlaps_TouchingStart_IsNotConflict()
    {
        False(Create("14:00", "15:30").Overlaps(new TimeOnly(15, 30), new TimeOnly(16, 0)), "15:30 конец и 15:30 начало не конфликт");
    }

    [Test]
    public void Overlaps_TouchingEnd_IsNotConflict()
    {
        False(Create("14:00", "15:30").Overlaps(new TimeOnly(13, 0), new TimeOnly(14, 0)), "14:00 конец и 14:00 начало не конфликт");
    }

    [Test]
    public void Overlaps_CancelledBooking_IsNotConflict()
    {
        False(Create("14:00", "15:30", BookingStatus.Cancelled).Overlaps(new TimeOnly(14, 0), new TimeOnly(15, 0)), "отменённая бронь не занимает слот");
    }
}
