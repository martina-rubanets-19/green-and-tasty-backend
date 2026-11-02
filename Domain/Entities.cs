using Amazon.DynamoDBv2.DataModel;

namespace GroupPreOrder.Domain;

// ПРИПУЩЕННЯ: назви таблиць і атрибутів. Dishes — точно як у seed dishes.json.
// Решту (Reservations, GroupReservations, GuestSessions, Orders) звірте з проєктом:
// достатньо поправити рядки в [DynamoDBTable] / [DynamoDBProperty] / IndexNames,
// логіка сервісу від цього не зміниться.
// Enum-и DynamoDBContext зберігає як N (число) — так само, як state/dishType у seed.

public enum GuestSessionType { Owner = 0, Guest = 1 }

public enum GroupReservationStatus { Open = 0, Submitted = 1, Closed = 2 }

public enum OrderStatus { Draft = 0, SubmittedByCustomer = 1, InProgress = 2, Completed = 3, Cancelled = 4 }

public static class DynamoIndexes
{
    /// <summary>GSI таблиці GuestSessions: partition key = groupReservationId</summary>
    public const string GuestSessionsByGroup = "groupReservationId-index";

    /// <summary>GSI таблиці GuestSessions: partition key = reservationId (лише owner session має це поле)</summary>
    public const string GuestSessionsByReservation = "reservationId-index";

    /// <summary>GSI таблиці Orders: partition key = reservationId</summary>
    public const string OrdersByReservation = "reservationId-index";
}

[DynamoDBTable("Reservations")]
public class Reservation
{
    [DynamoDBHashKey("id")] public string Id { get; set; } = default!;

    /// <summary>Id користувача, який створив бронювання (Owner).</summary>
    [DynamoDBProperty("customerId")] public string CustomerId { get; set; } = default!;

    [DynamoDBProperty("locationId")] public string? LocationId { get; set; }
}

[DynamoDBTable("GroupReservations")]
public class GroupReservation
{
    [DynamoDBHashKey("id")] public string Id { get; set; } = default!;
    [DynamoDBProperty("reservationId")] public string ReservationId { get; set; } = default!;
    [DynamoDBProperty("status")] public GroupReservationStatus Status { get; set; } = GroupReservationStatus.Open;
    [DynamoDBProperty("maxCapacity")] public int MaxCapacity { get; set; }
}

[DynamoDBTable("GuestSessions")]
public class GuestSession
{
    [DynamoDBHashKey("id")] public string Id { get; set; } = default!;

    [DynamoDBGlobalSecondaryIndexHashKey(DynamoIndexes.GuestSessionsByGroup, AttributeName = "groupReservationId")]
    public string GroupReservationId { get; set; } = default!;

    /// <summary>Заповнене лише для owner session.</summary>
    [DynamoDBGlobalSecondaryIndexHashKey(DynamoIndexes.GuestSessionsByReservation, AttributeName = "reservationId")]
    public string? ReservationId { get; set; }

    [DynamoDBProperty("type")] public GuestSessionType Type { get; set; }
    [DynamoDBProperty("name")] public string Name { get; set; } = default!;
    [DynamoDBProperty("isSubmitted")] public bool IsSubmitted { get; set; }

    /// <summary>ISO-8601 UTC — визначає порядок учасників у відповіді.</summary>
    [DynamoDBProperty("joinedAt")] public string JoinedAt { get; set; } = DateTime.UtcNow.ToString("O");

    /// <summary>Кошик гостя зберігається в самій сесії (List of Map) — без окремої таблиці.</summary>
    [DynamoDBProperty("items")] public List<DishQuantity> Items { get; set; } = new();
}

/// <summary>Елемент кошика / замовлення: { dishId, quantity }.</summary>
public class DishQuantity
{
    [DynamoDBProperty("dishId")] public string DishId { get; set; } = default!;
    [DynamoDBProperty("quantity")] public int Quantity { get; set; }
}

[DynamoDBTable("Dishes")]
public class Dish
{
    [DynamoDBHashKey("id")] public string Id { get; set; } = default!;
    [DynamoDBProperty("name")] public string Name { get; set; } = default!;
    [DynamoDBProperty("price")] public decimal Price { get; set; }
    [DynamoDBProperty("imageUrl")] public string? ImageUrl { get; set; }
    [DynamoDBProperty("weight")] public string? Weight { get; set; }
    [DynamoDBProperty("locationId")] public string? LocationId { get; set; }
}

[DynamoDBTable("Orders")]
public class Order
{
    [DynamoDBHashKey("id")] public string Id { get; set; } = default!;

    [DynamoDBGlobalSecondaryIndexHashKey(DynamoIndexes.OrdersByReservation, AttributeName = "reservationId")]
    public string ReservationId { get; set; } = default!;

    [DynamoDBProperty("status")] public OrderStatus Status { get; set; } = OrderStatus.Draft;
    [DynamoDBProperty("dishItems")] public List<DishQuantity> DishItems { get; set; } = new();
    [DynamoDBProperty("updatedAt")] public string? UpdatedAt { get; set; }
}
