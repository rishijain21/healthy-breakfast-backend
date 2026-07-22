// Sovva.Application/Interfaces/IDashboardService.cs

using System.Threading.Tasks;
using Sovva.Application.DTOs;

namespace Sovva.Application.Interfaces
{
    /// <summary>
    /// Dashboard aggregation service for user bootstrap data
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>
        /// Get aggregated dashboard summary for the current user
        /// Runs 5 queries in parallel for fast response
        /// </summary>
        Task<DashboardSummaryDto> GetDashboardSummaryAsync(int userId, CancellationToken ct = default);

        /// <summary>
        /// Invalidate the dashboard profile cache for a user
        /// </summary>
        Task InvalidateDashboardCacheAsync(int userId);

        /// <summary>
        /// Fast, lightweight dashboard data — wallet balance + active sub count + tomorrow orders.
        /// Runs fewer queries than GetDashboardSummaryAsync. Used by the new v2 endpoint.
        /// </summary>
        Task<DashboardLightDto> GetDashboardLightAsync(int userId, CancellationToken ct = default);
    }
}