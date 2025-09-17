using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pulse_Connect_API.Models
{
    // In your UserProvince model (if it doesn't exist, create it)
    public class UserProvince
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; }
        public string Province { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LeftAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public User User { get; set; }
    }

}
