using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pulse_Connect_API.Models
{
    public class TestQuestion
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string TestId { get; set; }

        [ForeignKey("TestId")]
        public CourseTest Test { get; set; }

        [Required]
        [MaxLength(500)]
        public string QuestionText { get; set; }

        public int Order { get; set; }

        public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
    }
}