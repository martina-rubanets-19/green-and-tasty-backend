namespace GroupPreOrder.Contracts;

// ===== Рівень 1 / 3: snapshot групи =====
public record DishDto(string Id, string Name, decimal Price, string? ImageUrl, string? Weight);

public record ParticipantItemDto(DishDto Dish, int Quantity);

public record ParticipantDto(
    string Id,
    string Type,          // "Owner" | "Guest"
    string Name,
    IReadOnlyList<ParticipantItemDto> Items,
    decimal Total,
    bool IsSubmitted);

public record GroupSessionsResponse(IReadOnlyList<ParticipantDto> Participants, decimal Total);

// ===== Рівень 2 =====
public record OwnerSessionIdResponse(string GuestSessionId);

public record SubmitGroupPreOrderRequest(string ReservationId);

public record ConsolidatedItemDto(string DishId, int Quantity);
