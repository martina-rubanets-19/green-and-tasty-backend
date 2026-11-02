using System.Security.Claims;
using GroupPreOrder.Application;
using GroupPreOrder.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPreOrder.Api;

[ApiController]
[Authorize]
public class GroupPreOrderController(IGroupPreOrderService service) : ControllerBase
{
    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new ForbiddenException("User is not identified.");

    // 🟢 Рівень 1
    [HttpGet("guest/groups/{groupReservationId}/sessions")]
    [ProducesResponseType<GroupSessionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupSessionsResponse>> GetSessions(string groupReservationId, CancellationToken ct)
        => Ok(await service.GetSessionsAsync(groupReservationId, UserId, ct));

    // 🔵 Рівень 2.1
    [HttpGet("reservations/{reservationId}/guest-session-id")]
    [ProducesResponseType<OwnerSessionIdResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OwnerSessionIdResponse>> GetOwnerSessionId(string reservationId, CancellationToken ct)
        => Ok(await service.GetOwnerSessionIdAsync(reservationId, UserId, ct));

    // 🔵 Рівень 2.2
    [HttpPost("group-reservations/{groupReservationId}/submit")]
    [ProducesResponseType<IReadOnlyList<ConsolidatedItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ConsolidatedItemDto>>> Submit(
        string groupReservationId, [FromBody] SubmitGroupPreOrderRequest request, CancellationToken ct)
        => Ok(await service.SubmitAsync(groupReservationId, request, UserId, ct));

    // 🟣 Рівень 3
    [HttpDelete("guest/groups/{groupReservationId}/sessions/{guestSessionId}")]
    [ProducesResponseType<GroupSessionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupSessionsResponse>> DeleteSession(
        string groupReservationId, string guestSessionId, CancellationToken ct)
        => Ok(await service.DeleteSessionAsync(groupReservationId, guestSessionId, UserId, ct));
}
