using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IDailyMaintenanceOrchestrator
    {
        Task RunDailyMaintenanceAsync();
    }
}
