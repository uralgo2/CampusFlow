namespace CampusFlow.Domain;

/// <summary>
/// Бронь аудитории на конкретную дату и интервал времени. Интервал полуоткрытый: <c>[StartTime, EndTime)</c>,
/// поэтому «встык» (15:00 конец и 15:00 начало) конфликтом не считается.
/// </summary>
public sealed class Booking
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public Room? Room { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public BookingStatus Status { get; set; }

    /// <summary>Необязательный комментарий, максимум 500 символов.</summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Пересекается ли действующая бронь с интервалом <paramref name="start"/>/<paramref name="end"/>.
    /// Касание границ пересечением не является.
    /// </summary>
    public bool Overlaps(TimeOnly start, TimeOnly end) =>
        Status == BookingStatus.Active && StartTime < end && start < EndTime;
}
