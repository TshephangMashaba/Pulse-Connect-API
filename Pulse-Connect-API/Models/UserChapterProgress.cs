using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pulse_Connect_API.Models
{
    public class UserChapterProgress
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string EnrollmentId { get; set; }

        [Required]
        public string ChapterId { get; set; }

        [ForeignKey("EnrollmentId")]
        public Enrollment Enrollment { get; set; }

        [ForeignKey("ChapterId")]
        public Chapter Chapter { get; set; }

        public bool IsCompleted { get; set; }
        public DateTime? CompletedDate { get; set; }
        public TimeSpan TimeSpent { get; set; }
    }
}