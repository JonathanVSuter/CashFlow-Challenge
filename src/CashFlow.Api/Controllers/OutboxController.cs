using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
using CashFlow.Application.Features.Outbox.Queries.GetOutboxMessages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/outbox")]
public sealed class OutboxController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromServices] IQueryHandler<GetOutboxMessagesQuery, IReadOnlyCollection<OutboxMessageDto>> handler, CancellationToken cancellationToken)
    {
        return Ok(await handler.HandleAsync(new GetOutboxMessagesQuery(), cancellationToken));
    }
}
