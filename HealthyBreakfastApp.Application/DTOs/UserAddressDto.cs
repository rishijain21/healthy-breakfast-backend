namespace HealthyBreakfastApp.Application.DTOs
{
    public class UserAddressDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ServiceableLocationId { get; set; }
        public string? Wing { get; set; }
        public string? Block { get; set; }
        public string FlatNumber { get; set; } = string.Empty;
        public string? Floor { get; set; }
        public string? AdditionalInstructions { get; set; }
        public string? Label { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsActive { get; set; }
        public string CompleteAddress { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
