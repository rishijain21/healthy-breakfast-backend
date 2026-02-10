using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class UpdateUserAddressDto
    {
        [MaxLength(50)]
        public string? Wing { get; set; }
        
        [MaxLength(50)]
        public string? Block { get; set; }
        
        [MaxLength(50)]
        public string? FlatNumber { get; set; }
        
        [MaxLength(20)]
        public string? Floor { get; set; }
        
        [MaxLength(500)]
        public string? AdditionalInstructions { get; set; }
        
        [MaxLength(50)]
        public string? Label { get; set; }
    }
}
