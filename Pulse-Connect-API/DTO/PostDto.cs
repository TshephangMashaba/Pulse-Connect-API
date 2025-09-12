// DTOs/PostDto.cs
namespace Pulse_Connect_API.DTO
{
    public class CreatePostDto
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string? Province { get; set; }
        public string Type { get; set; } = "Discussion";
        public string? Topic { get; set; }
        public bool IsAnonymous { get; set; } = false;
    }

    public class PostDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string? AuthorProfilePicture { get; set; }
        public string? Province { get; set; }
        public string Type { get; set; }
        public string? Topic { get; set; }
        public bool IsAnonymous { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Likes { get; set; }
        public int Views { get; set; }
        public int CommentCount { get; set; }
        public List<PostImageDto> Images { get; set; } = new List<PostImageDto>(); 
    }
    public class PostImageDto
    {
        public string Id { get; set; }
        public string ImageUrl { get; set; }
        public string? Caption { get; set; }
        public int Order { get; set; }
    }

    public class UpdatePostDto
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string? Topic { get; set; }
        public List<string>? ImageUrls { get; set; }
    }
}

// DTOs/CommentDto.cs
namespace Pulse_Connect_API.DTOs
{
    public class CreateCommentDto
    {
        public string Content { get; set; }
        public string PostId { get; set; }
        public string? ParentCommentId { get; set; }
    }

    public class CommentDto
    {
        public string Id { get; set; }
        public string Content { get; set; }
        public string AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string? AuthorProfilePicture { get; set; }
        public string PostId { get; set; }
        public string? ParentCommentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Likes { get; set; }
        public int ReplyCount { get; set; }
    }
}

// DTOs/ProvinceDto.cs
namespace Pulse_Connect_API.DTOs
{
    public class JoinProvinceDto
    {
        public string Province { get; set; }
    }

    public class ProvinceStatsDto
    {
        public string Province { get; set; }
        public int MemberCount { get; set; }
        public int PostCount { get; set; }
        public int ActiveDiscussions { get; set; }
    }
}