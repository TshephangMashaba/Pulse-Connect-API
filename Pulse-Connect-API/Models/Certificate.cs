using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pulse_Connect_API.Models
{
    public class Certificate
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string UserId { get; set; }

        [Required]
        public string CourseId { get; set; }

        [Required]
        public string TestAttemptId { get; set; }

        [Required]
        [MaxLength(200)]
        public string CertificateNumber { get; set; }

        [Required]
        public DateTime IssueDate { get; set; } = DateTime.UtcNow;

        [Required]
        public int Score { get; set; }

        [Required]
        public bool IsEmailed { get; set; } = false;

        public DateTime? EmailedDate { get; set; }

        [MaxLength(500)]
        public string DownloadUrl { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public User User { get; set; }

        [ForeignKey("CourseId")]
        public Course Course { get; set; }

        [ForeignKey("TestAttemptId")]
        public TestAttempt TestAttempt { get; set; }
    }
}