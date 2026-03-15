using System;
using System.ComponentModel.DataAnnotations;

namespace Sovva.Application.DTOs
{
    public class UpdateServiceableLocationDto
    {
        [MaxLength(100)]
        public string? City { get; set; }
        
        [MaxLength(100)]
        public string? Area { get; set; }
        
        [MaxLength(200)]
        public string? Locality { get; set; }
        
        [MaxLength(200)]
        public string? LandmarkOrSociety { get; set; }
        
        [MaxLength(10)]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be 6 digits")]
        public string? Pincode { get; set; }
        
        public bool? IsActive { get; set; }
        
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        
        [MaxLength(100)]
        public string? DeliveryTimeSlot { get; set; }
    }
}
