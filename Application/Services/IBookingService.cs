using CampusFlow.Contracts.Bookings;

namespace CampusFlow.Application.Services;

/// <summary>Брони: создание, личный кабинет, отмена, админ-операции и экспорт.</summary>
public interface IBookingService
{
    /// <summary>Создаёт бронь от имени текущего пользователя с проверкой конфликта.</summary>
    Task<BookingDto> CreateAsync(CreateBookingRequest? request, CancellationToken cancellationToken);

    /// <summary>Брони текущего пользователя с необязательным фильтром по статусу.</summary>
    Task<IReadOnlyList<BookingDto>> GetMyAsync(string? status, CancellationToken cancellationToken);

    /// <summary>Отмена собственной брони; чужая бронь — 403.</summary>
    Task<CancelBookingResponse> CancelOwnAsync(int bookingId, CancellationToken cancellationToken);

    /// <summary>Все брони для администратора с фильтрами.</summary>
    Task<IReadOnlyList<BookingDto>> GetForAdminAsync(
        string? date,
        int? roomId,
        int? userId,
        string? status,
        CancellationToken cancellationToken);

    /// <summary>Отмена любой брони администратором.</summary>
    Task<CancelBookingResponse> CancelByAdminAsync(int bookingId, CancellationToken cancellationToken);

    /// <summary>Экспорт броней в CSV с теми же фильтрами, что и админ-список.</summary>
    Task<CsvExport> ExportCsvAsync(
        string? date,
        int? roomId,
        int? userId,
        string? status,
        CancellationToken cancellationToken);
}