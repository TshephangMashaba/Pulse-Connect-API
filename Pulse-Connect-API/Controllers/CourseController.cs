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
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] // Protect all methods by default
    public class CourseController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;

        public CourseController(AppDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: api/course (Public)
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<CourseDTO>>> GetCourses()
        {
            var courses = await _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Chapters)
                .Include(c => c.Enrollments)
                .Select(c => new CourseDTO
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    InstructorId = c.InstructorId,
                    InstructorName = $"{c.Instructor.FirstName} {c.Instructor.LastName}",
                    ThumbnailUrl = c.ThumbnailUrl,
                    EstimatedDuration = c.EstimatedDuration,
                    CreatedDate = c.CreatedDate,
                    UpdatedDate = c.UpdatedDate,
                    ChapterCount = c.Chapters.Count,
                    EnrollmentCount = c.Enrollments.Count
                })
                .ToListAsync();

            return Ok(courses);
        }

        // GET: api/course/{id} (Public)
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<CourseDTO>> GetCourse(string id)
        {
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Chapters)
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                return NotFound();
            }

            var courseDto = new CourseDTO
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                InstructorId = course.InstructorId,
                InstructorName = $"{course.Instructor.FirstName} {course.Instructor.LastName}",
                ThumbnailUrl = course.ThumbnailUrl,
                EstimatedDuration = course.EstimatedDuration,
                CreatedDate = course.CreatedDate,
                UpdatedDate = course.UpdatedDate,
                ChapterCount = course.Chapters.Count,
                EnrollmentCount = course.Enrollments.Count // Fixed: Changed 'c' to 'course'
            };

            return Ok(courseDto);
        }

        // POST: api/course (Protected)
        [HttpPost]
        public async Task<ActionResult<Course>> CreateCourse([FromBody] CreateCourseDTO createCourseDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(createCourseDto.Title) || createCourseDto.Title == "string")
            {
                return BadRequest("Title must be a valid non-empty string");
            }

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

            var course = new Course
            {
                Id = Guid.NewGuid().ToString(),
                Title = createCourseDto.Title,
                Description = createCourseDto.Description,
                InstructorId = userId,
                ThumbnailUrl = createCourseDto.ThumbnailUrl,
                EstimatedDuration = createCourseDto.EstimatedDuration,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCourse), new { id = course.Id }, course);
        }

        // PUT: api/course/{id} (Protected)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCourse(string id, [FromBody] CreateCourseDTO updateCourseDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var course = await _context.Courses.FindAsync(id);
            if (course == null)
            {
                return NotFound();
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || course.InstructorId != userId)
            {
                return Forbid("You do not have permission to update this course");
            }

            course.Title = updateCourseDto.Title;
            course.Description = updateCourseDto.Description;
            course.ThumbnailUrl = updateCourseDto.ThumbnailUrl;
            course.EstimatedDuration = updateCourseDto.EstimatedDuration;
            course.UpdatedDate = DateTime.UtcNow;

            _context.Courses.Update(course);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/course/{id} (Protected)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourse(string id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null)
            {
                return NotFound();
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || course.InstructorId != userId)
            {
                return Forbid("You do not have permission to delete this course");
            }

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/course/{courseId}/chapter (Protected)
        [HttpPost("{courseId}/chapter")]
        public async Task<ActionResult<Chapter>> AddChapter(string courseId, [FromBody] CreateChapterDTO createChapterDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null)
            {
                return NotFound("Course not found");
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || course.InstructorId != userId)
            {
                return Forbid("You do not have permission to add chapters to this course");
            }

            var chapter = new Chapter
            {
                Id = Guid.NewGuid().ToString(),
                Title = createChapterDto.Title,
                Content = createChapterDto.Content,
                Order = createChapterDto.Order,
                MediaUrl = createChapterDto.MediaUrl,
                MediaType = createChapterDto.MediaType,
                CourseId = courseId
            };

            _context.Chapters.Add(chapter);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetChapter), new { courseId, chapterId = chapter.Id }, chapter);
        }

        // GET: api/course/{courseId}/chapter/{chapterId} (Public)
        [HttpGet("{courseId}/chapter/{chapterId}")]
        [AllowAnonymous]
        public async Task<ActionResult<ChapterDTO>> GetChapter(string courseId, string chapterId)
        {
            var chapter = await _context.Chapters
                .FirstOrDefaultAsync(c => c.Id == chapterId && c.CourseId == courseId);

            if (chapter == null)
            {
                return NotFound();
            }

            var chapterDto = new ChapterDTO
            {
                Id = chapter.Id,
                Title = chapter.Title,
                Content = chapter.Content,
                Order = chapter.Order,
                MediaUrl = chapter.MediaUrl,
                MediaType = chapter.MediaType,
                CourseId = chapter.CourseId
            };

            return Ok(chapterDto);
        }

        // GET: api/course/{courseId}/chapters (Public)
        [HttpGet("{courseId}/chapters")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ChapterDTO>>> GetChapters(string courseId)
        {
            var chapters = await _context.Chapters
                .Where(c => c.CourseId == courseId)
                .OrderBy(c => c.Order)
                .Select(c => new ChapterDTO
                {
                    Id = c.Id,
                    Title = c.Title,
                    Content = c.Content,
                    Order = c.Order,
                    MediaUrl = c.MediaUrl,
                    MediaType = c.MediaType,
                    CourseId = c.CourseId
                })
                .ToListAsync();

            return Ok(chapters);
        }

        // POST: api/course/{courseId}/test (Protected)
        [HttpPost("{courseId}/test")]
        public async Task<ActionResult<CourseTest>> CreateTest(string courseId, [FromBody] CreateTestDTO createTestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null)
            {
                return NotFound("Course not found");
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || course.InstructorId != userId)
            {
                return Forbid("You do not have permission to create tests for this course");
            }

            var test = new CourseTest
            {
                Id = Guid.NewGuid().ToString(),
                CourseId = courseId,
                Title = createTestDto.Title,
                Description = createTestDto.Description,
                PassingScore = createTestDto.PassingScore
            };

            foreach (var questionDto in createTestDto.Questions)
            {
                var question = new TestQuestion
                {
                    Id = Guid.NewGuid().ToString(),
                    QuestionText = questionDto.QuestionText,
                    Order = questionDto.Order
                };

                foreach (var optionDto in questionDto.Options)
                {
                    question.Options.Add(new QuestionOption
                    {
                        Id = Guid.NewGuid().ToString(),
                        OptionText = optionDto.OptionText,
                        IsCorrect = optionDto.IsCorrect,
                        Order = optionDto.Order
                    });
                }

                test.Questions.Add(question);
            }

            _context.CourseTests.Add(test);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTest), new { courseId, testId = test.Id }, test);
        }

        // GET: api/course/{courseId}/test (Public or restricted)
        [HttpGet("{courseId}/test")]
        [AllowAnonymous]
        public async Task<ActionResult<CourseTest>> GetTest(string courseId)
        {
            var test = await _context.CourseTests
                .Include(t => t.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(t => t.CourseId == courseId);

            if (test == null)
            {
                return NotFound();
            }

            return Ok(test);
        }

        // POST: api/course/enroll/{courseId} (Protected)
        [HttpPost("enroll/{courseId}")]
        public async Task<ActionResult<Enrollment>> EnrollInCourse(string courseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid or missing user ID in token");
            }

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null)
            {
                return NotFound("Course not found");
            }

            var existingEnrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId);

            if (existingEnrollment != null)
            {
                return Conflict("User is already enrolled in this course");
            }

            var enrollment = new Enrollment
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                CourseId = courseId,
                EnrollmentDate = DateTime.UtcNow,
                IsCompleted = false
            };

            _context.Enrollments.Add(enrollment);
            await _context.SaveChangesAsync();

            return Ok(enrollment);
        }

        // POST: api/course/unenroll/{courseId} (Protected)
        [HttpPost("unenroll/{courseId}")]
        public async Task<IActionResult> UnenrollFromCourse(string courseId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid or missing user ID in token");
            }

            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId);

            if (enrollment == null)
            {
                return NotFound("Enrollment not found");
            }

            _context.Enrollments.Remove(enrollment);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/course/complete-chapter (Protected)
        [HttpPost("complete-chapter")]
        public async Task<IActionResult> MarkChapterComplete([FromBody] MarkChapterCompleteDTO completeChapterDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid or missing user ID in token");
            }

            var chapter = await _context.Chapters
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.Id == completeChapterDto.ChapterId);

            if (chapter == null)
            {
                return NotFound("Chapter not found");
            }

            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == chapter.CourseId);

            if (enrollment == null)
            {
                return NotFound("Enrollment not found");
            }

            var progress = await _context.UserChapterProgresses
                .FirstOrDefaultAsync(p => p.EnrollmentId == enrollment.Id && p.ChapterId == completeChapterDto.ChapterId);

            if (progress == null)
            {
                progress = new UserChapterProgress
                {
                    Id = Guid.NewGuid().ToString(),
                    EnrollmentId = enrollment.Id,
                    ChapterId = completeChapterDto.ChapterId,
                    IsCompleted = true,
                    CompletedDate = DateTime.UtcNow,
                    TimeSpent = completeChapterDto.TimeSpent
                };
                _context.UserChapterProgresses.Add(progress);
            }
            else
            {
                progress.IsCompleted = true;
                progress.CompletedDate = DateTime.UtcNow;
                progress.TimeSpent = completeChapterDto.TimeSpent;
                _context.UserChapterProgresses.Update(progress);
            }

            await _context.SaveChangesAsync();

            return Ok();
        }

        // POST: api/course/{courseId}/submit-test (Protected)
        [HttpPost("{courseId}/submit-test")]
        public async Task<ActionResult<TestAttempt>> SubmitTest(string courseId, [FromBody] SubmitTestDTO submitTestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid or missing user ID in token");
            }

            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId);

            if (enrollment == null)
            {
                return NotFound("Enrollment not found");
            }

            var test = await _context.CourseTests
                .Include(t => t.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(t => t.CourseId == courseId);

            if (test == null)
            {
                return NotFound("Test not found");
            }

            int correctAnswers = 0;
            var userAnswers = new List<UserAnswer>();

            foreach (var answer in submitTestDto.Answers)
            {
                var question = test.Questions.FirstOrDefault(q => q.Id == answer.QuestionId);
                if (question == null) continue;

                var selectedOption = question.Options.FirstOrDefault(o => o.Id == answer.SelectedOptionId);
                if (selectedOption == null) continue;

                var userAnswer = new UserAnswer
                {
                    Id = Guid.NewGuid().ToString(),
                    QuestionId = answer.QuestionId,
                    SelectedOptionId = answer.SelectedOptionId,
                    IsCorrect = selectedOption.IsCorrect
                };

                if (selectedOption.IsCorrect)
                {
                    correctAnswers++;
                }

                userAnswers.Add(userAnswer);
            }

            int score = test.Questions.Count > 0 ? (int)Math.Round((double)correctAnswers / test.Questions.Count * 100) : 0;
            bool isPassed = score >= test.PassingScore;

            var attempt = new TestAttempt
            {
                Id = Guid.NewGuid().ToString(),
                EnrollmentId = enrollment.Id,
                TestId = test.Id,
                AttemptDate = DateTime.UtcNow,
                Score = score,
                IsPassed = isPassed,
                TotalQuestions = test.Questions.Count,
                CorrectAnswers = correctAnswers,
                UserAnswers = userAnswers
            };

            foreach (var userAnswer in userAnswers)
            {
                userAnswer.TestAttemptId = attempt.Id;
            }

            _context.TestAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            return Ok(attempt);
        }

        // GET: api/course/my-enrollments (Protected)
        [HttpGet("my-enrollments")]
        public async Task<ActionResult<IEnumerable<EnrollmentDTO>>> GetMyEnrollments()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid or missing user ID in token");
            }

            var enrollments = await _context.Enrollments
                .Include(e => e.Course)
                .ThenInclude(c => c.Chapters)
                .Include(e => e.ChapterProgress)
                .Where(e => e.UserId == userId)
                .Select(e => new EnrollmentDTO
                {
                    Id = e.Id,
                    UserId = e.UserId,
                    CourseId = e.CourseId,
                    CourseTitle = e.Course.Title,
                    EnrollmentDate = e.EnrollmentDate,
                    CompletionDate = e.CompletionDate,
                    IsCompleted = e.IsCompleted,
                    CompletedChapters = e.ChapterProgress.Count(p => p.IsCompleted),
                    TotalChapters = e.Course.Chapters.Count,
                    ProgressPercentage = e.Course.Chapters.Count > 0 ?
                        (int)Math.Round((double)e.ChapterProgress.Count(p => p.IsCompleted) / e.Course.Chapters.Count * 100) : 0
                })
                .ToListAsync();

            return Ok(enrollments);
        }
    }
}