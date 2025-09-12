using FluentEmail.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pulse_Connect_API.DTO;
using Pulse_Connect_API.DTOs;
using Pulse_Connect_API.Models;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Security.Claims;
using System.Text;
using QuestPDF.Fluent;



namespace Pulse_Connect_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CertificatesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IFluentEmail _emailSender;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger _logger;

        public CertificatesController(
            AppDbContext context,
            UserManager<User> userManager,
            IFluentEmail emailSender,
            IWebHostEnvironment environment,
            ILogger<CertificatesController> logger)
     

        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
            _environment = environment;
             _logger = logger;
        }

        // GET: api/certificates/my-certificates
        // GET: api/certificates/my-certificates
        [HttpGet("my-certificates")]
        public async Task<ActionResult<IEnumerable<CertificateDTO>>> GetMyCertificates()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid or missing user ID in token");
            }

            // First get all certificates for the user
            var allCertificates = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.User)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            // Then group by course and select the certificate with highest score
            var bestCertificates = allCertificates
                .GroupBy(c => c.CourseId)
                .Select(g => g.OrderByDescending(c => c.Score)
                             .ThenByDescending(c => c.IssueDate)
                             .First())
                .Select(c => new CertificateDTO
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    CourseId = c.CourseId,
                    CourseTitle = c.Course.Title,
                    UserName = $"{c.User.FirstName} {c.User.LastName}",
                    CertificateNumber = c.CertificateNumber,
                    IssueDate = c.IssueDate,
                    Score = c.Score,
                    DownloadUrl = c.DownloadUrl,
                    IsEmailed = c.IsEmailed
                })
                .OrderByDescending(c => c.IssueDate)
                .ToList();

            return Ok(bestCertificates);
        }

        // GET: api/certificates/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<CertificateDTO>> GetCertificate(string id)
        {
            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (certificate == null)
            {
                return NotFound("Certificate not found");
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (certificate.UserId != userId && !User.IsInRole("Admin"))
            {
                return Forbid("You don't have permission to view this certificate");
            }

            var certificateDto = new CertificateDTO
            {
                Id = certificate.Id,
                UserId = certificate.UserId,
                CourseId = certificate.CourseId,
                CourseTitle = certificate.Course.Title,
                UserName = $"{certificate.User.FirstName} {certificate.User.LastName}",
                CertificateNumber = certificate.CertificateNumber,
                IssueDate = certificate.IssueDate,
                Score = certificate.Score,
                DownloadUrl = certificate.DownloadUrl,
                IsEmailed = certificate.IsEmailed
            };

            return Ok(certificateDto);
        }

        // POST: api/certificates/generate
        [HttpPost("generate")]
        public async Task<ActionResult<CertificateDTO>> GenerateCertificate([FromBody] GenerateCertificateDTO generateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var testAttempt = await _context.TestAttempts
                .Include(ta => ta.Enrollment)
                .ThenInclude(e => e.Course)
                .Include(ta => ta.Enrollment)
                .ThenInclude(e => e.User)
                .FirstOrDefaultAsync(ta => ta.Id == generateDto.TestAttemptId);

            if (testAttempt == null)
            {
                return NotFound("Test attempt not found");
            }

            if (!testAttempt.IsPassed)
            {
                return BadRequest("Cannot generate certificate for a failed test attempt");
            }

            // Check if certificate already exists
            var existingCertificate = await _context.Certificates
                .FirstOrDefaultAsync(c => c.TestAttemptId == generateDto.TestAttemptId);

            if (existingCertificate != null)
            {
                return Conflict("Certificate already exists for this test attempt");
            }

            // Generate certificate
            var certificate = await CreateCertificate(testAttempt);

            if (generateDto.SendEmail)
            {
                await SendCertificateEmail(certificate);
            }

            var certificateDto = new CertificateDTO
            {
                Id = certificate.Id,
                UserId = certificate.UserId,
                CourseId = certificate.CourseId,
                CourseTitle = testAttempt.Enrollment.Course.Title,
                UserName = $"{testAttempt.Enrollment.User.FirstName} {testAttempt.Enrollment.User.LastName}",
                CertificateNumber = certificate.CertificateNumber,
                IssueDate = certificate.IssueDate,
                Score = certificate.Score,
                DownloadUrl = certificate.DownloadUrl,
                IsEmailed = certificate.IsEmailed
            };

            return CreatedAtAction(nameof(GetCertificate), new { id = certificate.Id }, certificateDto);
        }

        // POST: api/certificates/{id}/email
        [HttpPost("{id}/email")]
        public async Task<IActionResult> SendCertificateEmail(string id)
        {
            var certificate = await _context.Certificates
                .Include(c => c.User)
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (certificate == null)
            {
                return NotFound("Certificate not found");
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (certificate.UserId != userId && !User.IsInRole("Admin"))
            {
                return Forbid("You don't have permission to email this certificate");
            }

            await SendCertificateEmail(certificate);

            return Ok(new { message = "Certificate email sent successfully" });
        }

        // GET: api/certificates/{id}/download

        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadCertificate(string id)
        {
            var certificate = await _context.Certificates
                .Include(c => c.User)
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (certificate == null)
            {
                return NotFound("Certificate not found");
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (certificate.UserId != userId && !User.IsInRole("Admin"))
            {
                return Forbid("You don't have permission to download this certificate");
            }

            // Return certificate data for frontend generation
            return Ok(new
            {
                UserName = $"{certificate.User.FirstName} {certificate.User.LastName}",
                CourseTitle = certificate.Course.Title,
                Score = certificate.Score,
                CertificateNumber = certificate.CertificateNumber,
                IssueDate = certificate.IssueDate
            });
        }

        private async Task<Certificate> CreateCertificate(TestAttempt testAttempt)
        {
            var certificateNumber = GenerateCertificateNumber();

            var certificate = new Certificate
            {
                UserId = testAttempt.Enrollment.UserId,
                CourseId = testAttempt.Enrollment.CourseId,
                TestAttemptId = testAttempt.Id,
                CertificateNumber = certificateNumber,
                Score = testAttempt.Score,
                IssueDate = DateTime.UtcNow,
                DownloadUrl = $"{Request.Scheme}://{Request.Host}/api/certificates/{certificateNumber}/download"
            };

            _context.Certificates.Add(certificate);
            await _context.SaveChangesAsync();

            return certificate;
        }

        private string GenerateCertificateNumber()
        {
            return $"PC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
        }

        private async Task SendCertificateEmail(Certificate certificate)
        {
            var user = await _userManager.FindByIdAsync(certificate.UserId);
            var course = await _context.Courses.FindAsync(certificate.CourseId);

            if (user == null || course == null) return;

            var emailBody = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; }}
                        .certificate-info {{ background: #f8f9fa; padding: 20px; border-radius: 10px; }}
                        .button {{ display: inline-block; padding: 12px 24px; background: #10b981; color: white; text-decoration: none; border-radius: 5px; }}
                    </style>
                </head>
                <body>
                    <h2>🎉 Congratulations on Your Achievement!</h2>
                    <p>Dear {user.FirstName},</p>
                    <p>We're thrilled to inform you that you've successfully completed the course <strong>{course.Title}</strong> with a score of <strong>{certificate.Score}%</strong>!</p>
                    
                    <div class='certificate-info'>
                        <h3>Your Certificate Details:</h3>
                        <p><strong>Certificate Number:</strong> {certificate.CertificateNumber}</p>
                        <p><strong>Course:</strong> {course.Title}</p>
                        <p><strong>Score:</strong> {certificate.Score}%</p>
                        <p><strong>Issue Date:</strong> {certificate.IssueDate:MMMM dd, yyyy}</p>
                    </div>

                    <p>You can download your certificate by clicking the button below:</p>
                    <a href='{certificate.DownloadUrl}' class='button'>Download Certificate</a>

                    <p>Keep up the great work! Your certificate is also available in your Pulse Connect dashboard.</p>

                    <p>Best regards,<br>The Pulse Connect Team</p>
                </body>
                </html>";

            await _emailSender
                .To(user.Email)
                .Subject($"🎓 Your Certificate for {course.Title} - Pulse Connect")
                .Body(emailBody, isHtml: true)
                .SendAsync();

            // Update certificate email status
            certificate.IsEmailed = true;
            certificate.EmailedDate = DateTime.UtcNow;
            _context.Certificates.Update(certificate);
            await _context.SaveChangesAsync();
        }



        // GET: api/certificates/verify/{certificateNumber}
        [HttpGet("verify/{certificateNumber}")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> VerifyCertificate(string certificateNumber)
        {
            var certificate = await _context.Certificates
                .Include(c => c.User)
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.CertificateNumber == certificateNumber);

            if (certificate == null)
            {
                return NotFound(new { valid = false, message = "Certificate not found" });
            }

            return Ok(new
            {
                valid = true,
                certificateNumber = certificate.CertificateNumber,
                userName = $"{certificate.User.FirstName} {certificate.User.LastName}",
                courseTitle = certificate.Course.Title,
                issueDate = certificate.IssueDate,
                score = certificate.Score
            });
        }

        [HttpGet("course/{courseId}")]
        public async Task<ActionResult<IEnumerable<CertificateDTO>>> GetCertificatesByCourse(string courseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid or missing user ID in token");
            }

            // Get only the most recent certificate for this specific course
            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.User)
                .Where(c => c.UserId == userId && c.CourseId == courseId)
                .OrderByDescending(c => c.IssueDate)
                .Select(c => new CertificateDTO
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    CourseId = c.CourseId,
                    CourseTitle = c.Course.Title,
                    UserName = $"{c.User.FirstName} {c.User.LastName}",
                    CertificateNumber = c.CertificateNumber,
                    IssueDate = c.IssueDate,
                    Score = c.Score,
                    DownloadUrl = c.DownloadUrl,
                    IsEmailed = c.IsEmailed
                })
                .FirstOrDefaultAsync(); // Only return one certificate

            if (certificate == null)
            {
                return NotFound("No certificate found for this course");
            }

            return Ok(certificate);
        }

        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetCertificateStats()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid or missing user ID in token");
            }

            var certificateCount = await _context.Certificates
                .Where(c => c.UserId == userId)
                .CountAsync();

            // Calculate XP and badges based on certificates
            var xpPoints = certificateCount * 250;
            var badgesEarned = certificateCount >= 3 ? 2 : certificateCount >= 1 ? 1 : 0;

            return Ok(new
            {
                TotalCertificates = certificateCount,
                XpPoints = xpPoints,
                BadgesEarned = badgesEarned
            });
        }
        [HttpPost("{certificateId}/share")]
        public async Task<IActionResult> ShareCertificate(string certificateId, [FromBody] ShareRequestDto shareRequest)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var certificate = await _context.Certificates.FindAsync(certificateId);
            if (certificate == null || certificate.UserId != userId)
            {
                return NotFound("Certificate not found");
            }

            var share = new CertificateShare
            {
                UserId = userId,
                CertificateId = certificateId,
                Platform = shareRequest.Platform,
                SharedDate = DateTime.UtcNow
            };

            _context.CertificateShares.Add(share);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Certificate share recorded" });
        }

        public class ShareRequestDto
        {
            public string Platform { get; set; }
        }

        [HttpGet("achievements")]
        public async Task<ActionResult<AchievementResponseDto>> GetAchievements()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Invalid or missing user ID in token");
                }

                // Get user certificates
                var certificates = await _context.Certificates
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

                // Get test attempts with enrollment data
                var testAttempts = await _context.TestAttempts
                    .Include(ta => ta.Enrollment)
                    .Where(ta => ta.Enrollment.UserId == userId)
                    .ToListAsync();

                // Get forum posts count
                var forumPosts = await _context.Posts
                    .Where(p => p.UserId == userId)
                    .CountAsync();

                // Get shared certificates count
                var sharedCertificates = await _context.CertificateShares
                    .Where(cs => cs.UserId == userId)
                    .CountAsync();

                // Get courses completed in one day (for Quick Learner badge)
                var quickLearnerCourses = await _context.Enrollments
                    .Include(e => e.Course)
                    .Where(e => e.UserId == userId && e.CompletionDate.HasValue)
                    .Select(e => new
                    {
                        e.CourseId,
                        e.EnrollmentDate,
                        e.CompletionDate,
                        Duration = EF.Functions.DateDiffDay(e.EnrollmentDate, e.CompletionDate.Value)
                    })
                    .Where(x => x.Duration <= 1)
                    .CountAsync();

                // Create badges list
                var badges = new List<BadgeDto>
        {
            new BadgeDto
            {
                Id = "1",
                Name = "Health Champion",
                Description = "Complete 3 courses",
                Icon = "🏆",
                Earned = certificates.Count >= 3,
                Progress = Math.Min(certificates.Count, 3),
                Target = 3,
                Category = "completion"
            },
            new BadgeDto
            {
                Id = "2",
                Name = "Quick Learner",
                Description = "Finish a course in 1 day",
                Icon = "⚡",
                Earned = quickLearnerCourses > 0,
                Progress = quickLearnerCourses,
                Target = 1,
                Category = "speed"
            },
            new BadgeDto
            {
                Id = "3",
                Name = "Community Helper",
                Description = "5 forum posts",
                Icon = "💬",
                Earned = forumPosts >= 5,
                Progress = Math.Min(forumPosts, 5),
                Target = 5,
                Category = "community"
            },
            new BadgeDto
            {
                Id = "4",
                Name = "Quiz Master",
                Description = "Score 100% on a quiz",
                Icon = "🎯",
                Earned = testAttempts.Any(ta => ta.Score == 100),
                Progress = testAttempts.Any(ta => ta.Score == 100) ? 1 : 0,
                Target = 1,
                Category = "performance"
            },
            new BadgeDto
            {
                Id = "5",
                Name = "Knowledge Seeker",
                Description = "Complete 5 courses",
                Icon = "📚",
                Earned = certificates.Count >= 5,
                Progress = Math.Min(certificates.Count, 5),
                Target = 5,
                Category = "completion"
            },
            new BadgeDto
            {
                Id = "6",
                Name = "Health Advocate",
                Description = "Share 3 certificates",
                Icon = "📤",
                Earned = sharedCertificates >= 3,
                Progress = Math.Min(sharedCertificates, 3),
                Target = 3,
                Category = "sharing"
            },
            new BadgeDto
            {
                Id = "7",
                Name = "Certified Pro",
                Description = "Earn 10 certificates",
                Icon = "⭐",
                Earned = certificates.Count >= 10,
                Progress = Math.Min(certificates.Count, 10),
                Target = 10,
                Category = "mastery"
            },
            new BadgeDto
            {
                Id = "8",
                Name = "Perfect Score",
                Description = "Get 100% on 3 different courses",
                Icon = "💯",
                Earned = certificates.Count(c => c.Score == 100) >= 3,
                Progress = Math.Min(certificates.Count(c => c.Score == 100), 3),
                Target = 3,
                Category = "excellence"
            }
        };

                // Create response
                var response = new AchievementResponseDto
                {
                    Badges = badges,
                    EarnedBadges = badges.Where(b => b.Earned).ToList(),
                    PendingBadges = badges.Where(b => !b.Earned).ToList(),
                    TotalBadges = badges.Count,
                    EarnedCount = badges.Count(b => b.Earned)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                // Log the error
                _logger.LogError(ex, "Error retrieving achievements for user");
                return StatusCode(500, "An error occurred while retrieving achievements");
            }
        }
    }
}