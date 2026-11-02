using Amazon.DynamoDBv2.DataModel;
using GroupPreOrder.Domain;

namespace GroupPreOrder.Infrastructure;

/// <summary>Доступ до DynamoDB для групового pre-order. Сервіс працює лише з цим інтерфейсом.</summary>
public interface IGroupPreOrderRepository
{
    Task<Reservation?> GetReservationAsync(string reservationId, CancellationToken ct);
    Task<GroupReservation?> GetGroupReservationAsync(string groupReservationId, CancellationToken ct);
    Task<GuestSession?> GetGuestSessionAsync(string guestSessionId, CancellationToken ct);
    Task<List<GuestSession>> GetSessionsByGroupAsync(string groupReservationId, CancellationToken ct);
    Task<GuestSession?> GetOwnerSessionByReservationAsync(string reservationId, CancellationToken ct);
    Task<Order?> GetOrderByReservationAsync(string reservationId, CancellationToken ct);
    Task<Dictionary<string, Dish>> GetDishesAsync(IEnumerable<string> dishIds, CancellationToken ct);

    /// <summary>Атомарно зберігає order і group reservation (TransactWriteItems).</summary>
    Task SaveSubmitAsync(Order order, GroupReservation group, CancellationToken ct);

    Task DeleteGuestSessionAsync(string guestSessionId, CancellationToken ct);
}

public class GroupPreOrderRepository(IDynamoDBContext db) : IGroupPreOrderRepository
{
    public async Task<Reservation?> GetReservationAsync(string reservationId, CancellationToken ct)
        => await db.LoadAsync<Reservation>(reservationId, ct);

    public async Task<GroupReservation?> GetGroupReservationAsync(string groupReservationId, CancellationToken ct)
        => await db.LoadAsync<GroupReservation>(groupReservationId, ct);

    public async Task<GuestSession?> GetGuestSessionAsync(string guestSessionId, CancellationToken ct)
        => await db.LoadAsync<GuestSession>(guestSessionId, ct);

    public async Task<List<GuestSession>> GetSessionsByGroupAsync(string groupReservationId, CancellationToken ct)
        => await db.QueryAsync<GuestSession>(
                groupReservationId,
                new DynamoDBOperationConfig { IndexName = DynamoIndexes.GuestSessionsByGroup })
            .GetRemainingAsync(ct);

    public async Task<GuestSession?> GetOwnerSessionByReservationAsync(string reservationId, CancellationToken ct)
    {
        var sessions = await db.QueryAsync<GuestSession>(
                reservationId,
                new DynamoDBOperationConfig { IndexName = DynamoIndexes.GuestSessionsByReservation })
            .GetRemainingAsync(ct);

        return sessions.FirstOrDefault(s => s.Type == GuestSessionType.Owner);
    }

    public async Task<Order?> GetOrderByReservationAsync(string reservationId, CancellationToken ct)
    {
        var orders = await db.QueryAsync<Order>(
                reservationId,
                new DynamoDBOperationConfig { IndexName = DynamoIndexes.OrdersByReservation })
            .GetRemainingAsync(ct);

        // Якщо замовлень кілька — беремо те, що ще не скасоване/не завершене
        return orders.FirstOrDefault(o => o.Status is OrderStatus.Draft or OrderStatus.SubmittedByCustomer)
               ?? orders.FirstOrDefault();
    }

    public async Task<Dictionary<string, Dish>> GetDishesAsync(IEnumerable<string> dishIds, CancellationToken ct)
    {
        var ids = dishIds.Distinct().ToList();
        if (ids.Count == 0) return new();

        // BatchGetItem — до 100 ключів за запит; SDK сам розбиває більші пакети
        var batch = db.CreateBatchGet<Dish>();
        foreach (var id in ids) batch.AddKey(id);
        await batch.ExecuteAsync(ct);

        return batch.Results.ToDictionary(d => d.Id);
    }

    public async Task SaveSubmitAsync(Order order, GroupReservation group, CancellationToken ct)
    {
        var orderWrite = db.CreateTransactWrite<Order>();
        orderWrite.AddSaveItem(order);

        var groupWrite = db.CreateTransactWrite<GroupReservation>();
        groupWrite.AddSaveItem(group);

        await orderWrite.Combine(groupWrite).ExecuteAsync(ct);
    }

    public async Task DeleteGuestSessionAsync(string guestSessionId, CancellationToken ct)
        => await db.DeleteAsync<GuestSession>(guestSessionId, ct);
}
