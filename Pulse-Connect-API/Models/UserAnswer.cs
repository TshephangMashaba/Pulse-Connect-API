using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pulse_Connect_API.Models
{
    public class UserAnswer
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string TestAttemptId { get; set; }

        [Required]
        public string QuestionId { get; set; }

        public string? SelectedOptionId { get; set; }
        public bool IsCorrect { get; set; }

        [ForeignKey("TestAttemptId")]
        public TestAttempt TestAttempt { get; set; }

        [ForeignKey("QuestionId")]
        public TestQuestion Question { get; set; }

        [ForeignKey("SelectedOptionId")]
        public QuestionOption? SelectedOption { get; set; }
    }
}