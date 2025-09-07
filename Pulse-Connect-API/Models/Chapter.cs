using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pulse_Connect_API.Models
{
    public class Chapter
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        public string Content { get; set; }
        public int Order { get; set; }

        public string MediaUrl { get; set; }
        public string MediaType { get; set; }

        [Required]
        public string CourseId { get; set; }

        [ForeignKey("CourseId")]
        public Course Course { get; set; }

        public ICollection<UserChapterProgress> UserProgresses { get; set; } = new List<UserChapterProgress>();
    }
}