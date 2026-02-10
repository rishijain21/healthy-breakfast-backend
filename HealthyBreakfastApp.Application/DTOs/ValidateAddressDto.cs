namespace HealthyBreakfastApp.Application.DTOs
{
    public class ValidateAddressDto
    {
        public bool IsServiceable { get; set; }
        public string Message { get; set; } = string.Empty;
        public ServiceableLocationDto? ServiceableLocation { get; set; }
    }
}
