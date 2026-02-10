namespace HealthyBreakfastApp.Application.DTOs
{
    public class ServiceableLocationDto
    {
        public int Id { get; set; }
        public string City { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Locality { get; set; } = string.Empty;
        public string LandmarkOrSociety { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? DeliveryTimeSlot { get; set; }
        public string FullAddress { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
