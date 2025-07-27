using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Pulse_Connect_API.Models

{
    public class UserRole
    {
        [Key]
        public int UserRoleID { get; set; }
        [Required]
        public string? RoleName { get; set; } // Name of the role
        [Required]
        public string? RoleDescription { get; set; } // Description of the role
        public ICollection<User>? Users { get; set; } // Navigation property for users with this role
    }
}
