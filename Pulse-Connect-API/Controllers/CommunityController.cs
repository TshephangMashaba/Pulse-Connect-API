using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pulse_Connect_API.DTO;
using Pulse_Connect_API.DTOs;
using Pulse_Connect_API.Models;
using System.Security.Claims;

namespace Pulse_Connect_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommunityController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;

        public CommunityController(AppDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: api/community/posts
        [HttpGet("posts")]
        public async Task<ActionResult<IEnumerable<PostDto>>> GetPosts(
            [FromQuery] string? province = null,
            [FromQuery] string? type = null,
            [FromQuery] string? topic = null,
            [FromQuery] string? sortBy = "newest",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = _context.Posts
                .Include(p => p.Author)
                .Include(p => p.Comments)
                .Include(p => p.Images)
                .AsQueryable();

            // Filter by province
            if (!string.IsNullOrEmpty(province))
            {
                query = query.Where(p => p.Province == province);
            }

            // Filter by type
            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(p => p.Type.ToString().ToLower() == type.ToLower());
            }

            // Filter by topic
            if (!string.IsNullOrEmpty(topic))
            {
                query = query.Where(p => p.Topic == topic);
            }

            // Sort
            switch (sortBy.ToLower())
            {
                case "popular":
                    query = query.OrderByDescending(p => p.Likes + p.Comments.Count);
                    break;
                case "oldest":
                    query = query.OrderBy(p => p.CreatedAt);
                    break;
                default: // newest
                    query = query.OrderByDescending(p => p.CreatedAt);
                    break;
            }

            var totalCount = await query.CountAsync();
            var posts = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var postDtos = posts.Select(p => MapToPostDto(p, p.Author));

            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Page", page.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());

            return Ok(postDtos);
        }

        [HttpGet("posts/{id}")]
        public async Task<ActionResult<PostDto>> GetPost(string id)
        {
            var post = await _context.Posts
                .Include(p => p.Author)
                .Include(p => p.Comments)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                return NotFound();
            }

            // Increment view count
            post.Views++;
            await _context.SaveChangesAsync();

            return MapToPostDto(post, post.Author);
        }

        private PostDto MapToPostDto(Post post, User author)
        {
            return new PostDto
            {
                Id = post.Id,
                Title = post.Title,
                Content = post.Content,
                AuthorId = post.UserId,
                AuthorName = post.IsAnonymous ? "Anonymous" : $"{author.FirstName} {author.LastName}",
                // Fix: Ensure profile picture URL is properly formatted
                AuthorProfilePicture = post.IsAnonymous ? null : GetFullProfilePictureUrl(author.ProfilePicture),
                Province = post.Province,
                Type = post.Type.ToString(),
                Topic = post.Topic,
                IsAnonymous = post.IsAnonymous,
                CreatedAt = post.CreatedAt,
                Likes = post.Likes,
                Views = post.Views,
                CommentCount = post.Comments.Count,
                Images = post.Images.Select(i => new PostImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    Caption = i.Caption,
                    Order = i.Order
                }).ToList()
            };
        }

        private string GetFullProfilePictureUrl(string profilePicture)
        {
            if (string.IsNullOrEmpty(profilePicture))
                return null;

            // If it's already a full URL, return as-is
            if (profilePicture.StartsWith("http://") || profilePicture.StartsWith("https://"))
                return profilePicture;

            // If it's a relative path, make it absolute
            if (profilePicture.StartsWith("/"))
                return $"{Request.Scheme}://{Request.Host}{profilePicture}";

            // For relative paths without leading slash
            return $"{Request.Scheme}://{Request.Host}/{profilePicture.TrimStart('/')}";
        }

        [HttpGet("provinces/available")]
        public ActionResult<IEnumerable<string>> GetAvailableProvinces()
        {
            var provinces = new[] { "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo", "Mpumalanga", "North West", "Northern Cape", "Western Cape" };
            return Ok(provinces);
        }

        // POST: api/community/posts
        [HttpPost("posts")]
        public async Task<ActionResult<PostDto>> CreatePost([FromForm] CreatePostDto createPostDto, [FromForm] List<IFormFile> images)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized();
            }

            // Validate province if provided
            if (!string.IsNullOrEmpty(createPostDto.Province))
            {
                var validProvinces = new[] { "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo", "Mpumalanga", "North West", "Northern Cape", "Western Cape" };
                if (!validProvinces.Contains(createPostDto.Province))
                {
                    return BadRequest("Invalid province");
                }
            }

            var post = new Post
            {
                Id = Guid.NewGuid().ToString(),
                Title = createPostDto.Title,
                Content = createPostDto.Content,
                UserId = userId,
                Province = createPostDto.Province,
                Type = Enum.Parse<PostType>(createPostDto.Type),
                Topic = createPostDto.Topic,
                IsAnonymous = createPostDto.IsAnonymous,
                CreatedAt = DateTime.UtcNow
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            // Handle image uploads - FOLLOWING THE SAME PATTERN AS COURSE CONTROLLER
            if (images != null && images.Count > 0)
            {
                var uploadedImageUrls = await UploadImages(images, userId);

                for (int i = 0; i < uploadedImageUrls.Count; i++)
                {
                    var postImage = new PostImage
                    {
                        Id = Guid.NewGuid().ToString(),
                        PostId = post.Id,
                        ImageUrl = uploadedImageUrls[i],
                        Order = i
                    };
                    _context.PostImages.Add(postImage);
                }
                await _context.SaveChangesAsync();
            }

            // Load images for response
            await _context.Entry(post).Collection(p => p.Images).LoadAsync();

            return CreatedAtAction(nameof(GetPost), new { id = post.Id }, MapToPostDto(post, user));
        }

        // UPLOAD IMAGES METHOD - FOLLOWING THE SAME PATTERN AS COURSE CONTROLLER
        private async Task<List<string>> UploadImages(List<IFormFile> images, string userId)
        {
            var uploadedUrls = new List<string>();

            foreach (var image in images)
            {
                if (image != null && image.Length > 0)
                {
                    try
                    {
                        // Create uploads directory if it doesn't exist
                        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "posts", userId);
                        if (!Directory.Exists(uploadsDir))
                            Directory.CreateDirectory(uploadsDir);

                        // Generate unique filename
                        var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(image.FileName)}";
                        var filePath = Path.Combine(uploadsDir, fileName);

                        // Save the file
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await image.CopyToAsync(stream);
                        }

                        // Return relative URL (same as CourseController)
                        var imageUrl = $"/uploads/posts/{userId}/{fileName}";
                        uploadedUrls.Add(imageUrl);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other images
                        Console.WriteLine($"Error uploading image: {ex.Message}");
                    }
                }
            }

            return uploadedUrls;
        }

        // POST: api/community/posts/{id}/like
        [HttpPost("posts/{id}/like")]
        public async Task<ActionResult> LikePost(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized();
            }

            var post = await _context.Posts.FindAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            // Check if user already liked this post
            var existingLike = await _context.PostLikes
                .FirstOrDefaultAsync(pl => pl.PostId == id && pl.UserId == userId);

            if (existingLike != null)
            {
                // User already liked - remove the like (UNLIKE)
                _context.PostLikes.Remove(existingLike);
                post.Likes--; // DECREMENT the like count
            }
            else
            {
                // User hasn't liked - add like (LIKE)
                var postLike = new PostLike
                {
                    Id = Guid.NewGuid().ToString(),
                    PostId = id,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PostLikes.Add(postLike);
                post.Likes++; // INCREMENT the like count
            }

            await _context.SaveChangesAsync();

            // Check if current user liked this post (true if they just liked it, false if they unliked)
            var userLiked = existingLike == null;

            return Ok(new
            {
                Likes = post.Likes,
                UserLiked = userLiked
            });
        }

        // GET: api/community/posts/{id}/comments
        [HttpGet("posts/{id}/comments")]
        public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments(string id)
        {
            var comments = await _context.Comments
                .Include(c => c.Author)
                .Include(c => c.Replies)
                .Where(c => c.PostId == id && c.ParentCommentId == null)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var commentDtos = comments.Select(c => new CommentDto
            {
                Id = c.Id,
                Content = c.Content,
                AuthorId = c.UserId,
                AuthorName = $"{c.Author.FirstName} {c.Author.LastName}",
                AuthorProfilePicture = GetFullProfilePictureUrl(c.Author.ProfilePicture),
                PostId = c.PostId,
                ParentCommentId = c.ParentCommentId,
                CreatedAt = c.CreatedAt,
                Likes = c.Likes,
                ReplyCount = c.Replies.Count
            });

            return Ok(commentDtos);
        }

        [HttpGet("posts/{id}/userlike")]
        public async Task<ActionResult<bool>> GetUserLikeStatus(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Ok(false);
            }

            var userLiked = await _context.PostLikes
                .AnyAsync(pl => pl.PostId == id && pl.UserId == userId);

            return Ok(userLiked);
        }

        // POST: api/community/comments
        [HttpPost("comments")]
        public async Task<ActionResult<CommentDto>> CreateComment(CreateCommentDto createCommentDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized();
            }

            var post = await _context.Posts.FindAsync(createCommentDto.PostId);
            if (post == null)
            {
                return NotFound("Post not found");
            }

            // Validate parent comment if provided
            if (!string.IsNullOrEmpty(createCommentDto.ParentCommentId))
            {
                var parentComment = await _context.Comments.FindAsync(createCommentDto.ParentCommentId);
                if (parentComment == null)
                {
                    return NotFound("Parent comment not found");
                }
            }

            var comment = new Comment
            {
                Id = Guid.NewGuid().ToString(),
                Content = createCommentDto.Content,
                UserId = userId,
                PostId = createCommentDto.PostId,
                ParentCommentId = createCommentDto.ParentCommentId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Ok(new CommentDto
            {
                Id = comment.Id,
                Content = comment.Content,
                AuthorId = comment.UserId,
                AuthorName = $"{user.FirstName} {user.LastName}",
                AuthorProfilePicture = user.ProfilePicture,
                PostId = comment.PostId,
                ParentCommentId = comment.ParentCommentId,
                CreatedAt = comment.CreatedAt,
                Likes = comment.Likes,
                ReplyCount = 0
            });
        }

        // POST: api/community/provinces/join
        [HttpPost("provinces/join")]
        public async Task<ActionResult> JoinProvince(JoinProvinceDto joinProvinceDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized();
            }

            var validProvinces = new[] { "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo", "Mpumalanga", "North West", "Northern Cape", "Western Cape" };
            if (!validProvinces.Contains(joinProvinceDto.Province))
            {
                return BadRequest("Invalid province");
            }

            // Check if user is already a member
            var existingMembership = await _context.UserProvinces
                .FirstOrDefaultAsync(up => up.UserId == userId && up.Province == joinProvinceDto.Province);

            if (existingMembership != null)
            {
                if (!existingMembership.IsActive)
                {
                    existingMembership.IsActive = true;
                    existingMembership.JoinedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                return Ok(new { Message = "Already a member" });
            }

            var userProvince = new UserProvince
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Province = joinProvinceDto.Province,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.UserProvinces.Add(userProvince);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Joined province successfully" });
        }

        // GET: api/community/provinces/stats
        [HttpGet("provinces/stats")]
        public async Task<ActionResult<IEnumerable<ProvinceStatsDto>>> GetProvinceStats()
        {
            var provinceStats = await _context.UserProvinces
                .Where(up => up.IsActive) // Only count active members
                .GroupBy(up => up.Province)
                .Select(g => new ProvinceStatsDto
                {
                    Province = g.Key,
                    MemberCount = g.Count(),
                    PostCount = _context.Posts.Count(p => p.Province == g.Key),
                    ActiveDiscussions = _context.Posts.Count(p => p.Province == g.Key && p.Comments.Any(c => c.CreatedAt > DateTime.UtcNow.AddDays(-7)))
                })
                .ToListAsync();
             
            return Ok(provinceStats);
        }

        // GET: api/community/stats
        [HttpGet("stats")]
        public async Task<ActionResult> GetCommunityStats()
        {
            var totalMembers = await _context.Users.CountAsync();
            var activeDiscussions = await _context.Posts
                .CountAsync(p => p.Comments.Any(c => c.CreatedAt > DateTime.UtcNow.AddDays(-7)));
            var totalPosts = await _context.Posts.CountAsync();
            var answeredQuestions = await _context.Posts
                .CountAsync(p => p.Type == PostType.Question && p.Comments.Any());

            return Ok(new
            {
                TotalMembers = totalMembers,
                ActiveDiscussions = activeDiscussions,
                TotalPosts = totalPosts,
                AnsweredQuestions = answeredQuestions
            });
        }

        // ADD FILE UPLOAD ENDPOINT LIKE IN COURSE CONTROLLER
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null)
                {
                    return Unauthorized();
                }

                // Create uploads directory if it doesn't exist
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "posts", userId);
                if (!Directory.Exists(uploadsDir))
                    Directory.CreateDirectory(uploadsDir);

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsDir, fileName);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Return the relative URL (same as CourseController)
                var fileUrl = $"/uploads/posts/{userId}/{fileName}";
                return Ok(new { url = fileUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // GET: api/community/provinces/joined
        [HttpGet("provinces/joined")]
        public async Task<ActionResult<IEnumerable<string>>> GetJoinedProvinces()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized();
            }

            var joinedProvinces = await _context.UserProvinces
                .Where(up => up.UserId == userId && up.IsActive)
                .Select(up => up.Province)
                .ToListAsync();

            return Ok(joinedProvinces);
        }

        // POST: api/community/provinces/leave
        [HttpPost("provinces/leave")]
        public async Task<ActionResult> LeaveProvince(JoinProvinceDto leaveProvinceDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized();
            }

            var validProvinces = new[] { "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo", "Mpumalanga", "North West", "Northern Cape", "Western Cape" };
            if (!validProvinces.Contains(leaveProvinceDto.Province))
            {
                return BadRequest("Invalid province");
            }

            // Find the user's membership
            var membership = await _context.UserProvinces
                .FirstOrDefaultAsync(up => up.UserId == userId && up.Province == leaveProvinceDto.Province);

            if (membership != null)
            {
                if (membership.IsActive)
                {
                    membership.IsActive = false;
                    membership.LeftAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                return Ok(new { Message = "Left province successfully" });
            }

            return NotFound("Membership not found");
        }
    }
}