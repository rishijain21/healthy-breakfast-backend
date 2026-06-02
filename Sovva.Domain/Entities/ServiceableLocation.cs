using System.ComponentModel.DataAnnotations;

namespace Sovva.Domain.Entities
{
    public class ServiceableLocation : BaseEntity
    {
        public int Id { get; set; }
        
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
        public string Pincode { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
        
        // For future geofencing/mapping features
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        
        // Delivery time slot (optional for future use)
        [MaxLength(100)]
        public string? DeliveryTimeSlot { get; set; }
        
        // Navigation Properties
        public ICollection<UserAddress> UserAddresses { get; set; } = new List<UserAddress>();
        
        // Computed property for display
        public string FullAddress => $"{LandmarkOrSociety}, {Locality}, {Area}, {City} - {Pincode}".Trim(' ', ',');
    }
}
