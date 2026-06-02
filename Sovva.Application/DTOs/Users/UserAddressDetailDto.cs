namespace Sovva.Application.DTOs
{
    public class UserAddressDetailDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? Wing { get; set; }
        public string? Block { get; set; }
        public string FlatNumber { get; set; } = string.Empty;
        public string? Floor { get; set; }
        public string? AdditionalInstructions { get; set; }
        public string? Label { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsActive { get; set; }
        public string CompleteAddress { get; set; } = string.Empty;
        
        public ServiceableLocationDto ServiceableLocation { get; set; } = null!;
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
