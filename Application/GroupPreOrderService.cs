using GroupPreOrder.Contracts;
using GroupPreOrder.Domain;
using GroupPreOrder.Infrastructure;

namespace GroupPreOrder.Application;

public interface IGroupPreOrderService
{
    // 🟢 Рівень 1
    Task<GroupSessionsResponse> GetSessionsAsync(string groupReservationId, string userId, CancellationToken ct);

    // 🔵 Рівень 2
    Task<OwnerSessionIdResponse> GetOwnerSessionIdAsync(string reservationId, string userId, CancellationToken ct);
    Task<IReadOnlyList<ConsolidatedItemDto>> SubmitAsync(string groupReservationId, SubmitGroupPreOrderRequest request, string userId, CancellationToken ct);

    // 🟣 Рівень 3
    Task<GroupSessionsResponse> DeleteSessionAsync(string groupReservationId, string guestSessionId, string userId, CancellationToken ct);
}

public class GroupPreOrderService(IGroupPreOrderRepository repo) : IGroupPreOrderService
{
    // ======================= 🟢 Рівень 1 =======================
    public async Task<GroupSessionsResponse> GetSessionsAsync(string groupReservationId, string userId, CancellationToken ct)
    {
        var group = await GetGroupOrThrowAsync(groupReservationId, ct);
        await EnsureOwnerAsync(group.ReservationId, userId, ct);

        var sessions = await repo.GetSessionsByGroupAsync(group.Id, ct);
        return await BuildSnapshotAsync(sessions, ct);
    }

    // ======================= 🔵 Рівень 2.1 =======================
    public async Task<OwnerSessionIdResponse> GetOwnerSessionIdAsync(string reservationId, string userId, CancellationToken ct)
    {
        await EnsureOwnerAsync(reservationId, userId, ct);   // 404, якщо reservation немає; 403, якщо не Owner

        var ownerSession = await repo.GetOwnerSessionByReservationAsync(reservationId, ct)
            ?? throw new NotFoundException($"Owner session for reservation {reservationId} not found.");

        return new OwnerSessionIdResponse(ownerSession.Id);
    }

    // ======================= 🔵 Рівень 2.2 =======================
    public async Task<IReadOnlyList<ConsolidatedItemDto>> SubmitAsync(
        string groupReservationId, SubmitGroupPreOrderRequest request, string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.ReservationId))
            throw new BadRequestException("ReservationId is required.");

        var group = await GetGroupOrThrowAsync(groupReservationId, ct);                // 404
        await EnsureOwnerAsync(request.ReservationId, userId, ct);                   // 404 / 403

        if (group.ReservationId != request.ReservationId)
            throw new BadRequestException("Group reservation does not belong to this reservation.");

        if (group.Status == GroupReservationStatus.Submitted)
            throw new BadRequestException("Group reservation was submitted.");
        if (group.Status != GroupReservationStatus.Open)
            throw new BadRequestException($"Cannot submit order. Group reservation is '{group.Status}'.");

        // 1. лише submitted сесії (Owner першим, далі в порядку приєднання)
        var submittedSessions = (await repo.GetSessionsByGroupAsync(group.Id, ct))
            .Where(s => s.IsSubmitted)
            .OrderBy(s => s.Type == GuestSessionType.Owner ? 0 : 1)
            .ThenBy(s => s.JoinedAt, StringComparer.Ordinal)
            .ToList();

        if (submittedSessions.Count == 0)
            throw new BadRequestException("No submitted guest sessions found to consolidate.");

        // 2-3. зібрати всі страви і об'єднати по dishId (порядок першої появи)
        var consolidated = submittedSessions
            .SelectMany(s => s.Items ?? new List<DishQuantity>())
            .Where(i => i.Quantity > 0 && !string.IsNullOrEmpty(i.DishId))
            .GroupBy(i => i.DishId)
            .Select(g => new ConsolidatedItemDto(g.Key, g.Sum(i => i.Quantity)))
            .ToList();

        if (consolidated.Count == 0)
            throw new BadRequestException("No dishes selected in the submitted guest sessions.");

        var order = await repo.GetOrderByReservationAsync(request.ReservationId, ct)
            ?? throw new BadRequestException("Active order not found for this reservation.");

        // 4. додати страви в order (якщо страва вже є — збільшити кількість)
        order.DishItems ??= new List<DishQuantity>();
        foreach (var item in consolidated)
        {
            var existing = order.DishItems.FirstOrDefault(d => d.DishId == item.DishId);
            if (existing is not null)
                existing.Quantity += item.Quantity;
            else
                order.DishItems.Add(new DishQuantity { DishId = item.DishId, Quantity = item.Quantity });
        }

        // 5-6. статуси
        order.Status = OrderStatus.SubmittedByCustomer;
        order.UpdatedAt = DateTime.UtcNow.ToString("O");
        group.Status = GroupReservationStatus.Submitted;

        await repo.SaveSubmitAsync(order, group, ct);   // одна транзакція — або обидва, або нічого
        return consolidated;
    }

    // ======================= 🟣 Рівень 3 =======================
    public async Task<GroupSessionsResponse> DeleteSessionAsync(
        string groupReservationId, string guestSessionId, string userId, CancellationToken ct)
    {
        var group = await GetGroupOrThrowAsync(groupReservationId, ct);
        await EnsureOwnerAsync(group.ReservationId, userId, ct);

        var session = await repo.GetGuestSessionAsync(guestSessionId, ct)
            ?? throw new NotFoundException($"Guest session with id {guestSessionId} not found.");

        if (session.GroupReservationId != group.Id)
            throw new BadRequestException("Guest session does not belong to this group reservation.");

        if (session.Type == GuestSessionType.Owner)
            throw new BadRequestException("Owner session cannot be removed.");

        await repo.DeleteGuestSessionAsync(session.Id, ct);

        // GSI оновлюється eventually consistent, тому видалену сесію відфільтровуємо вручну
        var sessions = (await repo.GetSessionsByGroupAsync(group.Id, ct))
            .Where(s => s.Id != session.Id)
            .ToList();

        return await BuildSnapshotAsync(sessions, ct);
    }

    // ======================= Helpers =======================
    private async Task<GroupReservation> GetGroupOrThrowAsync(string groupReservationId, CancellationToken ct)
        => await repo.GetGroupReservationAsync(groupReservationId, ct)
           ?? throw new NotFoundException($"Group reservation with id {groupReservationId} not found.");

    private async Task<Reservation> EnsureOwnerAsync(string reservationId, string userId, CancellationToken ct)
    {
        var reservation = await repo.GetReservationAsync(reservationId, ct)
            ?? throw new NotFoundException($"Reservation with id {reservationId} not found.");

        if (reservation.CustomerId != userId)
            throw new ForbiddenException("Only the reservation owner can perform this action.");

        return reservation;
    }

    private async Task<GroupSessionsResponse> BuildSnapshotAsync(List<GuestSession> sessions, CancellationToken ct)
    {
        var dishes = await repo.GetDishesAsync(
            sessions.SelectMany(s => s.Items ?? new List<DishQuantity>()).Select(i => i.DishId), ct);

        var participants = sessions
            .OrderBy(s => s.Type == GuestSessionType.Owner ? 0 : 1)
            .ThenBy(s => s.JoinedAt, StringComparer.Ordinal)
            .Select(s =>
            {
                var items = (s.Items ?? new List<DishQuantity>())
                    .Where(i => i.Quantity > 0 && dishes.ContainsKey(i.DishId))   // страву видалили з меню — пропускаємо
                    .Select(i =>
                    {
                        var d = dishes[i.DishId];
                        return new ParticipantItemDto(new DishDto(d.Id, d.Name, d.Price, d.ImageUrl, d.Weight), i.Quantity);
                    })
                    .ToList();

                return new ParticipantDto(
                    s.Id,
                    s.Type.ToString(),
                    s.Name,
                    items,
                    items.Sum(i => i.Dish.Price * i.Quantity),
                    s.IsSubmitted);
            })
            .ToList();

        return new GroupSessionsResponse(participants, participants.Sum(p => p.Total));
    }
}
