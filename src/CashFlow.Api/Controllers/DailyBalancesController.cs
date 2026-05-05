using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
using CashFlow.Application.Features.DailyBalances.Queries.GetDailyBalanceByDate;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Api.Controllers;

[ApiController]
[Route("api/daily-balances")]
public sealed class DailyBalancesController : ControllerBase
{
    [HttpGet("{date}")]
    public async Task<IActionResult> GetByDate(
        [FromServices] IQueryHandler<GetDailyBalanceByDateQuery, DailyBalanceDto?> handler,
        [FromRoute] DateOnly date,
        CancellationToken cancellationToken)
    {
        var response = await handler.HandleAsync(new GetDailyBalanceByDateQuery(date), cancellationToken);
        return response is null ? NotFound(new { message = "Daily balance not found yet." }) : Ok(response);
    }
}
