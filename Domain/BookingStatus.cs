namespace CampusFlow.Domain;

/// <summary>Статус брони.</summary>
public enum BookingStatus
{
    /// <summary>Действующая бронь: занимает слот.</summary>
    Active,

    /// <summary>Отменённая бронь: слот снова свободен.</summary>
    Cancelled
}

/// <summary>Коды статусов брони в контракте API.</summary>
public static class BookingStatuses
{
    public const string Active = "active";
    public const string Cancelled = "cancelled";

    /// <summary>Код статуса для ответа API.</summary>
    public static string ToCode(BookingStatus status)
    {
        return status switch
        {
            BookingStatus.Active => Active,
            BookingStatus.Cancelled => Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Неизвестный статус брони")
        };
    }

    /// <summary>Разбор кода статуса без учёта регистра.</summary>
    public static bool TryParse(string? code, out BookingStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(code)) return false;

        switch (code.Trim().ToLowerInvariant())
        {
            case Active:
                status = BookingStatus.Active;
                return true;
            case Cancelled:
                status = BookingStatus.Cancelled;
                return true;
            default:
                return false;
        }
    }
}