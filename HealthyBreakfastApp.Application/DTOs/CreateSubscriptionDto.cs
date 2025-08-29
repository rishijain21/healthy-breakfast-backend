using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class CreateSubscriptionDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public int UserMealId { get; set; }

        [Required]
        [StringLength(50)]
        public string Frequency { get; set; } = null!; // e.g., "Daily", "Weekly", "Monthly"

        [Required]
        public DateOnly StartDate { get; set; }

        [Required]
        public DateOnly EndDate { get; set; }

        public bool Active { get; set; } = true;
    }
}
