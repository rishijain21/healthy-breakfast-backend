using System;
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
        try
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
        catch (Exception ex)
        {
            // Failsafe to prevent the filter from throwing and breaking Hangfire state transition
            Console.WriteLine($"JobFailureAlertFilter threw an exception: {ex}");
        }
    }
}
