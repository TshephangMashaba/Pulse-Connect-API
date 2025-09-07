using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pulse_Connect_API.Models
{
    public class TestAttempt
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string EnrollmentId { get; set; }

        [Required]
        public string TestId { get; set; }

        [ForeignKey("EnrollmentId")]
        public Enrollment Enrollment { get; set; }

        [ForeignKey("TestId")]
        public CourseTest Test { get; set; }

        public DateTime AttemptDate { get; set; } = DateTime.UtcNow;
        public int Score { get; set; }
        public bool IsPassed { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }

        public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
    }
}