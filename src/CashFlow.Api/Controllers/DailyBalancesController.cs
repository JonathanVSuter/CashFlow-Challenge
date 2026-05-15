using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
using CashFlow.Application.Features.DailyBalances.Queries.GetDailyBalanceByDate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Api.Controllers;

[Authorize]
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
        //foi usado o NotFound inicialmente para poder enviar uma mensagem. O ideal seria usar o "NoContent"
        return response is null ? NoContent() : Ok(response);
    }
}
