using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pulse_Connect_API.Models
{
    public class QuestionOption
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string QuestionId { get; set; }

        [ForeignKey("QuestionId")]
        public TestQuestion Question { get; set; }

        [Required]
        [MaxLength(500)]
        public string OptionText { get; set; }

        public bool IsCorrect { get; set; }
        public int Order { get; set; }
    }
}