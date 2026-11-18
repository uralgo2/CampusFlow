using CampusFlow.Application.Errors;
using CampusFlow.Application.Services;
using CampusFlow.Contracts.Bookings;
using CampusFlow.Domain;
using CampusFlow.Tests.Testing;
using CampusFlow.Tests.Testing.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using static CampusFlow.Tests.Testing.Assert;

namespace CampusFlow.Tests.Application;

/// <summary>Бронирование: конфликты, личный кабинет, отмена, админ-операции и CSV.</summary>
public sealed class BookingServiceTests
{
    [Test]
    public async Task Create_ValidBooking_ReturnsActiveBooking()
    {
        var fixture = CreateFixture();

        var booking = await fixture.Service.CreateAsync(
            Request(fixture.Data.A101, "14:00", "15:00", "  Подготовка к семинару  "),
            CancellationToken.None);

        True(booking.Id > 0, "бронь должна получить id");
        Equal(fixture.Data.A101.Id, booking.RoomId);
        Equal("A-101", booking.RoomName);
        Equal(1, booking.UserId);
        Equal("Иван Студентов", booking.UserDisplayName);
        Equal(Fixtures.Tomorrow(), booking.Date);
        Equal("14:00", booking.StartTime);
        Equal("15:00", booking.EndTime);
        Equal("active", booking.Status);
        Equal("Подготовка к семинару", booking.Comment, "комментарий должен обрезаться по краям");
        Equal(1, fixture.Data.Db.Bookings.Count);
    }

    [Test]
    public async Task Create_UsesUserIdFromCurrentUser()
    {
        var fixture = CreateFixture();
        fixture.User.UserId = 2;
        fixture.User.Login = "student2";
        fixture.User.DisplayName = "Мария Студентова";

        var booking = await fixture.Service.CreateAsync(
            Request(fixture.Data.A101, "14:00", "15:00"),
            CancellationToken.None);

        Equal(2, booking.UserId, "автор берётся из токена, а не из тела запроса");
        Equal("Мария Студентова", booking.UserDisplayName);
    }

    [Test]
    public async Task Create_OverlappingBooking_ThrowsConflictWithDetails()
    {
        var fixture = CreateFixture();
        await fixture.Service.CreateAsync(Request(fixture.Data.A101, "14:00", "15:30"), CancellationToken.None);

        var exception = await ThrowsApiErrorAsync(
            () => fixture.Service.CreateAsync(Request(fixture.Data.A101, "14:30", "15:00"), CancellationToken.None),
            409,
            ErrorCodes.BookingConflict);

        Detail(exception.Details, "roomId", fixture.Data.A101.Id);
        Detail(exception.Details, "date", Fixtures.Tomorrow());
        Detail(exception.Details, "startTime", "14:30");
        Detail(exception.Details, "endTime", "15:00");
        Equal("Аудитория уже забронирована на это время", exception.Message);
        Equal(1, fixture.Data.Db.Bookings.Count, "конфликтующая бронь не должна сохраняться");
    }

    [Test]
    public async Task Create_TouchingSlots_IsAllowed()
    {
        var fixture = CreateFixture();

        await fixture.Service.CreateAsync(Request(fixture.Data.A101, "14:00", "15:00"), CancellationToken.None);
        await fixture.Service.CreateAsync(Request(fixture.Data.A101, "15:00", "16:00"), CancellationToken.None);
        await fixture.Service.CreateAsync(Request(fixture.Data.A101, "13:00", "14:00"), CancellationToken.None);

        Equal(3, fixture.Data.Db.Bookings.Count, "касание границ не конфликт");
    }

    [Test]
    public async Task Create_SameSlotInAnotherRoom_IsAllowed()
    {
        var fixture = CreateFixture();

        await fixture.Service.CreateAsync(Request(fixture.Data.A101, "14:00", "15:00"), CancellationToken.None);
        await fixture.Service.CreateAsync(Request(fixture.Data.B101, "14:00", "15:00"), CancellationToken.None);

        Equal(2, fixture.Data.Db.Bookings.Count);
    }

    [Test]
    public async Task Create_CancelledBookingDoesNotBlockSlot()
    {
        var fixture = CreateFixture();
        fixture.Data.Db.AddBooking(
            fixture.Data.A101.Id,
            1,
            Fixtures.Today.AddDays(1),
            "14:00",
            "15:00",
            BookingStatus.Cancelled);

        var booking = await fixture.Service.CreateAsync(
            Request(fixture.Data.A101, "14:00", "15:00"),
            CancellationToken.None);

        Equal("active", booking.Status);
    }

    [Test]
    public async Task Create_MissingRoom_ThrowsRoomNotFound()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(
            () => fixture.Service.CreateAsync(Request(999, "14:00", "15:00"), CancellationToken.None),
            404,
            ErrorCodes.RoomNotFound);
    }

    [Test]
    public async Task Create_PastDate_ThrowsValidationError()
    {
        var fixture = CreateFixture();
        var yesterday = Fixtures.Iso(Fixtures.Today.AddDays(-1));

        await ThrowsApiErrorAsync(
            () => fixture.Service.CreateAsync(
                new CreateBookingRequest(fixture.Data.A101.Id, yesterday, "14:00", "15:00", null),
                CancellationToken.None),
            400,
            ErrorCodes.Validation);
    }

    [Test]
    public async Task Create_TooLongComment_ThrowsValidationError()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(
            () => fixture.Service.CreateAsync(
                Request(fixture.Data.A101, "14:00", "15:00", new string('x', 501)),
                CancellationToken.None),
            400,
            ErrorCodes.Validation);
    }

    [Test]
    public async Task Create_AnonymousUser_ThrowsUnauthorized()
    {
        var fixture = CreateFixture();
        fixture.User.UserId = null;

        await ThrowsApiErrorAsync(
            () => fixture.Service.CreateAsync(Request(fixture.Data.A101, "14:00", "15:00"), CancellationToken.None),
            401,
            ErrorCodes.Unauthorized);
    }

    [Test]
    public async Task GetMy_ReturnsOnlyCurrentUserBookings()
    {
        var fixture = CreateFixture();
        SeedBookings(fixture);

        var mine = await fixture.Service.GetMyAsync(null, CancellationToken.None);

        Equal(2, mine.Count, "у student1 две брони");
        True(mine.All(booking => booking.UserId == 1), "в списке только брони текущего пользователя");
    }

    [Test]
    public async Task GetMy_FiltersByStatus()
    {
        var fixture = CreateFixture();
        SeedBookings(fixture);

        var active = await fixture.Service.GetMyAsync("active", CancellationToken.None);
        var cancelled = await fixture.Service.GetMyAsync("cancelled", CancellationToken.None);

        Equal(1, active.Count);
        Equal("active", active[0].Status);
        Equal(1, cancelled.Count);
        Equal("cancelled", cancelled[0].Status);
    }

    [Test]
    public async Task GetMy_InvalidStatus_ThrowsValidationError()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(
            () => fixture.Service.GetMyAsync("deleted", CancellationToken.None),
            400,
            ErrorCodes.Validation);
    }

    [Test]
    public async Task CancelOwn_MarksBookingCancelled()
    {
        var fixture = CreateFixture();
        var created =
            await fixture.Service.CreateAsync(Request(fixture.Data.A101, "14:00", "15:00"), CancellationToken.None);

        var result = await fixture.Service.CancelOwnAsync(created.Id, CancellationToken.None);

        Equal(created.Id, result.Id);
        Equal("cancelled", result.Status);
        Equal(BookingStatus.Cancelled, fixture.Data.Db.Bookings.Single().Status);
    }

    [Test]
    public async Task CancelOwn_ForeignBooking_ThrowsForbidden()
    {
        var fixture = CreateFixture();
        var created =
            await fixture.Service.CreateAsync(Request(fixture.Data.A101, "14:00", "15:00"), CancellationToken.None);
        fixture.User.UserId = 2;
        fixture.User.Login = "student2";

        await ThrowsApiErrorAsync(
            () => fixture.Service.CancelOwnAsync(created.Id, CancellationToken.None),
            403,
            ErrorCodes.Forbidden);

        Equal(BookingStatus.Active, fixture.Data.Db.Bookings.Single().Status, "бронь не должна меняться");
    }

    [Test]
    public async Task CancelOwn_AlreadyCancelled_ThrowsConflict()
    {
        var fixture = CreateFixture();
        var created =
            await fixture.Service.CreateAsync(Request(fixture.Data.A101, "14:00", "15:00"), CancellationToken.None);
        await fixture.Service.CancelOwnAsync(created.Id, CancellationToken.None);

        await ThrowsApiErrorAsync(
            () => fixture.Service.CancelOwnAsync(created.Id, CancellationToken.None),
            409,
            ErrorCodes.AlreadyCancelled);
    }

    [Test]
    public async Task CancelOwn_MissingBooking_ThrowsNotFound()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(
            () => fixture.Service.CancelOwnAsync(9999, CancellationToken.None),
            404,
            ErrorCodes.BookingNotFound);
    }

    [Test]
    public async Task AdminCancel_CancelsAnyBooking()
    {
        var fixture = CreateFixture();
        var created =
            await fixture.Service.CreateAsync(Request(fixture.Data.A101, "14:00", "15:00"), CancellationToken.None);
        fixture.User.UserId = 3;
        fixture.User.Login = "admin";
        fixture.User.Role = UserRole.Admin;

        var result = await fixture.Service.CancelByAdminAsync(created.Id, CancellationToken.None);

        Equal("cancelled", result.Status);
    }

    [Test]
    public async Task AdminList_FiltersByDateAndUser()
    {
        var fixture = CreateFixture();
        SeedBookings(fixture);

        var tomorrow = Fixtures.Today.AddDays(1);
        var all = await fixture.Service.GetForAdminAsync(null, null, null, null, CancellationToken.None);
        var filtered = await fixture.Service.GetForAdminAsync(
            Fixtures.Iso(tomorrow),
            null,
            2,
            "active",
            CancellationToken.None);

        Equal(3, all.Count, "админ видит все брони");
        Equal(1, filtered.Count, "фильтр по дате, автору и статусу");
        Equal(2, filtered[0].UserId);
    }

    [Test]
    public async Task AdminList_InvalidStatus_ThrowsValidationError()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(
            () => fixture.Service.GetForAdminAsync(null, null, null, "pending", CancellationToken.None),
            400,
            ErrorCodes.Validation);
    }

    [Test]
    public async Task ExportCsv_ContainsHeaderAndRows()
    {
        var fixture = CreateFixture();
        SeedBookings(fixture);

        var csv = await fixture.Service.ExportCsvAsync(null, null, null, null, CancellationToken.None);

        Contains("id,roomId,roomName,userId,userDisplayName,date,startTime,endTime,status,comment", csv.Content,
            "нет заголовка CSV");
        Contains("A-101", csv.Content, "нет строки с аудиторией");
        Contains("Иван Студентов", csv.Content, "нет имени автора");
        True(csv.FileName.EndsWith(".csv", StringComparison.Ordinal), "имя файла должно заканчиваться на .csv");
    }

    private static void SeedBookings(BookingFixture fixture)
    {
        var tomorrow = Fixtures.Today.AddDays(1);

        fixture.Data.Db.AddBooking(fixture.Data.A101.Id, 1, tomorrow, "10:00", "11:00", BookingStatus.Active,
            "Консультация");
        fixture.Data.Db.AddBooking(fixture.Data.A101.Id, 1, tomorrow, "12:00", "13:00", BookingStatus.Cancelled,
            "Отменили");
        fixture.Data.Db.AddBooking(fixture.Data.B101.Id, 2, tomorrow, "14:00", "15:00", BookingStatus.Active,
            "Собрание");
    }

    private static CreateBookingRequest Request(Room room, string startTime, string endTime, string? comment = null)
    {
        return new CreateBookingRequest(room.Id, Fixtures.Tomorrow(), startTime, endTime, comment);
    }

    private static CreateBookingRequest Request(int roomId, string startTime, string endTime)
    {
        return new CreateBookingRequest(roomId, Fixtures.Tomorrow(), startTime, endTime, null);
    }

    private static BookingFixture CreateFixture()
    {
        var data = Fixtures.Standard();
        var clock = Fixtures.Clock();
        var user = FakeCurrentUser.Student();
        var bookings = new InMemoryBookingRepository(data.Db);
        var rooms = new InMemoryRoomRepository(data.Db);
        var service = new BookingService(
            bookings,
            rooms,
            user,
            clock,
            NullLogger<BookingService>.Instance);

        return new BookingFixture(service, data, user, clock);
    }

    private sealed record BookingFixture(
        BookingService Service,
        Fixtures.StandardData Data,
        FakeCurrentUser User,
        FakeClock Clock);
}