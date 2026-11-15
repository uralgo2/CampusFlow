using System.Globalization;
using System.Text;
using CampusFlow.Application.Abstractions;
using CampusFlow.Application.Errors;
using CampusFlow.Application.Mapping;
using CampusFlow.Application.Parsing;
using CampusFlow.Application.Validation;
using CampusFlow.Contracts.Bookings;
using CampusFlow.Domain;

namespace CampusFlow.Application.Services;

/// <inheritdoc />
public sealed class BookingService : IBookingService
{
    /// <summary>
    /// Проверка конфликта и вставка выполняются под этим замком, чтобы два одновременных запроса
    /// не создали пересекающиеся брони. Для MVP с SQLite этого достаточно.
    /// </summary>
    private static readonly SemaphoreSlim CreateLock = new(1, 1);

    private readonly IBookingRepository _bookings;
    private readonly IRoomRepository _rooms;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IBookingRepository bookings,
        IRoomRepository rooms,
        ICurrentUser currentUser,
        IClock clock,
        ILogger<BookingService> logger)
    {
        _bookings = bookings;
        _rooms = rooms;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BookingDto> CreateAsync(CreateBookingRequest? request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();

        if (request is null)
        {
            throw ApiException.Validation("body", "Тело запроса обязательно");
        }

        if (request.RoomId is null or <= 0)
        {
            throw ApiException.Validation("roomId", "Поле roomId обязательно и должно быть больше 0");
        }

        var roomId = request.RoomId.Value;
        var (date, start, end) = SlotParser.Parse(_clock.Today, request.Date, request.StartTime, request.EndTime);
        var comment = NormalizeComment(request.Comment);

        var room = await _rooms.GetAsync(roomId, cancellationToken)
                   ?? throw ApiException.RoomNotFound(roomId);

        Booking saved;

        await CreateLock.WaitAsync(cancellationToken);
        try
        {
            var hasConflict = await _bookings.HasConflictAsync(roomId, date, start, end, null, cancellationToken);
            if (hasConflict)
            {
                throw ApiException.BookingConflict(
                    roomId,
                    ApiParsing.FormatDate(date),
                    ApiParsing.FormatTime(start),
                    ApiParsing.FormatTime(end));
            }

            var booking = new Booking
            {
                RoomId = roomId,
                UserId = userId,
                Date = date,
                StartTime = start,
                EndTime = end,
                Status = BookingStatus.Active,
                Comment = comment
            };

            saved = await _bookings.AddAsync(booking, cancellationToken);
        }
        finally
        {
            CreateLock.Release();
        }

        _logger.LogInformation(
            "Создана бронь {BookingId}: аудитория {RoomId}, {Date} {Start}-{End}, пользователь {UserId}",
            saved.Id,
            roomId,
            ApiParsing.FormatDate(date),
            ApiParsing.FormatTime(start),
            ApiParsing.FormatTime(end),
            userId);

        // DTO собирается из уже проверенных значений, чтобы не делать лишний запрос с join.
        return new BookingDto(
            saved.Id,
            roomId,
            room.Name,
            userId,
            _currentUser.DisplayName ?? _currentUser.Login ?? string.Empty,
            ApiParsing.FormatDate(date),
            ApiParsing.FormatTime(start),
            ApiParsing.FormatTime(end),
            BookingStatuses.Active,
            comment);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookingDto>> GetMyAsync(string? status, CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var filter = ParseStatusOrNull(status);

        var bookings = await _bookings.ListForUserAsync(userId, filter, cancellationToken);
        return bookings.Select(DtoMapper.ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<CancelBookingResponse> CancelOwnAsync(int bookingId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var booking = await _bookings.GetAsync(bookingId, cancellationToken)
                      ?? throw ApiException.BookingNotFound(bookingId);

        if (booking.UserId != userId)
        {
            throw ApiException.Forbidden("Отменить можно только свою бронь");
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            throw ApiException.AlreadyCancelled(bookingId);
        }

        booking.Status = BookingStatus.Cancelled;
        await _bookings.SaveAsync(cancellationToken);

        _logger.LogInformation("Бронь {BookingId} отменена пользователем {UserId}", bookingId, userId);

        return new CancelBookingResponse(booking.Id, BookingStatuses.Cancelled);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookingDto>> GetForAdminAsync(
        string? date,
        int? roomId,
        int? userId,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = BuildAdminQuery(date, roomId, userId, status);
        var bookings = await _bookings.ListAsync(query, cancellationToken);
        return bookings.Select(DtoMapper.ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<CancelBookingResponse> CancelByAdminAsync(int bookingId, CancellationToken cancellationToken)
    {
        var booking = await _bookings.GetAsync(bookingId, cancellationToken)
                      ?? throw ApiException.BookingNotFound(bookingId);

        if (booking.Status == BookingStatus.Cancelled)
        {
            throw ApiException.AlreadyCancelled(bookingId);
        }

        booking.Status = BookingStatus.Cancelled;
        await _bookings.SaveAsync(cancellationToken);

        _logger.LogInformation(
            "Бронь {BookingId} отменена администратором {AdminId}",
            bookingId,
            _currentUser.UserId);

        return new CancelBookingResponse(booking.Id, BookingStatuses.Cancelled);
    }

    /// <inheritdoc />
    public async Task<CsvExport> ExportCsvAsync(
        string? date,
        int? roomId,
        int? userId,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = BuildAdminQuery(date, roomId, userId, status);
        var bookings = await _bookings.ListAsync(query, cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("id,roomId,roomName,userId,userDisplayName,date,startTime,endTime,status,comment");

        foreach (var booking in bookings)
        {
            var dto = DtoMapper.ToDto(booking);
            builder
                .Append(dto.Id.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(dto.RoomId.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(EscapeCsv(dto.RoomName)).Append(',')
                .Append(dto.UserId.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(EscapeCsv(dto.UserDisplayName)).Append(',')
                .Append(dto.Date).Append(',')
                .Append(dto.StartTime).Append(',')
                .Append(dto.EndTime).Append(',')
                .Append(dto.Status).Append(',')
                .Append(EscapeCsv(dto.Comment ?? string.Empty))
                .AppendLine();
        }

        var fileName = $"campusflow-bookings-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return new CsvExport(fileName, builder.ToString());
    }

    private static BookingStatus? ParseStatusOrNull(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!BookingStatuses.TryParse(status, out var parsed))
        {
            throw ApiException.Validation("status", "Допустимые значения: active, cancelled");
        }

        return parsed;
    }

    private static BookingQuery BuildAdminQuery(string? date, int? roomId, int? userId, string? status)
    {
        var errors = new Dictionary<string, string>();

        DateOnly? parsedDate = null;
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (ApiParsing.TryParseDate(date, out var value))
            {
                parsedDate = value;
            }
            else
            {
                errors["date"] = "Неверный формат даты, ожидается YYYY-MM-DD";
            }
        }

        if (roomId is <= 0)
        {
            errors["roomId"] = "Параметр roomId должен быть положительным";
        }

        if (userId is <= 0)
        {
            errors["userId"] = "Параметр userId должен быть положительным";
        }

        BookingStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (BookingStatuses.TryParse(status, out var value))
            {
                parsedStatus = value;
            }
            else
            {
                errors["status"] = "Допустимые значения: active, cancelled";
            }
        }

        if (errors.Count > 0)
        {
            throw ApiException.Validation(errors);
        }

        return new BookingQuery(parsedDate, roomId, userId, parsedStatus);
    }

    private static string? NormalizeComment(string? comment)
    {
        var normalized = ApiParsing.NormalizeOptional(comment);
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length > 500)
        {
            throw ApiException.Validation("comment", "Комментарий не должен быть длиннее 500 символов");
        }

        return normalized;
    }

    private static string EscapeCsv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
