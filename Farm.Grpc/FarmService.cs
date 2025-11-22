namespace Servers;

using System.Threading.Tasks;
using Grpc.Core;

public class FarmService : Services.Farm.FarmBase
{
    /// <summary>
    /// Initializes a new instance of FarmLogic
    /// </summary>
    private readonly FarmLogic mLogic = new FarmLogic();


    /// <summary>
    /// Submits a specified amount of food to the farm for processing.
    /// </summary>
    /// <param name="request">The gRPC request message with the amount of food to submit.</param>
    /// <param name="context">The gRPC call context providing information about the call.</param>
    /// <returns>
    /// A task representing the asynchronous operation, that indicates whether the submission was accepted 
    /// and, if not, why it failed.
    /// </returns>
    public override Task<Services.SubmissionResult> SubmitFood(Services.SubmitRequest request, ServerCallContext context)
    {
        var logicResult = mLogic.SubmitFood(request.Amount);
        var response = new Services.SubmissionResult
        {
            IsAccepted = logicResult.IsAccepted,
            FailReason = logicResult.FailReason ?? string.Empty
        };
        return Task.FromResult(response);
    }

    /// <summary>
    /// Submits a specified amount of water to the farm for processing.
    /// </summary>
    /// <param name="request">The gRPC request message with the amount of water to submit.</param>
    /// <param name="context">The gRPC call context providing information about the call.</param>
    /// <returns>
    /// A task representing the asynchronous operation, that indicates whether the submission was accepted
    /// and, if not, why it failed.
    /// </returns>
    public override Task<Services.SubmissionResult> SubmitWater(Services.SubmitRequest request, ServerCallContext context)
    {
        var logicResult = mLogic.SubmitWater(request.Amount);
        var response = new Services.SubmissionResult
        {
            IsAccepted = logicResult.IsAccepted,
            FailReason = logicResult.FailReason ?? string.Empty
        };
        return Task.FromResult(response);
    }
}
