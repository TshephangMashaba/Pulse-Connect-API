using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pulse_Connect_API.Data;
using Pulse_Connect_API.Models;

namespace Pulse_Connect_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AnalyticsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AnalyticsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAnalyticsData()
        {
            try
            {
                var analyticsData = new
                {
                    Summary = await GetSummaryStats(),
                    UserEngagement = await GetUserEngagementData(),
                    CourseCompletion = await GetCourseCompletionData(),
                    GeographicDistribution = await GetGeographicDistributionData(),
                    HealthTopicEngagement = await GetHealthTopicEngagementData(),
                    DeviceUsage = await GetDeviceUsageData(),
                    LanguagePreference = await GetLanguagePreferenceData(),
                    RecentActivity = await GetRecentActivityData(),
                    PopularCourses = await GetPopularCoursesData()
                };

                return Ok(analyticsData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("recent-activity")]
        public async Task<IActionResult> GetRecentActivity()
        {
            try
            {
                var data = await GetRecentActivityData();
                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("popular-courses")]
        public async Task<IActionResult> GetPopularCourses()
        {
            try
            {
                var data = await GetPopularCoursesData();
                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private async Task<object> GetRecentActivityData()
        {
            // Get recent user registrations
            var recentUsers = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .Select(u => new
                {
                    Type = "user_registered",
                    Message = $"{u.FirstName} {u.LastName} registered",
                    Time = DateTime.UtcNow - u.CreatedAt
                })
                .ToListAsync();

            // Get recent certificates
            var recentCertificates = await _context.Certificates
                .Include(c => c.User)
                .Include(c => c.Course)
                .OrderByDescending(c => c.IssueDate)
                .Take(5)
                .Select(c => new
                {
                    Type = "certificate_issued",
                    Message = $"{c.Course.Title} certificate issued to {c.User.FirstName}",
                    Time = DateTime.UtcNow - c.IssueDate
                })
                .ToListAsync();

            // Get recent posts
            var recentPosts = await _context.Posts
                .Include(p => p.Author)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .Select(p => new
                {
                    Type = "community_post",
                    Message = $"{p.Author.FirstName} posted in {p.Topic}",
                    Time = DateTime.UtcNow - p.CreatedAt
                })
                .ToListAsync();

            // Combine and format
            var activities = recentUsers
                .Concat(recentCertificates)
                .Concat(recentPosts)
                .OrderByDescending(a => a.Time)
                .Take(10)
                .Select(a => new
                {
                    a.Type,
                    a.Message,
                    Time = FormatTimeSpan(a.Time)
                })
                .ToList();

            return activities;
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours < 1) return $"{(int)timeSpan.TotalMinutes} minutes ago";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours} hours ago";
            return $"{(int)timeSpan.TotalDays} days ago";
        }

        private async Task<object> GetPopularCoursesData()
        {
            var courses = await _context.Courses
                .Include(c => c.Enrollments)
                .OrderByDescending(c => c.Enrollments.Count)
                .Take(5)
                .Select(c => new
                {
                    Title = c.Title,
                    Enrollments = c.Enrollments.Count,
                    CompletionRate = c.Enrollments.Count > 0 ?
                        (double)c.Enrollments.Count(e => e.IsCompleted) / c.Enrollments.Count * 100 : 0
                })
                .ToListAsync();

            return courses;
        }

        [HttpGet("user-engagement")]
        public async Task<IActionResult> GetUserEngagement()
        {
            try
            {
                var data = await GetUserEngagementData();
                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("course-completion")]
        public async Task<IActionResult> GetCourseCompletion()
        {
            try
            {
                var data = await GetCourseCompletionData();
                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("geographic-distribution")]
        public async Task<IActionResult> GetGeographicDistribution()
        {
            try
            {
                var data = await GetGeographicDistributionData();
                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("health-topic-engagement")]
        public async Task<IActionResult> GetHealthTopicEngagement()
        {
            try
            {
                var data = await GetHealthTopicEngagementData();
                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private async Task<object> GetSummaryStats()
        {
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users
                .Where(u => _context.Enrollments.Any(e => e.UserId == u.Id &&
                    e.EnrollmentDate > DateTime.UtcNow.AddDays(-30)))
                .CountAsync();
            var coursesCompleted = await _context.Enrollments
                .Where(e => e.IsCompleted)
                .CountAsync();
            var certificatesIssued = await _context.Certificates.CountAsync();
            var forumPosts = await _context.Posts.CountAsync();

            var completionRate = totalUsers > 0 ?
                (double)coursesCompleted / totalUsers * 100 : 0;

            return new
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                CoursesCompleted = coursesCompleted,
                CertificatesIssued = certificatesIssued,
                ForumPosts = forumPosts,
                AvgCompletionRate = Math.Round(completionRate, 2)
            };
        }

        private async Task<object> GetUserEngagementData()
        {
            // Last 7 days user activity
            var dates = Enumerable.Range(0, 7)
                .Select(i => DateTime.UtcNow.AddDays(-i).Date)
                .Reverse()
                .ToList();

            var dailyActivity = new List<int>();

            foreach (var date in dates)
            {
                var count = await _context.Enrollments
                    .Where(e => e.EnrollmentDate.Date == date)
                    .CountAsync();
                dailyActivity.Add(count);
            }

            return new
            {
                Labels = dates.Select(d => d.ToString("MMM dd")).ToArray(),
                Values = dailyActivity.ToArray()
            };
        }

        private async Task<object> GetCourseCompletionData()
        {
            var courses = await _context.Courses
                .Include(c => c.Enrollments)
                .Take(5)
                .ToListAsync();

            var completionRates = courses.Select(c =>
            {
                var totalEnrollments = c.Enrollments.Count;
                var completedEnrollments = c.Enrollments.Count(e => e.IsCompleted);
                return totalEnrollments > 0 ?
                    Math.Round((double)completedEnrollments / totalEnrollments * 100, 2) : 0;
            }).ToArray();

            return new
            {
                Labels = courses.Select(c => c.Title).ToArray(),
                Values = completionRates
            };
        }

        private async Task<object> GetGeographicDistributionData()
        {
            var provinces = new[] { "Gauteng", "Limpopo", "Eastern Cape", "KwaZulu-Natal",
                "Western Cape", "North West", "Free State", "Mpumalanga", "Northern Cape" };

            var distribution = new List<int>();

            foreach (var province in provinces)
            {
                var count = await _context.UserProvinces
                    .Where(up => up.Province == province && up.IsActive)
                    .CountAsync();
                distribution.Add(count);
            }

            return new
            {
                Labels = provinces,
                Values = distribution.ToArray()
            };
        }

        private async Task<object> GetHealthTopicEngagementData()
        {
            var topics = new[] { "HIV/AIDS", "Mental Health", "First Aid", "Hygiene", "Safety Practices" };
            var engagement = new List<int>();

            foreach (var topic in topics)
            {
                var count = await _context.Posts
                    .Where(p => p.Topic == topic)
                    .CountAsync();
                engagement.Add(count);
            }

            return new
            {
                Labels = topics,
                Values = engagement.ToArray()
            };
        }

        private async Task<object> GetDeviceUsageData()
        {
            // Simulating device usage data (in a real app, you'd track this)
            return new
            {
                Labels = new[] { "Mobile App", "Mobile Web", "Desktop" },
                Values = new[] { 75, 20, 5 }
            };
        }

        private async Task<object> GetLanguagePreferenceData()
        {
            // Simulating language preference data
            return new
            {
                Labels = new[] { "IsiZulu", "IsiXhosa", "Sesotho", "English" },
                Values = new[] { 45, 25, 15, 15 }
            };
        }
    }
}