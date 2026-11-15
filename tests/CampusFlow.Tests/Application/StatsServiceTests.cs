using CampusFlow.Application.Errors;
using CampusFlow.Application.Services;
using CampusFlow.Domain;
using CampusFlow.Tests.Testing;
using CampusFlow.Tests.Testing.Fakes;
using static CampusFlow.Tests.Testing.Assert;

namespace CampusFlow.Tests.Application;

/// <summary>Статистика загрузки: итоги, разбивка по дням и корпусам, фильтры и валидация периода.</summary>
public sealed class StatsServiceTests
{
    [Test]
    public async Task GetStats_ComputesTotalsForDefaultWeek()
    {
        var fixture = CreateFixture();

        var stats = await fixture.Service.GetStatsAsync(null, null, null, CancellationToken.None);

        Equal(Fixtures.Iso(Fixtures.Today), stats.From);
        Equal(Fixtures.Iso(Fixtures.Today.AddDays(6)), stats.To);
        Equal(7, stats.ByDay.Count, "по умолчанию неделя");
        Equal(2, stats.Totals.ActiveBookings);
        Equal(1, stats.Totals.CancelledBookings);
        Equal(120, stats.Totals.BookedMinutes, "60 + 60 минут активных броней");
        Equal(2 * 7 * 540, stats.Totals.TotalMinutes);
        Equal(1.6, stats.Totals.UtilizationPercent);
    }

    [Test]
    public async Task GetStats_ComputesDailyAndBuildingBreakdown()
    {
        var fixture = CreateFixture();

        var stats = await fixture.Service.GetStatsAsync(null, null, null, CancellationToken.None);

        var today = stats.ByDay.Single(day => day.Date == Fixtures.Iso(Fixtures.Today));
        Equal(1, today.Load.ActiveBookings);
        Equal(60, today.Load.BookedMinutes);

        Equal(2, stats.ByBuilding.Count);
        var buildingA = stats.ByBuilding.Single(building => building.Building == "A");
        Equal(1, buildingA.Rooms);
        Equal(1, buildingA.Load.ActiveBookings);

        var buildingB = stats.ByBuilding.Single(building => building.Building == "B");
        Equal(1, buildingB.Load.ActiveBookings);
        Equal(1, buildingB.Load.CancelledBookings);
    }

    [Test]
    public async Task GetStats_FiltersByBuilding()
    {
        var fixture = CreateFixture();

        var stats = await fixture.Service.GetStatsAsync(null, null, "B", CancellationToken.None);

        Equal(1, stats.Totals.ActiveBookings);
        Equal(1, stats.Totals.CancelledBookings);
        Equal(1 * 7 * 540, stats.Totals.TotalMinutes, "в корпусе B одна аудитория");
    }

    [Test]
    public async Task GetStats_CustomPeriod_IsRespected()
    {
        var fixture = CreateFixture();
        var from = Fixtures.Iso(Fixtures.Today);
        var to = Fixtures.Iso(Fixtures.Today.AddDays(1));

        var stats = await fixture.Service.GetStatsAsync(from, to, null, CancellationToken.None);

        Equal(2, stats.ByDay.Count);
        Equal(2 * 2 * 540, stats.Totals.TotalMinutes);
    }

    [Test]
    public async Task GetStats_InvalidPeriod_ThrowsValidationError()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(
            () => fixture.Service.GetStatsAsync(Fixtures.Iso(Fixtures.Today.AddDays(5)), Fixtures.Iso(Fixtures.Today), null, CancellationToken.None),
            400,
            ErrorCodes.Validation);

        await ThrowsApiErrorAsync(
            () => fixture.Service.GetStatsAsync(Fixtures.Iso(Fixtures.Today), Fixtures.Iso(Fixtures.Today.AddDays(400)), null, CancellationToken.None),
            400,
            ErrorCodes.Validation);
    }

    [Test]
    public async Task GetStats_InvalidDatesAndBuilding_ThrowValidationError()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(
            () => fixture.Service.GetStatsAsync("14.11.2026", null, null, CancellationToken.None),
            400,
            ErrorCodes.Validation);

        await ThrowsApiErrorAsync(
            () => fixture.Service.GetStatsAsync(null, null, "Z", CancellationToken.None),
            400,
            ErrorCodes.Validation);
    }

    private static StatsFixture CreateFixture()
    {
        var clock = Fixtures.Clock();
        var db = new FakeDatabase();

        var student1 = db.AddUser(1, "student1", "Иван Студентов", UserRole.Student);
        var a101 = db.AddRoom("A-101", "A", 1, 30, Equipment.Projector);
        var b101 = db.AddRoom("B-101", "B", 1, 40, Equipment.Projector, Equipment.Audio);

        db.AddBooking(a101.Id, student1.Id, Fixtures.Today, "10:00", "11:00", BookingStatus.Active);
        db.AddBooking(b101.Id, student1.Id, Fixtures.Today, "12:00", "13:00", BookingStatus.Cancelled);
        db.AddBooking(b101.Id, student1.Id, Fixtures.Today.AddDays(1), "13:00", "14:00", BookingStatus.Active);

        var service = new StatsService(
            new InMemoryBookingRepository(db),
            new InMemoryRoomRepository(db),
            clock);

        return new StatsFixture(service);
    }

    private sealed record StatsFixture(StatsService Service);
}
