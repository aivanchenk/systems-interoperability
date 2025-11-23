namespace Services;

public interface IFarmService
{
    /// <summary>
	/// Try submitting Food resource.
	/// </summary>
	/// <param name="amount">Amount of food submitted.</param>
	/// <returns>Submit result descriptor.</returns>
    SubmissionResult SubmitFood(int amount);

    /// <summary>
    /// Try submitting Water resource.
    /// </summary>
    /// <param name="amount">Amount of water submitted.</param>
    /// <returns>Submit result descriptor.</returns>
    SubmissionResult SubmitWater(int amount);
}