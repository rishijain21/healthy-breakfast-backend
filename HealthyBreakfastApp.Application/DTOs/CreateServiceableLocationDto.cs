using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class CreateServiceableLocationDto
    {
        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string Area { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string Locality { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string LandmarkOrSociety { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(10)]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be 6 digits")]
        public string Pincode { get; set; } = string.Empty;
        
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        
        [MaxLength(100)]
        public string? DeliveryTimeSlot { get; set; }
    }
}
