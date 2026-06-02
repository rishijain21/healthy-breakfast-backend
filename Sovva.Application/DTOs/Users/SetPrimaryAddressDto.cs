using System.ComponentModel.DataAnnotations;

namespace Sovva.Application.DTOs
{
    public class SetPrimaryAddressDto
    {
        [Required]
        public int AddressId { get; set; }
    }
}
