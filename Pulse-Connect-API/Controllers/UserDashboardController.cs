
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pulse_Connect_API.DTOs;
using Pulse_Connect_API.Models;
using System.Security.Claims;

namespace Pulse_Connect_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class UserDashboardController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;

        public UserDashboardController(AppDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: api/userdashboard/stats
        [HttpGet("stats")]
        public async Task<ActionResult<DashboardStatsDTO>> GetDashboardStats()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Invalid or missing user ID in token");
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found");
                }

                var stats = new DashboardStatsDTO
                {
                    TotalEnrollments = await _context.Enrollments
                        .Where(e => e.UserId == userId)
                        .CountAsync(),

                    CompletedCourses = await _context.Enrollments
                        .Where(e => e.UserId == userId && e.IsCompleted)
                        .CountAsync(),

                    InProgressCourses = await _context.Enrollments
                        .Where(e => e.UserId == userId && !e.IsCompleted)
                        .CountAsync(),

                    TotalCertificates = await _context.Certificates
                        .Where(c => c.UserId == userId)
                        .CountAsync(),

                    TotalXpPoints = await CalculateXpPoints(userId),
                    TotalBadges = await CalculateBadgeCount(userId),
                    WeeklyProgress = await CalculateWeeklyProgress(userId),
                    AverageCompletionRate = await CalculateAverageCompletionRate(userId)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                // Log the full exception for debugging
                Console.WriteLine($"Error in GetDashboardStats: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    Message = "Internal server error occurred while retrieving dashboard statistics",
                    Details = ex.Message
                });
            }
        }

        // GET: api/userdashboard/course-progress
        [HttpGet("course-progress")]
        public async Task<ActionResult<IEnumerable<CourseProgressDTO>>> GetCourseProgress()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Invalid or missing user ID in token");
                }

                var courseProgress = await _context.Enrollments
                    .Include(e => e.Course)
                        .ThenInclude(c => c.Chapters)
                    .Include(e => e.ChapterProgress)
                    .Where(e => e.UserId == userId && e.Course != null && !e.IsCompleted) // Filter out completed courses
                    .Select(e => new CourseProgressDTO
                    {
                        CourseId = e.CourseId,
                        CourseTitle = e.Course != null ? e.Course.Title : "Unknown Course",
                        ProgressPercentage = CalculateProgressPercentage(e),
                        CompletedChapters = e.ChapterProgress.Count(p => p.IsCompleted),
                        TotalChapters = e.Course != null && e.Course.Chapters != null ? e.Course.Chapters.Count : 0,
                        LastAccessed = GetLastAccessedDate(e),
                        EstimatedRemainingTime = CalculateEstimatedRemainingTime(e),
                        IsCompleted = e.IsCompleted // Add this property to track completion status
                    })
                    .Where(cp => cp.TotalChapters > 0 && cp.ProgressPercentage < 100) // Filter out 100% completed courses
                    .OrderByDescending(cp => cp.LastAccessed)
                    .ToListAsync();

                return Ok(courseProgress);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCourseProgress: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    Message = "Internal server error occurred while retrieving course progress",
                    Details = ex.Message
                });
            }
        }

        // GET: api/userdashboard/achievements
        [HttpGet("achievements")]
        public async Task<ActionResult<AchievementsDTO>> GetAchievements()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Invalid or missing user ID in token");
                }

                var achievements = new AchievementsDTO
                {
                    TotalCertificates = await _context.Certificates
                        .Where(c => c.UserId == userId)
                        .CountAsync(),

                    PerfectScores = await _context.TestAttempts
                        .Include(ta => ta.Enrollment)
                        .Where(ta => ta.Enrollment != null && ta.Enrollment.UserId == userId && ta.Score == 100)
                        .CountAsync(),

                    CoursesCompleted = await _context.Enrollments
                        .Where(e => e.UserId == userId && e.IsCompleted)
                        .CountAsync(),

                    ChaptersCompleted = await _context.UserChapterProgresses
                        .Include(p => p.Enrollment)
                        .Where(p => p.Enrollment != null && p.Enrollment.UserId == userId && p.IsCompleted)
                        .CountAsync(),

                    StreakDays = await CalculateLearningStreak(userId),
                    TotalLearningHours = await CalculateTotalLearningHours(userId)
                };

                // Calculate badges based on achievements
                achievements.Badges = CalculateBadges(achievements);

                return Ok(achievements);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAchievements: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    Message = "Internal server error occurred while retrieving achievements",
                    Details = ex.Message
                });
            }
        }

        // GET: api/userdashboard/recent-activity
        [HttpGet("recent-activity")]
        public async Task<ActionResult<IEnumerable<RecentActivityDTO>>> GetRecentActivity()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Invalid or missing user ID in token");
                }

                // Get recent test attempts with proper null checks
                var testAttempts = await _context.TestAttempts
                    .Include(ta => ta.Test)
                    .Include(ta => ta.Enrollment)
                        .ThenInclude(e => e.Course)
                    .Where(ta => ta.Enrollment != null && ta.Enrollment.UserId == userId && ta.Test != null)
                    .OrderByDescending(ta => ta.AttemptDate)
                    .Take(5)
                    .Select(ta => new RecentActivityDTO
                    {
                        Type = "Test Completed",
                        Title = ta.Test.Title,
                        Description = $"Scored {ta.Score}% on {ta.Test.Title}",
                        Timestamp = ta.AttemptDate,
                        CourseId = ta.Enrollment.CourseId,
                        CourseTitle = ta.Enrollment.Course != null ? ta.Enrollment.Course.Title : "Unknown Course",
                        IsSuccess = ta.IsPassed
                    })
                    .ToListAsync();

                // Get recent chapter completions with proper null checks
                var chapterCompletions = await _context.UserChapterProgresses
                    .Include(p => p.Chapter)
                    .Include(p => p.Enrollment)
                        .ThenInclude(e => e.Course)
                    .Where(p => p.Enrollment != null && p.Enrollment.UserId == userId && p.IsCompleted && p.Chapter != null)
                    .OrderByDescending(p => p.CompletedDate)
                    .Take(5)
                    .Select(p => new RecentActivityDTO
                    {
                        Type = "Chapter Completed",
                        Title = p.Chapter.Title,
                        Description = $"Completed chapter: {p.Chapter.Title}",
                        Timestamp = p.CompletedDate ?? DateTime.UtcNow,
                        CourseId = p.Enrollment.CourseId,
                        CourseTitle = p.Enrollment.Course != null ? p.Enrollment.Course.Title : "Unknown Course",
                        IsSuccess = true
                    })
                    .ToListAsync();

                // Get recent enrollments with proper null checks
                var recentEnrollments = await _context.Enrollments
                    .Include(e => e.Course)
                    .Where(e => e.UserId == userId && e.Course != null)
                    .OrderByDescending(e => e.EnrollmentDate)
                    .Take(5)
                    .Select(e => new RecentActivityDTO
                    {
                        Type = "Course Enrolled",
                        Title = e.Course.Title,
                        Description = $"Enrolled in {e.Course.Title}",
                        Timestamp = e.EnrollmentDate,
                        CourseId = e.CourseId,
                        CourseTitle = e.Course.Title,
                        IsSuccess = true
                    })
                    .ToListAsync();

                // Combine and order all activities
                var allActivities = testAttempts
                    .Concat(chapterCompletions)
                    .Concat(recentEnrollments)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(10)
                    .ToList();

                return Ok(allActivities);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetRecentActivity: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    Message = "Internal server error occurred while retrieving recent activity",
                    Details = ex.Message
                });
            }
        }

        // GET: api/userdashboard/recommended-courses
        [HttpGet("recommended-courses")]
        public async Task<ActionResult<IEnumerable<CourseDTO>>> GetRecommendedCourses()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Invalid or missing user ID in token");
                }

                // Get user's enrolled course IDs
                var userCourseIds = await _context.Enrollments
                    .Where(e => e.UserId == userId)
                    .Select(e => e.CourseId)
                    .ToListAsync();

                // Get recommended courses (not enrolled by user)
                var recommendedCourses = await _context.Courses
                    .Include(c => c.Instructor)
                    .Include(c => c.Enrollments)
                    .Include(c => c.Chapters)
                    .Where(c => !userCourseIds.Contains(c.Id) && c.Instructor != null)
                    .OrderByDescending(c => c.Enrollments.Count)
                    .Take(6)
                    .Select(c => new CourseDTO
                    {
                        Id = c.Id,
                        Title = c.Title,
                        Description = c.Description,
                        InstructorId = c.InstructorId,
                        InstructorName = $"{c.Instructor.FirstName} {c.Instructor.LastName}",
                        ThumbnailUrl = c.ThumbnailUrl,
                        EstimatedDuration = c.EstimatedDuration,
                        EnrollmentCount = c.Enrollments.Count,
                        ChapterCount = c.Chapters.Count
                    })
                    .ToListAsync();

                return Ok(recommendedCourses);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetRecommendedCourses: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    Message = "Internal server error occurred while retrieving recommended courses",
                    Details = ex.Message
                });
            }
        }

        // Helper methods with robust error handling
        private int CalculateProgressPercentage(Enrollment enrollment)
        {
            try
            {
                if (enrollment.Course == null || enrollment.Course.Chapters == null || enrollment.Course.Chapters.Count == 0)
                    return 0;

                var completedChapters = enrollment.ChapterProgress.Count(p => p.IsCompleted);
                return (int)Math.Round((double)completedChapters / enrollment.Course.Chapters.Count * 100);
            }
            catch
            {
                return 0;
            }
        }

        private DateTime GetLastAccessedDate(Enrollment enrollment)
        {
            try
            {
                var lastCompleted = enrollment.ChapterProgress
                    .Where(p => p.IsCompleted && p.CompletedDate.HasValue)
                    .OrderByDescending(p => p.CompletedDate)
                    .Select(p => p.CompletedDate.Value)
                    .FirstOrDefault();

                return lastCompleted != default ? lastCompleted : enrollment.EnrollmentDate;
            }
            catch
            {
                return enrollment.EnrollmentDate;
            }
        }

        private async Task<int> CalculateXpPoints(string userId)
        {
            try
            {
                var certificates = await _context.Certificates
                    .Where(c => c.UserId == userId)
                    .CountAsync();

                var completedCourses = await _context.Enrollments
                    .Where(e => e.UserId == userId && e.IsCompleted)
                    .CountAsync();

                var perfectScores = await _context.TestAttempts
                    .Include(ta => ta.Enrollment)
                    .Where(ta => ta.Enrollment != null && ta.Enrollment.UserId == userId && ta.Score == 100)
                    .CountAsync();

                return (certificates * 250) + (completedCourses * 100) + (perfectScores * 50);
            }
            catch
            {
                return 0;
            }
        }

        private async Task<int> CalculateBadgeCount(string userId)
        {
            try
            {
                var achievements = new AchievementsDTO
                {
                    TotalCertificates = await _context.Certificates
                        .Where(c => c.UserId == userId)
                        .CountAsync(),

                    PerfectScores = await _context.TestAttempts
                        .Include(ta => ta.Enrollment)
                        .Where(ta => ta.Enrollment != null && ta.Enrollment.UserId == userId && ta.Score == 100)
                        .CountAsync(),

                    CoursesCompleted = await _context.Enrollments
                        .Where(e => e.UserId == userId && e.IsCompleted)
                        .CountAsync(),

                    ChaptersCompleted = await _context.UserChapterProgresses
                        .Include(p => p.Enrollment)
                        .Where(p => p.Enrollment != null && p.Enrollment.UserId == userId && p.IsCompleted)
                        .CountAsync(),

                    StreakDays = await CalculateLearningStreak(userId),
                    TotalLearningHours = await CalculateTotalLearningHours(userId)
                };

                return CalculateBadges(achievements).Count;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<double> CalculateWeeklyProgress(string userId)
        {
            try
            {
                var startOfWeek = DateTime.UtcNow.AddDays(-7);

                var chaptersCompletedThisWeek = await _context.UserChapterProgresses
                    .Include(p => p.Enrollment)
                    .Where(p => p.Enrollment != null && p.Enrollment.UserId == userId &&
                               p.IsCompleted &&
                               p.CompletedDate >= startOfWeek)
                    .CountAsync();

                var totalChapters = await _context.Enrollments
                    .Include(e => e.Course)
                    .Where(e => e.UserId == userId && !e.IsCompleted && e.Course != null)
                    .SumAsync(e => e.Course.Chapters != null ? e.Course.Chapters.Count : 0);

                return totalChapters > 0 ? (double)chaptersCompletedThisWeek / totalChapters * 100 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<double> CalculateAverageCompletionRate(string userId)
        {
            try
            {
                var enrollments = await _context.Enrollments
                    .Include(e => e.Course)
                    .Include(e => e.ChapterProgress)
                    .Where(e => e.UserId == userId && e.Course != null)
                    .ToListAsync();

                if (!enrollments.Any()) return 0;

                var totalCompletionRate = enrollments.Sum(e =>
                {
                    if (e.Course.Chapters == null || e.Course.Chapters.Count == 0)
                        return 0;

                    return (double)e.ChapterProgress.Count(p => p.IsCompleted) / e.Course.Chapters.Count * 100;
                });

                return totalCompletionRate / enrollments.Count;
            }
            catch
            {
                return 0;
            }
        }

        private string CalculateEstimatedRemainingTime(Enrollment enrollment)
        {
            try
            {
                if (enrollment.Course == null || enrollment.Course.Chapters == null || enrollment.Course.Chapters.Count == 0)
                    return "N/A";

                var completedChapters = enrollment.ChapterProgress.Count(p => p.IsCompleted);
                var remainingChapters = enrollment.Course.Chapters.Count - completedChapters;

                if (enrollment.Course.EstimatedDuration > 0)
                {
                    var avgTimePerChapter = enrollment.Course.EstimatedDuration / enrollment.Course.Chapters.Count;
                    var remainingHours = remainingChapters * avgTimePerChapter;

                    return remainingHours < 1 ?
                        $"{(int)(remainingHours * 60)} minutes" :
                        $"{remainingHours:0.#} hours";
                }

                return $"{remainingChapters} chapters remaining";
            }
            catch
            {
                return "N/A";
            }
        }

        private async Task<int> CalculateLearningStreak(string userId)
        {
            try
            {
                var completionDates = await _context.UserChapterProgresses
                    .Include(p => p.Enrollment)
                    .Where(p => p.Enrollment != null && p.Enrollment.UserId == userId &&
                               p.IsCompleted && p.CompletedDate.HasValue)
                    .Select(p => p.CompletedDate.Value.Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToListAsync();

                if (!completionDates.Any()) return 0;

                var streak = 1;
                var currentDate = DateTime.UtcNow.Date;

                for (int i = 0; i < completionDates.Count - 1; i++)
                {
                    if ((completionDates[i] - completionDates[i + 1]).Days == 1)
                    {
                        streak++;
                    }
                    else if (completionDates[i] != completionDates[i + 1])
                    {
                        break;
                    }
                }

                return streak;
            }
            catch
            {
                return 0;
            }
        }
        // Add these endpoints to UserDashboardController.cs
        [HttpGet("notifications")]
        public async Task<ActionResult<IEnumerable<NotificationDTO>>> GetNotifications()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Invalid or missing user ID in token");
                }

                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new NotificationDTO
                    {
                        Id = n.Id,
                        Title = n.Title,
                        Message = n.Message,
                        Type = n.Type,
                        IsRead = n.IsRead,
                        CreatedAt = n.CreatedAt,
                        RelatedEntityId = n.RelatedEntityId,
                        RelatedEntityType = n.RelatedEntityType
                    })
                    .ToListAsync();

                return Ok(notifications);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetNotifications: {ex.Message}");
                return StatusCode(500, new
                {
                    Message = "Internal server error occurred while retrieving notifications",
                    Details = ex.Message
                });
            }
        }

        [HttpPost("notifications/mark-as-read/{id}")]
        public async Task<IActionResult> MarkNotificationAsRead(string id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Invalid or missing user ID in token");
                }

                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (notification == null)
                {
                    return NotFound("Notification not found");
                }

                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                _context.Notifications.Update(notification);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Notification marked as read" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MarkNotificationAsRead: {ex.Message}");
                return StatusCode(500, new
                {
                    Message = "Internal server error occurred",
                    Details = ex.Message
                });
            }
        }

        [HttpPost("notifications/mark-all-read")]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Invalid or missing user ID in token");
                }

                var unreadNotifications = await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.UtcNow;
                }

                if (unreadNotifications.Any())
                {
                    _context.Notifications.UpdateRange(unreadNotifications);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { Message = "All notifications marked as read" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MarkAllNotificationsAsRead: {ex.Message}");
                return StatusCode(500, new
                {
                    Message = "Internal server error occurred",
                    Details = ex.Message
                });
            }
        }

        [HttpGet("notifications/unread-count")]
        public async Task<ActionResult<int>> GetUnreadNotificationCount()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Invalid or missing user ID in token");
                }

                var count = await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .CountAsync();

                return Ok(count);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetUnreadNotificationCount: {ex.Message}");
                return StatusCode(500, new
                {
                    Message = "Internal server error occurred",
                    Details = ex.Message
                });
            }
        }

       


        private async Task<double> CalculateTotalLearningHours(string userId)
        {
            try
            {
                var totalMinutes = await _context.UserChapterProgresses
                    .Include(p => p.Enrollment)
                    .Where(p => p.Enrollment != null && p.Enrollment.UserId == userId &&
                               p.IsCompleted && p.TimeSpent != TimeSpan.Zero)
                    .SumAsync(p => (double?)p.TimeSpent.TotalMinutes) ?? 0;

                return Math.Round(totalMinutes / 60, 1);
            }
            catch
            {
                return 0;
            }
        }

        private List<BadgeDTO> CalculateBadges(AchievementsDTO achievements)
        {
            var badges = new List<BadgeDTO>();

            if (achievements.TotalCertificates >= 1)
                badges.Add(new BadgeDTO { Name = "First Certificate", Description = "Earned your first certificate", Icon = "🏆" });

            if (achievements.TotalCertificates >= 3)
                badges.Add(new BadgeDTO { Name = "Certified Learner", Description = "Earned 3 certificates", Icon = "🎓" });

            if (achievements.PerfectScores >= 1)
                badges.Add(new BadgeDTO { Name = "Perfect Score", Description = "Scored 100% on a test", Icon = "💯" });

            if (achievements.CoursesCompleted >= 5)
                badges.Add(new BadgeDTO { Name = "Course Master", Description = "Completed 5 courses", Icon = "📚" });

            if (achievements.StreakDays >= 7)
                badges.Add(new BadgeDTO { Name = "Weekly Streak", Description = "7-day learning streak", Icon = "🔥" });

            if (achievements.TotalLearningHours >= 10)
                badges.Add(new BadgeDTO { Name = "Dedicated Learner", Description = "10+ hours of learning", Icon = "⏰" });

            return badges;
        }
    }


}


  
// DTO Classes
public class DashboardStatsDTO
    {
        public int TotalEnrollments { get; set; }
        public int CompletedCourses { get; set; }
        public int InProgressCourses { get; set; }
        public int TotalCertificates { get; set; }
        public int TotalXpPoints { get; set; }
        public int TotalBadges { get; set; }
        public double WeeklyProgress { get; set; }
        public double AverageCompletionRate { get; set; }
    }

    public class RecentActivityDTO
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string CourseId { get; set; }
        public string CourseTitle { get; set; }
        public bool IsSuccess { get; set; }
    }

public class CourseProgressDTO
{
    public string CourseId { get; set; }
    public string CourseTitle { get; set; }
    public int ProgressPercentage { get; set; }
    public int CompletedChapters { get; set; }
    public int TotalChapters { get; set; }
    public DateTime LastAccessed { get; set; }
    public string EstimatedRemainingTime { get; set; }
    public bool IsCompleted { get; set; } // Add this property
}

public class NotificationDTO
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public string Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RelatedEntityId { get; set; }
    public string RelatedEntityType { get; set; }
}

public class AchievementsDTO
    {
        public int TotalCertificates { get; set; }
        public int PerfectScores { get; set; }
        public int CoursesCompleted { get; set; }
        public int ChaptersCompleted { get; set; }
        public int StreakDays { get; set; }
        public double TotalLearningHours { get; set; }
        public List<BadgeDTO> Badges { get; set; }
    }

    public class BadgeDTO
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
    }

    public class LearningTrendsDTO
    {
        public List<DailyProgressDTO> DailyProgress { get; set; }
        public List<CourseCompletionDTO> CourseCompletionRates { get; set; }
        public List<CategoryTimeDTO> TimeSpentByCategory { get; set; }
        public List<TestPerformanceDTO> TestPerformance { get; set; }
    }

    public class DailyProgressDTO
    {
        public DateTime Date { get; set; }
        public int ChaptersCompleted { get; set; }
        public double MinutesStudied { get; set; }
    }

    public class CourseCompletionDTO
    {
        public string CourseId { get; set; }
        public string CourseTitle { get; set; }
        public double CompletionRate { get; set; }
        public int TotalChapters { get; set; }
        public int CompletedChapters { get; set; }
    }

    public class CategoryTimeDTO
    {
        public string Category { get; set; }
        public double TotalMinutes { get; set; }
    }

    public class TestPerformanceDTO
    {
        public string CourseName { get; set; }
        public double AverageScore { get; set; }
        public int AttemptCount { get; set; }
        public double BestScore { get; set; }
    }
