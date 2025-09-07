using Pulse_Connect_API.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class CourseTest
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string CourseId { get; set; }

    [ForeignKey("CourseId")]
    public Course Course { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; }

    public string Description { get; set; }
    public int PassingScore { get; set; } = 70;

    public ICollection<TestQuestion> Questions { get; set; } = new List<TestQuestion>();
}