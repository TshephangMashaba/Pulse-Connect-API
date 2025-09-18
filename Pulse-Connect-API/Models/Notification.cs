// Models/Notification.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pulse_Connect_API.Models
{
    public class Notification
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string UserId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        [MaxLength(500)]
        public string Message { get; set; }

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } // "info", "success", "warning", "error"

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAt { get; set; }

        public string RelatedEntityId { get; set; }

        [MaxLength(50)]
        public string RelatedEntityType { get; set; } // "course", "certificate", "test", "system"

        // Navigation properties
        [ForeignKey("UserId")]
        public User User { get; set; }
    }
}