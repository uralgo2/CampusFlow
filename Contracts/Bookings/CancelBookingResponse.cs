namespace CampusFlow.Contracts.Bookings;

/// <summary>Ответ на отмену брони.</summary>
/// <param name="Id">Идентификатор брони.</param>
/// <param name="Status">Новый статус: всегда <c>cancelled</c>.</param>
public sealed record CancelBookingResponse(int Id, string Status);