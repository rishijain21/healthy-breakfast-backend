using System;
using System.ComponentModel.DataAnnotations;

namespace Sovva.Application.DTOs
{
    public class RegisterUserRequest
    {
        /// <summary>
        /// Populated by AuthController from JWT sub claim — NOT sent by client.
        /// </summary>
        public Guid AuthId { get; set; }

        /// <summary>
        /// Populated by AuthController from JWT email claim — NOT sent by client.
        /// Kept here for service compatibility.
        /// </summary>
        public string Email { get; set; } = null!;

        [Required]
        [StringLength(255, MinimumLength = 2)]
        public string Name { get; set; } = null!;

        public string? Phone { get; set; }
    }
}
