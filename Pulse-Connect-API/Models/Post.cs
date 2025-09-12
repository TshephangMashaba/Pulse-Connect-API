using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pulse_Connect_API.Models
{
    public class Post
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public User Author { get; set; }

        public string? Province { get; set; } // Nullable for general posts

        public PostType Type { get; set; } = PostType.Discussion;

        public string? Topic { get; set; } // HIV/AIDS, Mental Health, etc.

        public bool IsAnonymous { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public int Likes { get; set; } = 0;

        public int Views { get; set; } = 0;

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<PostImage> Images { get; set; } = new List<PostImage>();
    }

    public enum PostType
    {
        Discussion,
        Question,
        Resource,
        Event
    }
    

}