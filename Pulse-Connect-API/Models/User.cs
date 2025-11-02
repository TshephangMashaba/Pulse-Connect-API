
    using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

    namespace Pulse_Connect_API.Models
    {
        public class User : IdentityUser
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? PhoneNumber { get; set; }
            public DateTime? DateOfBirth { get; set; }
            public string? ProfilePicture { get; set; }
            public string? Address { get; set; }
            public string? Race { get; set; }
            public string? Gender { get; set; }
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        }
    }

public class ContactSubmission
{
    [Key]
    public string Id { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    [Required]
    [StringLength(100)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(5000)]
    public string Message { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessedBy { get; set; }
    public string? AdminNotes { get; set; }
}





