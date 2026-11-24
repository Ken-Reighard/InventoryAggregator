using InventoryAggregator.Core.Interfaces;
using InventoryAggregator.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAggregator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryAggregationService _aggregationService;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(
            IInventoryAggregationService aggregationService,
            ILogger<InventoryController> logger)
        {
            _aggregationService = aggregationService;
            _logger = logger;
        }

        [HttpGet("summary")]
        [ProducesResponseType(typeof(AggregatedInventoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
        public async Task<ActionResult<AggregatedInventoryResponse>> GetSummary(CancellationToken cancellationToken)
        {
            try
            {
                var summary = await _aggregationService.GetAggregatedInventoryAsync(cancellationToken);
                return Ok(summary);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Upstream inventory service request failed.");
                return Problem(
                    title: "Upstream inventory service failure",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Upstream inventory service request timed out.");
                return Problem(
                    title: "Upstream inventory service timed out",
                    detail: "The upstream inventory API did not respond within the allowed timeout.",
                    statusCode: StatusCodes.Status504GatewayTimeout);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Upstream inventory payload validation failed.");
                return Problem(
                    title: "Upstream inventory data error",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
        }
    }
}

