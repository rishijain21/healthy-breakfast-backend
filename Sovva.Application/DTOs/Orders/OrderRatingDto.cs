using System.ComponentModel.DataAnnotations;

namespace Sovva.Application.DTOs
{
    public class OrderRatingDto
    {
        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Review { get; set; }
    }
}
