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
    }
}