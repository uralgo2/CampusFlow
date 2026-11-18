using CampusFlow.Application.Errors;
using CampusFlow.Application.Services;
using CampusFlow.Contracts.Rooms;
using CampusFlow.Domain;
using CampusFlow.Tests.Testing;
using CampusFlow.Tests.Testing.Fakes;
using static CampusFlow.Tests.Testing.Assert;

namespace CampusFlow.Tests.Application;

/// <summary>Справочник аудиторий, свободные слоты и  поиск свободных аудиторий.</summary>
public sealed class RoomServiceTests
{
    [Test]
    public async Task GetRooms_FiltersByBuildingCapacityAndEquipment()
    {
        var fixture = CreateFixture();

        var rooms = await fixture.Service.GetRoomsAsync("A", 20, "projector", CancellationToken.None);

        Equal(1, rooms.Count);
        Equal("A-101", rooms[0].Name);
    }

    [Test]
    public async Task GetRooms_WithoutFilters_ReturnsAllOrderedByBuildingAndName()
    {
        var fixture = CreateFixture();

        var rooms = await fixture.Service.GetRoomsAsync(null, null, null, CancellationToken.None);

        Equal(3, rooms.Count);
        SequenceEqual(new[] { "A-101", "A-102", "B-101" }, rooms.Select(room => room.Name));
    }

    [Test]
    public async Task GetRooms_InvalidFilters_ThrowValidationError()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(() => fixture.Service.GetRoomsAsync("Z", null, null, CancellationToken.None), 400,
            ErrorCodes.Validation);
        await ThrowsApiErrorAsync(() => fixture.Service.GetRoomsAsync(null, 0, null, CancellationToken.None), 400,
            ErrorCodes.Validation);
        await ThrowsApiErrorAsync(() => fixture.Service.GetRoomsAsync(null, -1, null, CancellationToken.None), 400,
            ErrorCodes.Validation);
        await ThrowsApiErrorAsync(() => fixture.Service.GetRoomsAsync(null, null, "monitor", CancellationToken.None),
            400, ErrorCodes.Validation);
    }

    [Test]
    public async Task GetRoom_MissingRoom_ThrowsNotFound()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(() => fixture.Service.GetRoomAsync(999, CancellationToken.None), 404,
            ErrorCodes.RoomNotFound);
    }

    [Test]
    public async Task GetFreeSlots_UsesOnlyActiveBookings()
    {
        var fixture = CreateFixture();
        var tomorrow = Fixtures.Today.AddDays(1);

        fixture.Data.Db.AddBooking(fixture.Data.A101.Id, 1, tomorrow, "10:00", "11:30");
        fixture.Data.Db.AddBooking(fixture.Data.A101.Id, 1, tomorrow, "15:00", "16:00", BookingStatus.Cancelled);

        var response = await fixture.Service.GetFreeSlotsAsync(
            fixture.Data.A101.Id,
            Fixtures.Iso(tomorrow),
            CancellationToken.None);

        Equal(2, response.FreeSlots.Count);
        Equal("09:00", response.FreeSlots[0].StartTime);
        Equal("10:00", response.FreeSlots[0].EndTime);
        Equal("11:30", response.FreeSlots[1].StartTime);
        Equal("18:00", response.FreeSlots[1].EndTime);
        Equal(Fixtures.Iso(tomorrow), response.Date);
    }

    [Test]
    public async Task GetFreeSlots_NoBookings_ReturnsWholeDay()
    {
        var fixture = CreateFixture();

        var response = await fixture.Service.GetFreeSlotsAsync(
            fixture.Data.A101.Id,
            Fixtures.Tomorrow(),
            CancellationToken.None);

        Equal(1, response.FreeSlots.Count);
        Equal("09:00", response.FreeSlots[0].StartTime);
        Equal("18:00", response.FreeSlots[0].EndTime);
    }

    [Test]
    public async Task GetFreeSlots_PastDate_ThrowsValidationError()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(
            () => fixture.Service.GetFreeSlotsAsync(
                fixture.Data.A101.Id,
                Fixtures.Iso(Fixtures.Today.AddDays(-1)),
                CancellationToken.None),
            400,
            ErrorCodes.Validation);
    }

    [Test]
    public async Task GetFreeSlots_MissingRoom_ThrowsNotFound()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(
            () => fixture.Service.GetFreeSlotsAsync(999, Fixtures.Tomorrow(), CancellationToken.None),
            404,
            ErrorCodes.RoomNotFound);
    }

    [Test]
    public async Task CreateRoom_ValidRequest_AddsRoom()
    {
        var fixture = CreateFixture();

        var room = await fixture.Service.CreateRoomAsync(
            new CreateRoomRequest("D-201", "d", 2, 25, new[] { "projector", "whiteboard" }),
            CancellationToken.None);

        True(room.Id > 0, "аудитория должна получить id");
        Equal("D-201", room.Name);
        Equal("D", room.Building);
        Equal(2, room.Floor);
        Equal(25, room.Capacity);
        SequenceEqual(new[] { "projector", "whiteboard" }, room.Equipment);
        Equal(4, fixture.Data.Db.Rooms.Count);
    }

    [Test]
    public async Task CreateRoom_DuplicateName_ThrowsValidationError()
    {
        var fixture = CreateFixture();

        var exception = await ThrowsApiErrorAsync(
            () => fixture.Service.CreateRoomAsync(
                new CreateRoomRequest("a-101", "A", 1, 10, Array.Empty<string>()),
                CancellationToken.None),
            400,
            ErrorCodes.Validation);

        Contains("name", string.Join(",", ((IDictionary<string, string>)exception.Details!).Keys));
    }

    [Test]
    public async Task UpdateRoom_ChangesFields()
    {
        var fixture = CreateFixture();

        var room = await fixture.Service.UpdateRoomAsync(
            fixture.Data.A101.Id,
            new UpdateRoomRequest("A-101", "A", 2, 35, new[] { "audio" }),
            CancellationToken.None);

        Equal(2, room.Floor);
        Equal(35, room.Capacity);
        SequenceEqual(new[] { "audio" }, room.Equipment);
    }

    [Test]
    public async Task UpdateRoom_MissingRoom_ThrowsNotFound()
    {
        var fixture = CreateFixture();

        await ThrowsApiErrorAsync(
            () => fixture.Service.UpdateRoomAsync(999, new UpdateRoomRequest("X-1", "A", 1, 10, Array.Empty<string>()),
                CancellationToken.None),
            404,
            ErrorCodes.RoomNotFound);
    }

    [Test]
    public async Task DeleteRoom_WithActiveBookings_ThrowsRoomInUse()
    {
        var fixture = CreateFixture();
        fixture.Data.Db.AddBooking(
            fixture.Data.A101.Id,
            1,
            Fixtures.Today.AddDays(1),
            "10:00",
            "11:00");

        await ThrowsApiErrorAsync(
            () => fixture.Service.DeleteRoomAsync(fixture.Data.A101.Id, CancellationToken.None),
            409,
            ErrorCodes.RoomInUse);
    }

    [Test]
    public async Task DeleteRoom_WithoutActiveBookings_RemovesRoom()
    {
        var fixture = CreateFixture();
        fixture.Data.Db.AddBooking(
            fixture.Data.A101.Id,
            1,
            Fixtures.Today.AddDays(1),
            "10:00",
            "11:00",
            BookingStatus.Cancelled);

        await fixture.Service.DeleteRoomAsync(fixture.Data.A101.Id, CancellationToken.None);

        Equal(2, fixture.Data.Db.Rooms.Count);
        False(fixture.Data.Db.Rooms.Any(room => room.Id == fixture.Data.A101.Id), "аудитория должна быть удалена");
    }

    [Test]
    public async Task FindFreeRooms_ExcludesBusyRooms()
    {
        var fixture = CreateFixture();
        var tomorrow = Fixtures.Today.AddDays(1);

        fixture.Data.Db.AddBooking(fixture.Data.A101.Id, 1, tomorrow, "10:00", "10:30");

        var response = await fixture.Service.FindFreeRoomsAsync(
            Fixtures.Iso(tomorrow),
            "10:00",
            "11:00",
            null,
            null,
            null,
            CancellationToken.None);

        False(response.Rooms.Any(room => room.Name == "A-101"), "A-101 занята в 10:00–11:00");
        True(response.Rooms.Any(room => room.Name == "A-102"), "A-102 должна быть свободна");
        Equal("10:00", response.StartTime);
    }

    [Test]
    public async Task FindFreeRooms_AppliesRoomFilters()
    {
        var fixture = CreateFixture();

        var response = await fixture.Service.FindFreeRoomsAsync(
            Fixtures.Tomorrow(),
            "10:00",
            "11:00",
            "B",
            30,
            "projector",
            CancellationToken.None);

        Equal(1, response.Rooms.Count);
        Equal("B-101", response.Rooms[0].Name);
    }

    private static RoomFixture CreateFixture()
    {
        var data = Fixtures.Standard();
        data.Db.AddRoom("A-102", "A", 1, 20, Equipment.Whiteboard);

        var clock = Fixtures.Clock();
        var rooms = new InMemoryRoomRepository(data.Db);
        var bookings = new InMemoryBookingRepository(data.Db);
        var service = new RoomService(rooms, bookings, clock);

        return new RoomFixture(service, data);
    }

    private sealed record RoomFixture(RoomService Service, Fixtures.StandardData Data);
}