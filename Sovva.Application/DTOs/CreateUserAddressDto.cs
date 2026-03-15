using System.ComponentModel.DataAnnotations;

namespace Sovva.Application.DTOs
{
    public class CreateUserAddressDto
    {
        [Required]
        public int ServiceableLocationId { get; set; }
        
        [MaxLength(50)]
        public string? Wing { get; set; }
        
        [MaxLength(50)]
        public string? Block { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string FlatNumber { get; set; } = string.Empty;
        
        [MaxLength(20)]
        public string? Floor { get; set; }
        
        [MaxLength(500)]
        public string? AdditionalInstructions { get; set; }
        
        [MaxLength(50)]
        public string? Label { get; set; }
        
        public bool SetAsPrimary { get; set; } = false;
    }
}
