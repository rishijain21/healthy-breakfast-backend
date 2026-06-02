using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;

namespace Sovva.WebAPI.Infrastructure;

public class JobFailureAlertFilter : JobFilterAttribute, IElectStateFilter
{
    private readonly ILogger<JobFailureAlertFilter> _logger;

    public JobFailureAlertFilter(ILogger<JobFailureAlertFilter> logger)
    {
        _logger = logger;
    }

    public void OnStateElection(ElectStateContext context)
    {
        if (context.CandidateState is FailedState failedState)
        {
            _logger.LogError(
                failedState.Exception,
                "CRITICAL: Hangfire job {JobId} ({JobName}) FAILED. " +
                "Manual intervention may be required for midnight wallet processing.",
                context.BackgroundJob.Id,
                context.BackgroundJob.Job.Method.Name);
        }
    }
}
