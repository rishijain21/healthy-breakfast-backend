using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class SetPrimaryAddressDto
    {
        [Required]
        public int AddressId { get; set; }
    }
}
