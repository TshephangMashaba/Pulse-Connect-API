using Microsoft.AspNetCore.Identity;

namespace Pulse_Connect_API.Models
{
    public class User : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime DateOfBirth { get; set; }

        public string? ProfilePicture { get; set; }
         public string Address { get; set; }
        public string Race { get; set; }
        public string Gender { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }


    
}

