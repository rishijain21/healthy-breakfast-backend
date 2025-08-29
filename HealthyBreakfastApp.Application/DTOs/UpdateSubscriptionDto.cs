using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class UpdateSubscriptionDto
    {
        [StringLength(50)]
        public string? Frequency { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        public bool? Active { get; set; }
    }
}
