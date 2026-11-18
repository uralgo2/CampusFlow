using CampusFlow.Application.Abstractions;
using CampusFlow.Application.Errors;
using CampusFlow.Application.Mapping;
using CampusFlow.Application.Parsing;
using CampusFlow.Application.Validation;
using CampusFlow.Contracts.Rooms;
using CampusFlow.Domain;

namespace CampusFlow.Application.Services;

/// <inheritdoc />
public sealed class RoomService : IRoomService
{
    private readonly IBookingRepository _bookings;
    private readonly IClock _clock;
    private readonly IRoomRepository _rooms;

    public RoomService(IRoomRepository rooms, IBookingRepository bookings, IClock clock)
    {
        _rooms = rooms;
        _bookings = bookings;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoomDto>> GetRoomsAsync(
        string? building,
        int? minCapacity,
        string? equipment,
        CancellationToken cancellationToken)
    {
        var query = ParseRoomQuery(building, minCapacity, equipment);
        var rooms = await _rooms.ListAsync(query, cancellationToken);
        return rooms.Select(DtoMapper.ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<RoomDto> GetRoomAsync(int roomId, CancellationToken cancellationToken)
    {
        var room = await _rooms.GetAsync(roomId, cancellationToken)
                   ?? throw ApiException.RoomNotFound(roomId);
        return DtoMapper.ToDto(room);
    }

    /// <inheritdoc />
    public async Task<FreeSlotsResponse> GetFreeSlotsAsync(int roomId, string? date,
        CancellationToken cancellationToken)
    {
        var room = await _rooms.GetAsync(roomId, cancellationToken)
                   ?? throw ApiException.RoomNotFound(roomId);
        var parsedDate = SlotParser.ParseDate(_clock.Today, date);

        var bookings = await _bookings.ListForRoomAndDateAsync(roomId, parsedDate, true, cancellationToken);
        var slots = SchedulePolicy.FreeSlots(bookings.Select(booking => (booking.StartTime, booking.EndTime)))
            .Select(DtoMapper.ToDto)
            .ToList();

        return new FreeSlotsResponse(room.Id, ApiParsing.FormatDate(parsedDate), slots);
    }

    /// <inheritdoc />
    public async Task<FreeRoomsResponse> FindFreeRoomsAsync(
        string? date,
        string? startTime,
        string? endTime,
        string? building,
        int? minCapacity,
        string? equipment,
        CancellationToken cancellationToken)
    {
        var (parsedDate, start, end) = SlotParser.Parse(_clock.Today, date, startTime, endTime);
        var query = ParseRoomQuery(building, minCapacity, equipment);

        var candidates = await _rooms.ListAsync(query, cancellationToken);
        var free = new List<RoomDto>();

        foreach (var room in candidates)
        {
            var bookings = await _bookings.ListForRoomAndDateAsync(room.Id, parsedDate, true, cancellationToken);
            if (!bookings.Any(booking => booking.Overlaps(start, end))) free.Add(DtoMapper.ToDto(room));
        }

        return new FreeRoomsResponse(
            ApiParsing.FormatDate(parsedDate),
            ApiParsing.FormatTime(start),
            ApiParsing.FormatTime(end),
            free);
    }

    /// <inheritdoc />
    public async Task<RoomDto> CreateRoomAsync(CreateRoomRequest? request, CancellationToken cancellationToken)
    {
        if (request is null) throw ApiException.Validation("body", "Тело запроса обязательно");

        var values = RoomRequestValidator.Validate(
            request.Name,
            request.Building,
            request.Floor,
            request.Capacity,
            request.Equipment);

        await EnsureNameIsFreeAsync(values.Name, null, cancellationToken);

        var room = new Room
        {
            Name = values.Name,
            Building = values.Building,
            Floor = values.Floor,
            Capacity = values.Capacity,
            Equipment = values.Equipment
        };

        await _rooms.AddAsync(room, cancellationToken);
        await _rooms.SaveAsync(cancellationToken);

        return DtoMapper.ToDto(room);
    }

    /// <inheritdoc />
    public async Task<RoomDto> UpdateRoomAsync(int roomId, UpdateRoomRequest? request,
        CancellationToken cancellationToken)
    {
        var room = await _rooms.GetAsync(roomId, cancellationToken)
                   ?? throw ApiException.RoomNotFound(roomId);

        if (request is null) throw ApiException.Validation("body", "Тело запроса обязательно");

        var values = RoomRequestValidator.Validate(
            request.Name,
            request.Building,
            request.Floor,
            request.Capacity,
            request.Equipment);

        await EnsureNameIsFreeAsync(values.Name, roomId, cancellationToken);

        room.Name = values.Name;
        room.Building = values.Building;
        room.Floor = values.Floor;
        room.Capacity = values.Capacity;
        room.Equipment = values.Equipment;

        await _rooms.SaveAsync(cancellationToken);

        return DtoMapper.ToDto(room);
    }

    /// <inheritdoc />
    public async Task DeleteRoomAsync(int roomId, CancellationToken cancellationToken)
    {
        var room = await _rooms.GetAsync(roomId, cancellationToken)
                   ?? throw ApiException.RoomNotFound(roomId);

        var activeBookings = await _bookings.CountActiveForRoomAsync(roomId, cancellationToken);
        if (activeBookings > 0) throw ApiException.RoomInUse(roomId, activeBookings);

        await _rooms.DeleteAsync(room, cancellationToken);
    }

    private async Task EnsureNameIsFreeAsync(string name, int? excludeRoomId, CancellationToken cancellationToken)
    {
        if (await _rooms.NameExistsAsync(name, excludeRoomId, cancellationToken))
            throw ApiException.Validation("name", "Аудитория с таким названием уже существует");
    }

    private static RoomQuery ParseRoomQuery(string? building, int? minCapacity, string? equipment)
    {
        var errors = new Dictionary<string, string>();

        string? normalizedBuilding = null;
        if (!string.IsNullOrWhiteSpace(building) && !Buildings.TryNormalize(building, out normalizedBuilding))
            errors["building"] = "Допустимые значения: A, B, C, D";

        if (minCapacity is not null && minCapacity <= 0)
            errors["minCapacity"] = "Параметр minCapacity должен быть целым числом больше 0";

        var equipmentList = new List<Equipment>();
        if (!string.IsNullOrWhiteSpace(equipment))
        {
            var unknown = new List<string>();
            foreach (var code in equipment.Split(',',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (EquipmentCatalog.TryParse(code, out var parsed))
                {
                    if (!equipmentList.Contains(parsed)) equipmentList.Add(parsed);
                }
                else
                {
                    unknown.Add(code);
                }

            if (unknown.Count > 0)
                errors["equipment"] =
                    $"Неизвестное оборудование: {string.Join(", ", unknown)}. Допустимые значения: {string.Join(", ", EquipmentCatalog.Codes)}";
        }

        if (errors.Count > 0) throw ApiException.Validation(errors);

        return new RoomQuery(normalizedBuilding, minCapacity, equipmentList);
    }
}