using System;
using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class RegisterUserRequest
    {
        [Required]
        public Guid AuthId { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [StringLength(255, MinimumLength = 2)]
        public string Name { get; set; } = null!;

        public string? Phone { get; set; }
    }
}
