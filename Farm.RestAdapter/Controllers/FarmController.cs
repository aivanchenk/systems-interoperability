using Microsoft.AspNetCore.Mvc;

namespace Farm.RestAdapter.Controllers;

/// <summary>
/// Result of a submission operation
/// </summary>
public class SubmissionResult
{
    /// <summary>
    /// Whether the submission was accepted
    /// </summary>
    public bool IsAccepted { get; set; }

    /// <summary>
    /// Reason for failure if not accepted
    /// </summary>
    public string? FailReason { get; set; }
}

/// <summary>
/// API controller that adapts REST calls to gRPC Farm service
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class FarmController : ControllerBase
{
    private readonly ILogger<FarmController> _logger;
    private readonly Services.Farm.FarmClient _grpcClient;

    /// <summary>
    /// Initializes a new instance of the FarmController
    /// </summary>
    public FarmController(ILogger<FarmController> logger, Services.Farm.FarmClient grpcClient)
    {
        _logger = logger;
        _grpcClient = grpcClient;
    }

    /// <summary>
    /// Submit food to the farm
    /// </summary>
    /// <param name="amount">Amount of food to submit</param>
    /// <returns>Result of the submission</returns>
    [HttpPost("submitFood")]
    [ProducesResponseType(typeof(SubmissionResult), 200)]
    public async Task<ActionResult<SubmissionResult>> SubmitFood([FromBody] double amount)
    {
        try
        {
            _logger.LogInformation($"REST -> gRPC: Submitting {amount} food");

            var request = new Services.SubmitRequest { Amount = amount };
            var grpcResponse = await _grpcClient.SubmitFoodAsync(request);

            var response = new SubmissionResult
            {
                IsAccepted = grpcResponse.IsAccepted,
                FailReason = grpcResponse.FailReason
            };

            _logger.LogInformation($"gRPC -> REST: Food submission result: Accepted={response.IsAccepted}");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling gRPC Farm service");
            return StatusCode(500, new SubmissionResult
            {
                IsAccepted = false,
                FailReason = "Adapter error: " + ex.Message
            });
        }
    }

    /// <summary>
    /// Submit water to the farm
    /// </summary>
    /// <param name="amount">Amount of water to submit</param>
    /// <returns>Result of the submission</returns>
    [HttpPost("submitWater")]
    [ProducesResponseType(typeof(SubmissionResult), 200)]
    public async Task<ActionResult<SubmissionResult>> SubmitWater([FromBody] double amount)
    {
        try
        {
            _logger.LogInformation($"REST -> gRPC: Submitting {amount} water");

            var request = new Services.SubmitRequest { Amount = amount };
            var grpcResponse = await _grpcClient.SubmitWaterAsync(request);

            var response = new SubmissionResult
            {
                IsAccepted = grpcResponse.IsAccepted,
                FailReason = grpcResponse.FailReason
            };

            _logger.LogInformation($"gRPC -> REST: Water submission result: Accepted={response.IsAccepted}");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling gRPC Farm service");
            return StatusCode(500, new SubmissionResult
            {
                IsAccepted = false,
                FailReason = "Adapter error: " + ex.Message
            });
        }
    }
}