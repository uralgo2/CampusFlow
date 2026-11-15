using CampusFlow.Domain;
using CampusFlow.Tests.Testing;
using static CampusFlow.Tests.Testing.Assert;

namespace CampusFlow.Tests.Domain;

/// <summary>Правила рабочего дня и расчёт свободных интервалов.</summary>
public sealed class SchedulePolicyTests
{
    [Test]
    public void FreeSlots_NoBookings_ReturnsWholeWorkingDay()
    {
        var slots = SchedulePolicy.FreeSlots(Array.Empty<(TimeOnly Start, TimeOnly End)>());

        Equal(1, slots.Count);
        Equal(new TimeOnly(9, 0), slots[0].Start);
        Equal(new TimeOnly(18, 0), slots[0].End);
    }

    [Test]
    public void FreeSlots_BookingInMiddle_ReturnsTwoIntervals()
    {
        var slots = SchedulePolicy.FreeSlots(new[]
        {
            (new TimeOnly(12, 0), new TimeOnly(14, 0))
        });

        Equal(2, slots.Count);
        Equal(new TimeOnly(9, 0), slots[0].Start);
        Equal(new TimeOnly(12, 0), slots[0].End);
        Equal(new TimeOnly(14, 0), slots[1].Start);
        Equal(new TimeOnly(18, 0), slots[1].End);
    }

    [Test]
    public void FreeSlots_BookingAtDayStart_DoesNotCreateEmptySlot()
    {
        var slots = SchedulePolicy.FreeSlots(new[]
        {
            (new TimeOnly(9, 0), new TimeOnly(10, 0))
        });

        Equal(1, slots.Count);
        Equal(new TimeOnly(10, 0), slots[0].Start);
    }

    [Test]
    public void FreeSlots_BookingAtDayEnd_DoesNotCreateEmptySlot()
    {
        var slots = SchedulePolicy.FreeSlots(new[]
        {
            (new TimeOnly(17, 0), new TimeOnly(18, 0))
        });

        Equal(1, slots.Count);
        Equal(new TimeOnly(17, 0), slots[0].End);
    }

    [Test]
    public void FreeSlots_OverlappingBookings_AreMerged()
    {
        var slots = SchedulePolicy.FreeSlots(new[]
        {
            (new TimeOnly(10, 0), new TimeOnly(12, 0)),
            (new TimeOnly(11, 0), new TimeOnly(13, 0))
        });

        Equal(2, slots.Count);
        Equal(new TimeOnly(9, 0), slots[0].Start);
        Equal(new TimeOnly(10, 0), slots[0].End);
        Equal(new TimeOnly(13, 0), slots[1].Start);
        Equal(new TimeOnly(18, 0), slots[1].End);
    }

    [Test]
    public void FreeSlots_FullDayBooked_ReturnsEmptyList()
    {
        var slots = SchedulePolicy.FreeSlots(new[]
        {
            (new TimeOnly(9, 0), new TimeOnly(18, 0))
        });

        Empty(slots);
    }

    [Test]
    public void FreeSlots_BookingsOutsideWorkingDay_AreClipped()
    {
        var slots = SchedulePolicy.FreeSlots(new[]
        {
            (new TimeOnly(7, 0), new TimeOnly(10, 0)),
            (new TimeOnly(16, 0), new TimeOnly(20, 0))
        });

        Equal(1, slots.Count);
        Equal(new TimeOnly(10, 0), slots[0].Start);
        Equal(new TimeOnly(16, 0), slots[0].End);
    }

    [Test]
    public void FreeSlots_UnsortedInput_IsHandled()
    {
        var slots = SchedulePolicy.FreeSlots(new[]
        {
            (new TimeOnly(15, 0), new TimeOnly(16, 0)),
            (new TimeOnly(10, 0), new TimeOnly(11, 0))
        });

        Equal(3, slots.Count);
        Equal(new TimeOnly(9, 0), slots[0].Start);
        Equal(new TimeOnly(16, 0), slots[2].Start);
    }

    [Test]
    public void IsOnGrid_AcceptsHalfHourStepsOnly()
    {
        True(SchedulePolicy.IsOnGrid(new TimeOnly(9, 0)), "09:00 должно быть на сетке");
        True(SchedulePolicy.IsOnGrid(new TimeOnly(14, 30)), "14:30 должно быть на сетке");
        True(SchedulePolicy.IsOnGrid(new TimeOnly(18, 0)), "18:00 должно быть на сетке");
        False(SchedulePolicy.IsOnGrid(new TimeOnly(14, 10)), "14:10 не кратно 30 минутам");
        False(SchedulePolicy.IsOnGrid(new TimeOnly(14, 0, 30)), "секунды не допускаются");
    }

    [Test]
    public void IsWithinWorkingDay_UsesInclusiveBounds()
    {
        True(SchedulePolicy.IsWithinWorkingDay(new TimeOnly(9, 0)), "09:00 входит в рабочий день");
        True(SchedulePolicy.IsWithinWorkingDay(new TimeOnly(18, 0)), "18:00 входит в рабочий день");
        False(SchedulePolicy.IsWithinWorkingDay(new TimeOnly(8, 30)), "08:30 вне рабочего дня");
        False(SchedulePolicy.IsWithinWorkingDay(new TimeOnly(18, 30)), "18:30 вне рабочего дня");
    }

    [Test]
    public void WorkingDayMinutes_IsNineHours()
    {
        Equal(540, SchedulePolicy.WorkingDayMinutes);
    }
}
