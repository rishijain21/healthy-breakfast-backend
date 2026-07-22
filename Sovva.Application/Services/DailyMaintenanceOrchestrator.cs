using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Services
{
    public class DailyMaintenanceOrchestrator : IDailyMaintenanceOrchestrator
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IScheduledOrderService _scheduledOrderService;
        private readonly ISubscriptionSchedulingService _subscriptionSchedulingService;
        private readonly ILogger<DailyMaintenanceOrchestrator> _logger;

        public DailyMaintenanceOrchestrator(
            ISubscriptionService subscriptionService,
            IScheduledOrderService scheduledOrderService,
            ISubscriptionSchedulingService subscriptionSchedulingService,
            ILogger<DailyMaintenanceOrchestrator> logger)
        {
            _subscriptionService = subscriptionService;
            _scheduledOrderService = scheduledOrderService;
            _subscriptionSchedulingService = subscriptionSchedulingService;
            _logger = logger;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 1800)] // 30 minutes timeout
        public async Task RunDailyMaintenanceAsync()
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("[MAINT-{CorrelationId}] Starting Daily Maintenance Orchestrator", correlationId);

        var stepErrors = new List<Exception>();

        // ─────────────────────────────────────────────────────────────
        // STEP 1 — Expire Subscriptions
        // ─────────────────────────────────────────────────────────────
        try
        {
            _logger.LogInformation("[MAINT-{CorrelationId}] Step 1/4: Expire Subscriptions", correlationId);
            await _subscriptionService.ExpireSubscriptionsAsync();
            _logger.LogInformation("[MAINT-{CorrelationId}] Step 1/4 ✅ Complete", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MAINT-{CorrelationId}] Step 1/4 ❌ FAILED: Expire Subscriptions", correlationId);
            stepErrors.Add(new Exception("Step 1 (ExpireSubscriptions) failed", ex));
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 2 — Update Next Scheduled Dates
        // ─────────────────────────────────────────────────────────────
        try
        {
            _logger.LogInformation("[MAINT-{CorrelationId}] Step 2/4: Update Next Scheduled Dates", correlationId);
            await _subscriptionService.UpdateNextScheduledDatesAsync();
            _logger.LogInformation("[MAINT-{CorrelationId}] Step 2/4 ✅ Complete", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MAINT-{CorrelationId}] Step 2/4 ❌ FAILED: Update Next Scheduled Dates", correlationId);
            stepErrors.Add(new Exception("Step 2 (UpdateNextScheduledDates) failed", ex));
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 3 — Confirm All Scheduled Orders (midnight debit job)
        // NOTE: Failure here must NOT block Step 4 — GenerateOrders must
        //       always run so tomorrow's orders are created.
        // ─────────────────────────────────────────────────────────────
        try
        {
            _logger.LogInformation("[MAINT-{CorrelationId}] Step 3/4: Confirm All Scheduled Orders", correlationId);
            var confirmResult = await _scheduledOrderService.ConfirmAllScheduledOrdersAsync();
            _logger.LogInformation("[MAINT-{CorrelationId}] Step 3/4 ✅ Complete", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MAINT-{CorrelationId}] Step 3/4 ❌ FAILED: Confirm All Scheduled Orders", correlationId);
            stepErrors.Add(new Exception("Step 3 (ConfirmAllScheduledOrders) failed", ex));
            // ⚠️ INTENTIONAL: DO NOT return here — Step 4 must run regardless.
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 4 — Generate Scheduled Orders From Subscriptions
        // This MUST always run — even if Step 3 failed.
        // ─────────────────────────────────────────────────────────────
        try
        {
            _logger.LogInformation("[MAINT-{CorrelationId}] Step 4/4: Generate Scheduled Orders From Subscriptions", correlationId);
            await _subscriptionSchedulingService.GenerateScheduledOrdersFromSubscriptionsAsync(correlationId);
            _logger.LogInformation("[MAINT-{CorrelationId}] Step 4/4 ✅ Complete", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MAINT-{CorrelationId}] Step 4/4 ❌ FAILED: Generate Scheduled Orders From Subscriptions", correlationId);
            stepErrors.Add(new Exception("Step 4 (GenerateScheduledOrders) failed", ex));
        }

        // ─────────────────────────────────────────────────────────────
        // FINAL — if any steps failed, throw so Hangfire marks job as failed
        // and retries automatically
        // ─────────────────────────────────────────────────────────────
        if (stepErrors.Count > 0)
        {
            var summary = string.Join("; ", stepErrors.Select(e => e.Message));
            _logger.LogError("[MAINT-{CorrelationId}] ❌ Daily Maintenance completed with {FailedSteps}/{TotalSteps} step failures: {Summary}",
                correlationId, stepErrors.Count, 4, summary);
            throw new AggregateException(
                $"Daily Maintenance failed ({stepErrors.Count}/4 steps failed). See inner exceptions for details.",
                stepErrors);
        }

        _logger.LogInformation("[MAINT-{CorrelationId}] ✅ Daily Maintenance Orchestrator completed successfully (all 4 steps passed)", correlationId);
    }
}
}
