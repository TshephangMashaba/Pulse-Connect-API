using FluentEmail.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pulse_Connect_API.DTOs;
using Pulse_Connect_API.Models;
using System.Security.Claims;
using static System.Net.Mime.MediaTypeNames;

namespace Pulse_Connect_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CourseController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IFluentEmail _emailSender;

        public CourseController(AppDbContext context, UserManager<User> userManager, IFluentEmail emailSender)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        // GET: api/course (Public) - No email needed for GET operations
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

        // GET: api/course/{id} (Public) - No email needed for GET operations
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
                EnrollmentCount = course.Enrollments.Count
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

            // Send course creation confirmation email
            var emailBody = $@"
                <h2>Course Created Successfully</h2>
                <p>Hello {user.FirstName},</p>
                <p>Your course <strong>{course.Title}</strong> has been successfully created on Pulse Connect.</p>
                <p><strong>Course Details:</strong></p>
                <ul>
                    <li><strong>Title:</strong> {course.Title}</li>
                    <li><strong>Description:</strong> {course.Description}</li>
                    <li><strong>Estimated Duration:</strong> {course.EstimatedDuration} hours</li>
                    <li><strong>Created Date:</strong> {course.CreatedDate.ToString("f")}</li>
                </ul>
                <p>You can now start adding chapters and content to your course.</p>
                <p>Thank you for contributing to Pulse Connect!</p>";

            await _emailSender
                .To(user.Email)
                .Subject($"Course Created: {course.Title}")
                .Body(emailBody, isHtml: true)
                .SendAsync();

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

            var course = await _context.Courses
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                return NotFound();
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || course.InstructorId != userId)
            {
                return Forbid("You do not have permission to update this course");
            }

            var oldTitle = course.Title;

            course.Title = updateCourseDto.Title;
            course.Description = updateCourseDto.Description;
            course.ThumbnailUrl = updateCourseDto.ThumbnailUrl;
            course.EstimatedDuration = updateCourseDto.EstimatedDuration;
            course.UpdatedDate = DateTime.UtcNow;

            _context.Courses.Update(course);
            await _context.SaveChangesAsync();

            // Send course update notification email
            var instructor = await _userManager.FindByIdAsync(course.InstructorId);
            if (instructor != null)
            {
                var emailBody = $@"
                    <h2>Course Updated Successfully</h2>
                    <p>Hello {instructor.FirstName},</p>
                    <p>Your course <strong>{oldTitle}</strong> has been successfully updated.</p>
                    <p><strong>Updated Course Details:</strong></p>
                    <ul>
                        <li><strong>Title:</strong> {course.Title}</li>
                        <li><strong>Description:</strong> {course.Description}</li>
                        <li><strong>Estimated Duration:</strong> {course.EstimatedDuration} hours</li>
                        <li><strong>Last Updated:</strong> {course.UpdatedDate.ToString("f")}</li>
                    </ul>
                    <p>The changes are now live on Pulse Connect.</p>";

                await _emailSender
                    .To(instructor.Email)
                    .Subject($"Course Updated: {course.Title}")
                    .Body(emailBody, isHtml: true)
                    .SendAsync();
            }

            return NoContent();
        }

        // DELETE: api/course/{id} (Protected)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourse(string id)
        {
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Enrollments)
                .ThenInclude(e => e.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                return NotFound();
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || course.InstructorId != userId)
            {
                return Forbid("You do not have permission to delete this course");
            }

            // Get enrolled users for notification
            var enrolledUsers = course.Enrollments.Select(e => e.User).ToList();

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            // Send course deletion notification to instructor
            var instructor = await _userManager.FindByIdAsync(course.InstructorId);
            if (instructor != null)
            {
                var instructorEmailBody = $@"
                    <h2>Course Deleted Successfully</h2>
                    <p>Hello {instructor.FirstName},</p>
                    <p>Your course <strong>{course.Title}</strong> has been successfully deleted from Pulse Connect.</p>
                    <p><strong>Deleted Course Details:</strong></p>
                    <ul>
                        <li><strong>Title:</strong> {course.Title}</li>
                        <li><strong>Description:</strong> {course.Description}</li>
                        <li><strong>Enrollments:</strong> {course.Enrollments.Count} students</li>
                    </ul>
                    <p>All associated content, chapters, and enrollments have been removed.</p>";

                await _emailSender
                    .To(instructor.Email)
                    .Subject($"Course Deleted: {course.Title}")
                    .Body(instructorEmailBody, isHtml: true)
                    .SendAsync();
            }

            // Send notification to enrolled students
            foreach (var student in enrolledUsers)
            {
                var studentEmailBody = $@"
                    <h2>Course No Longer Available</h2>
                    <p>Hello {student.FirstName},</p>
                    <p>The course <strong>{course.Title}</strong> that you were enrolled in has been removed from Pulse Connect by the instructor.</p>
                    <p>Your enrollment and progress in this course have been removed from your account.</p>
                    <p>We apologize for any inconvenience this may cause.</p>
                    <p>If you have any questions, please contact our support team.</p>";

                await _emailSender
                    .To(student.Email)
                    .Subject($"Course Removed: {course.Title}")
                    .Body(studentEmailBody, isHtml: true)
                    .SendAsync();
            }

            return NoContent();
        }


        [HttpPost("{courseId}/chapter")]
        public async Task<ActionResult<Chapter>> AddChapter(
        string courseId,
        [FromForm] string Title,
        [FromForm] string Content,
        [FromForm] string MediaUrl = null,
        [FromForm] string MediaType = null,
        IFormFile MediaFile = null)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    // Return validation errors as JSON, not HTML
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(new
                    {
                        message = "Validation failed",
                        errors = errors
                    });
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(Title))
                {
                    return BadRequest(new { message = "Title is required" });
                }

                if (string.IsNullOrWhiteSpace(Content))
                {
                    return BadRequest(new { message = "Content is required" });
                }

                var course = await _context.Courses
                    .Include(c => c.Instructor)
                    .Include(c => c.Enrollments)
                    .ThenInclude(e => e.User)
                    .FirstOrDefaultAsync(c => c.Id == courseId);

                if (course == null)
                {
                    return NotFound(new { message = "Course not found" });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || course.InstructorId != userId)
                {
                    return Forbid("You do not have permission to add chapters to this course");
                }

                // Calculate next order number
                int nextOrder = await _context.Chapters
                    .Where(c => c.CourseId == courseId)
                    .OrderByDescending(c => c.Order)
                    .Select(c => c.Order)
                    .FirstOrDefaultAsync() + 1;

                string finalMediaUrl = MediaUrl;
                string finalMediaType = MediaType;

                // Handle file upload if provided
                if (MediaFile != null && MediaFile.Length > 0)
                {
                    try
                    {
                        // Create uploads directory if it doesn't exist
                        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                        if (!Directory.Exists(uploadsDir))
                            Directory.CreateDirectory(uploadsDir);

                        // Generate unique filename
                        var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(MediaFile.FileName)}";
                        var filePath = Path.Combine(uploadsDir, fileName);

                        // Save the file
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await MediaFile.CopyToAsync(stream);
                        }

                        finalMediaUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";
                        finalMediaType = GetMediaTypeFromFileName(MediaFile.FileName);
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, new { message = $"Error uploading file: {ex.Message}" });
                    }
                }

                var chapter = new Chapter
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = Title,
                    Content = Content, // This preserves the HTML content from TinyMCE
                    Order = nextOrder,
                    MediaUrl = finalMediaUrl,
                    MediaType = finalMediaType,
                    CourseId = courseId
                };

                _context.Chapters.Add(chapter);
                await _context.SaveChangesAsync();

                // Send new chapter notification to enrolled students
                foreach (var enrollment in course.Enrollments)
                {
                    if (enrollment.User != null)
                    {
                        var studentEmailBody = $@"
                <h2>New Chapter Available</h2>
                <p>Hello {enrollment.User.FirstName},</p>
                <p>A new chapter <strong>{chapter.Title}</strong> has been added to the course <strong>{course.Title}</strong>.</p>
                <p>You can now access this new content and continue your learning journey.</p>
                <p>Happy learning!</p>";

                        await _emailSender
                            .To(enrollment.User.Email)
                            .Subject($"New Chapter: {chapter.Title} - {course.Title}")
                            .Body(studentEmailBody, isHtml: true)
                            .SendAsync();
                    }
                }

                // Return the created chapter with proper JSON formatting
                return CreatedAtAction(nameof(GetChapter), new { courseId, chapterId = chapter.Id }, new
                {
                    Id = chapter.Id,
                    Title = chapter.Title,
                    Content = chapter.Content, // HTML content is preserved here
                    Order = chapter.Order,
                    MediaUrl = chapter.MediaUrl,
                    MediaType = chapter.MediaType,
                    CourseId = chapter.CourseId
                });
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error adding chapter: {ex.Message}");

                // Return a proper JSON error response
                return StatusCode(500, new
                {
                    message = "An error occurred while adding the chapter",
                    error = ex.Message
                });
            }
        }
        private string GetMediaTypeFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();

            return extension switch
            {
                ".mp4" or ".mov" or ".avi" or ".wmv" or ".webm" => "video",
                ".mp3" or ".wav" or ".ogg" or ".m4a" => "audio",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "image",
                ".pdf" or ".doc" or ".docx" or ".txt" or ".ppt" or ".pptx" => "document",
                _ => "file"
            };
        }


        // GET: api/course/{courseId}/test
        [HttpGet("{courseId}/test")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetTest(string courseId)
        {
            try
            {
                var test = await _context.CourseTests
                    .Include(t => t.Questions)
                    .ThenInclude(q => q.Options)
                    .FirstOrDefaultAsync(t => t.CourseId == courseId);

                if (test == null)
                {
                    // Return a proper empty test structure
                    return Ok(new
                    {
                        Id = (string)null,
                        CourseId = courseId,
                        Title = "",
                        Description = "",
                        PassingScore = 70,
                        Questions = new List<object>()
                    });
                }

                // Return simplified response to avoid circular references
                var response = new
                {
                    Id = test.Id,
                    CourseId = test.CourseId,
                    Title = test.Title,
                    Description = test.Description,
                    PassingScore = test.PassingScore,
                    Questions = test.Questions.Select(q => new
                    {
                        Id = q.Id,
                        QuestionText = q.QuestionText,
                        Order = q.Order,
                        Options = q.Options.Select(o => new
                        {
                            Id = o.Id,
                            OptionText = o.OptionText,
                            IsCorrect = o.IsCorrect,
                            Order = o.Order
                        }).ToList()
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting test for course {courseId}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return StatusCode(500, "An error occurred while retrieving the test");
            }
        }
        // GET: api/course/{courseId}/chapter/{chapterId} (Public) - No email needed
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

        // GET: api/course/{courseId}/chapters (Public) - No email needed
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

            var course = await _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Enrollments)
                .ThenInclude(e => e.User)
                .FirstOrDefaultAsync(c => c.Id == courseId);

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

            // Send new test notification to enrolled students
            foreach (var enrollment in course.Enrollments)
            {
                var studentEmailBody = $@"
                    <h2>New Test Available</h2>
                    <p>Hello {enrollment.User.FirstName},</p>
                    <p>A new test <strong>{test.Title}</strong> has been added to the course <strong>{course.Title}</strong>.</p>
                    <p><strong>Test Details:</strong></p>
                    <ul>
                        <li><strong>Title:</strong> {test.Title}</li>
                        <li><strong>Description:</strong> {test.Description}</li>
                        <li><strong>Passing Score:</strong> {test.PassingScore}%</li>
                        <li><strong>Questions:</strong> {test.Questions.Count}</li>
                    </ul>
                    <p>You can take this test to assess your understanding of the course material.</p>
                    <p>Good luck!</p>";

                await _emailSender
                    .To(enrollment.User.Email)
                    .Subject($"New Test: {test.Title} - {course.Title}")
                    .Body(studentEmailBody, isHtml: true)
                    .SendAsync();
            }

            return CreatedAtAction(nameof(GetTest), new { courseId, testId = test.Id }, test);
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

            var user = await _userManager.FindByIdAsync(userId);
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(c => c.Id == courseId);

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

            // Send enrollment confirmation to student
            var studentEmailBody = $@"
                <h2>Course Enrollment Confirmation</h2>
                <p>Hello {user.FirstName},</p>
                <p>You have successfully enrolled in the course <strong>{course.Title}</strong>.</p>
                <p><strong>Course Details:</strong></p>
                <ul>
                    <li><strong>Title:</strong> {course.Title}</li>
                    <li><strong>Instructor:</strong> {course.Instructor.FirstName} {course.Instructor.LastName}</li>
                    <li><strong>Estimated Duration:</strong> {course.EstimatedDuration} hours</li>
                    <li><strong>Enrollment Date:</strong> {enrollment.EnrollmentDate.ToString("f")}</li>
                </ul>
                <p>You can now start learning! Access your course from your dashboard.</p>
                <p>Happy learning!</p>";

            await _emailSender
                .To(user.Email)
                .Subject($"Enrollment Confirmation: {course.Title}")
                .Body(studentEmailBody, isHtml: true)
                .SendAsync();

            // Send enrollment notification to instructor
            var instructorEmailBody = $@"
                <h2>New Student Enrollment</h2>
                <p>Hello {course.Instructor.FirstName},</p>
                <p>A new student has enrolled in your course <strong>{course.Title}</strong>.</p>
                <p><strong>Student Details:</strong></p>
                <ul>
                    <li><strong>Name:</strong> {user.FirstName} {user.LastName}</li>
                    <li><strong>Email:</strong> {user.Email}</li>
                    <li><strong>Enrollment Date:</strong> {enrollment.EnrollmentDate.ToString("f")}</li>
                </ul>
                <p>Total enrollments for this course: {course.Enrollments.Count + 1}</p>";

            await _emailSender
                .To(course.Instructor.Email)
                .Subject($"New Enrollment: {user.FirstName} {user.LastName} - {course.Title}")
                .Body(instructorEmailBody, isHtml: true)
                .SendAsync();

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

            var user = await _userManager.FindByIdAsync(userId);
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                .ThenInclude(c => c.Instructor)
                .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId);

            if (enrollment == null)
            {
                return NotFound("Enrollment not found");
            }

            _context.Enrollments.Remove(enrollment);
            await _context.SaveChangesAsync();

            // Send unenrollment confirmation to student
            var studentEmailBody = $@"
                <h2>Course Unenrollment Confirmation</h2>
                <p>Hello {user.FirstName},</p>
                <p>You have been successfully unenrolled from the course <strong>{enrollment.Course.Title}</strong>.</p>
                <p><strong>Unenrollment Details:</strong></p>
                <ul>
                    <li><strong>Course:</strong> {enrollment.Course.Title}</li>
                    <li><strong>Instructor:</strong> {enrollment.Course.Instructor.FirstName} {enrollment.Course.Instructor.LastName}</li>
                    <li><strong>Original Enrollment Date:</strong> {enrollment.EnrollmentDate.ToString("f")}</li>
                    <li><strong>Unenrollment Date:</strong> {DateTime.UtcNow.ToString("f")}</li>
                </ul>
                <p>Your progress and data for this course have been removed.</p>
                <p>If this was a mistake or you'd like to re-enroll, you can do so from the course page.</p>";

            await _emailSender
                .To(user.Email)
                .Subject($"Unenrollment Confirmation: {enrollment.Course.Title}")
                .Body(studentEmailBody, isHtml: true)
                .SendAsync();

            // Send unenrollment notification to instructor
            var instructorEmailBody = $@"
                <h2>Student Unenrollment Notification</h2>
                <p>Hello {enrollment.Course.Instructor.FirstName},</p>
                <p>A student has unenrolled from your course <strong>{enrollment.Course.Title}</strong>.</p>
                <p><strong>Student Details:</strong></p>
                <ul>
                    <li><strong>Name:</strong> {user.FirstName} {user.LastName}</li>
                    <li><strong>Email:</strong> {user.Email}</li>
                    <li><strong>Enrollment Duration:</strong> {(DateTime.UtcNow - enrollment.EnrollmentDate).Days} days</li>
                </ul>
                <p>Current enrollments for this course: {enrollment.Course.Enrollments.Count - 1}</p>";

            await _emailSender
                .To(enrollment.Course.Instructor.Email)
                .Subject($"Student Unenrollment: {user.FirstName} {user.LastName} - {enrollment.Course.Title}")
                .Body(instructorEmailBody, isHtml: true)
                .SendAsync();

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

            var user = await _userManager.FindByIdAsync(userId);
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

            // Send chapter completion notification (optional - could be too frequent)
            // Uncomment if you want to send emails for every chapter completion
            /*
            var emailBody = $@"
                <h2>Chapter Completed!</h2>
                <p>Hello {user.FirstName},</p>
                <p>Congratulations! You have successfully completed the chapter <strong>{chapter.Title}</strong> in the course <strong>{chapter.Course.Title}</strong>.</p>
                <p><strong>Completion Details:</strong></p>
                <ul>
                    <li><strong>Chapter:</strong> {chapter.Title}</li>
                    <li><strong>Course:</strong> {chapter.Course.Title}</li>
                    <li><strong>Completion Date:</strong> {DateTime.UtcNow.ToString("f")}</li>
                    <li><strong>Time Spent:</strong> {completeChapterDto.TimeSpent} minutes</li>
                </ul>
                <p>Keep up the great work! Continue to the next chapter to further your learning.</p>";

            await _emailSender
                .To(user.Email)
                .Subject($"Chapter Completed: {chapter.Title}")
                .Body(emailBody, isHtml: true)
                .SendAsync();
            */

            return Ok();
        }

        private async Task GenerateCertificateAutomatically(TestAttempt attempt, User user, Course course)
        {
            try
            {
                // Check if certificate already exists
                var existingCertificate = await _context.Certificates
                    .FirstOrDefaultAsync(c => c.TestAttemptId == attempt.Id);

                if (existingCertificate != null)
                    return;

                var certificateNumber = $"PC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";

                var certificate = new Certificate
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = user.Id,
                    CourseId = course.Id,
                    TestAttemptId = attempt.Id,
                    CertificateNumber = certificateNumber,
                    Score = attempt.Score,
                    IssueDate = DateTime.UtcNow,
                    DownloadUrl = $"{Request.Scheme}://{Request.Host}/api/certificates/{certificateNumber}/download",
                    IsEmailed = true // Mark as emailed since we'll send it below
                };

                _context.Certificates.Add(certificate);
                await _context.SaveChangesAsync();

                // Send certificate email
                await SendCertificateEmail(certificate, user, course);
            }
            catch (Exception ex)
            {
                // Log the error but don't break the test submission
                Console.WriteLine($"Error generating certificate: {ex.Message}");
            }
        }

        private async Task SendCertificateEmail(Certificate certificate, User user, Course course)
        {
            try
            {
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending certificate email: {ex.Message}");
            }
        }

        // Add this to your CourseController
        [HttpPost("upload")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
                // Create uploads directory if it doesn't exist
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
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

                // Return the URL to access the file
                var fileUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";
                return Ok(new { url = fileUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // GET: api/course/my-enrollments (Protected) - No email needed for GET operations
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



        // PUT: api/course/{courseId}/chapter/{chapterId} (Protected)
        [HttpPut("{courseId}/chapter/{chapterId}")]
        public async Task<IActionResult> UpdateChapter(string courseId, string chapterId, [FromBody] CreateChapterDTO updateChapterDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var chapter = await _context.Chapters
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.Id == chapterId && c.CourseId == courseId);

            if (chapter == null)
            {
                return NotFound("Chapter not found");
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || chapter.Course.InstructorId != userId)
            {
                return Forbid("You do not have permission to update this chapter");
            }

            chapter.Title = updateChapterDto.Title;
            chapter.Content = updateChapterDto.Content;
            chapter.MediaUrl = updateChapterDto.MediaUrl;
            chapter.MediaType = updateChapterDto.MediaType;

            _context.Chapters.Update(chapter);
            await _context.SaveChangesAsync();

            return Ok(chapter);
        }

        // GET: api/course/{courseId}/test (Public or restricted) - No email needed



        // DELETE: api/course/{courseId}/chapter/{chapterId} (Protected)
        [HttpDelete("{courseId}/chapter/{chapterId}")]
        public async Task<IActionResult> DeleteChapter(string courseId, string chapterId)
        {
            var chapter = await _context.Chapters
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.Id == chapterId && c.CourseId == courseId);

            if (chapter == null)
            {
                return NotFound("Chapter not found");
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || chapter.Course.InstructorId != userId)
            {
                return Forbid("You do not have permission to delete this chapter");
            }

            _context.Chapters.Remove(chapter);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        // POST: api/course/{courseId}/test/basic
        [HttpPost("{courseId}/test/basic")]
        public async Task<ActionResult<object>> CreateTestBasic(string courseId, [FromBody] CreateTestBasicDTO createTestBasicDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(createTestBasicDto.Title))
                {
                    return BadRequest("Test title is required");
                }

                var course = await _context.Courses
                    .Include(c => c.Instructor)
                    .FirstOrDefaultAsync(c => c.Id == courseId);

                if (course == null)
                {
                    return NotFound($"Course with ID {courseId} not found");
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Invalid or missing user ID in token");
                }

                if (course.InstructorId != userId)
                {
                    return Forbid("You do not have permission to create tests for this course");
                }

                // Check if test already exists
                var existingTest = await _context.CourseTests
                    .FirstOrDefaultAsync(t => t.CourseId == courseId);

                if (existingTest != null)
                {
                    // Update existing test basic info
                    existingTest.Title = createTestBasicDto.Title;
                    existingTest.Description = createTestBasicDto.Description;
                    existingTest.PassingScore = createTestBasicDto.PassingScore;

                    _context.CourseTests.Update(existingTest);
                    await _context.SaveChangesAsync();

                    // Return simplified response without navigation properties
                    return Ok(new
                    {
                        Id = existingTest.Id,
                        CourseId = existingTest.CourseId,
                        Title = existingTest.Title,
                        Description = existingTest.Description,
                        PassingScore = existingTest.PassingScore
                    });
                }

                // Create new test
                var test = new CourseTest
                {
                    Id = Guid.NewGuid().ToString(),
                    CourseId = courseId,
                    Title = createTestBasicDto.Title,
                    Description = createTestBasicDto.Description,
                    PassingScore = createTestBasicDto.PassingScore,
                    Questions = new List<TestQuestion>()
                };

                _context.CourseTests.Add(test);
                await _context.SaveChangesAsync();

                // Return simplified response without navigation properties to avoid circular reference
                var response = new
                {
                    Id = test.Id,
                    CourseId = test.CourseId,
                    Title = test.Title,
                    Description = test.Description,
                    PassingScore = test.PassingScore
                };

                return Ok(response);
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"Database error creating test: {dbEx.Message}");
                if (dbEx.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {dbEx.InnerException.Message}");
                }
                return StatusCode(500, "A database error occurred while creating the test");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating test basic for course {courseId}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return StatusCode(500, "An unexpected error occurred while creating the test");
            }
        }


        // POST: api/course/{courseId}/test/questions (Add questions to existing test)
        // POST: api/course/{courseId}/test/questions (Add questions to existing test)
        [HttpPost("{courseId}/test/questions")]
        public async Task<ActionResult<object>> AddQuestionsToTest(string courseId, [FromBody] List<CreateQuestionDTO> questionsDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var test = await _context.CourseTests
                .Include(t => t.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(t => t.CourseId == courseId);

            if (test == null)
            {
                return NotFound("Test not found. Please create the test first.");
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var course = await _context.Courses.FindAsync(courseId);

            if (string.IsNullOrEmpty(userId) || course.InstructorId != userId)
            {
                return Forbid("You do not have permission to modify this test");
            }

            // Clear existing questions
            test.Questions.Clear();

            // Add new questions
            foreach (var questionDto in questionsDto)
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

            _context.CourseTests.Update(test);
            await _context.SaveChangesAsync();

            // Return the updated test
            var response = new
            {
                Id = test.Id,
                CourseId = test.CourseId,
                Title = test.Title,
                Description = test.Description,
                PassingScore = test.PassingScore,
                Questions = test.Questions.Select(q => new
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    Order = q.Order,
                    Options = q.Options.Select(o => new
                    {
                        Id = o.Id,
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect,
                        Order = o.Order
                    }).ToList()
                }).ToList()
            };

            return Ok(response);
        }
  


    // GET: api/course/my-courses (Get courses for the current user)
[HttpGet("my-courses")]
        public async Task<ActionResult<IEnumerable<EnrollmentDTO>>> GetMyCourses()
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

        // POST: api/course/submit-test
        // POST: api/course/submit-test
        [HttpPost("submit-test")]
        public async Task<ActionResult<TestResultDTO>> SubmitTest([FromBody] SubmitTestRequestDTO submitRequest)
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

            try
            {
                // Get the test with questions and correct answers
                var test = await _context.CourseTests
                    .Include(t => t.Questions)
                    .ThenInclude(q => q.Options)
                    .Include(t => t.Course)
                    .ThenInclude(c => c.Instructor)
                    .FirstOrDefaultAsync(t => t.Id == submitRequest.TestId);

                if (test == null)
                {
                    return NotFound("Test not found");
                }

                // Verify user is enrolled in the course
                var enrollment = await _context.Enrollments
                    .Include(e => e.User)
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == test.CourseId);

                if (enrollment == null)
                {
                    return Forbid("You must be enrolled in the course to take the test");
                }

                var user = enrollment.User;
                var course = test.Course;

                // Calculate score
                int correctAnswers = 0;
                var userAnswers = new List<UserAnswer>();

                foreach (var userAnswer in submitRequest.Answers)
                {
                    var question = test.Questions.FirstOrDefault(q => q.Id == userAnswer.QuestionId);
                    if (question == null) continue;

                    var selectedOption = question.Options.FirstOrDefault(o => o.Id == userAnswer.SelectedOptionId);
                    if (selectedOption == null) continue;

                    bool isCorrect = selectedOption.IsCorrect;

                    if (isCorrect)
                    {
                        correctAnswers++;
                    }

                    userAnswers.Add(new UserAnswer
                    {
                        Id = Guid.NewGuid().ToString(),
                        QuestionId = userAnswer.QuestionId,
                        SelectedOptionId = userAnswer.SelectedOptionId,
                        IsCorrect = isCorrect
                    });
                }

                // Calculate percentage score
                int score = test.Questions.Count > 0
                    ? (int)Math.Round((double)correctAnswers / test.Questions.Count * 100)
                    : 0;

                bool isPassed = score >= test.PassingScore;

                // Save test attempt
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

                // Set foreign key for user answers
                foreach (var answer in userAnswers)
                {
                    answer.TestAttemptId = attempt.Id;
                }

                _context.TestAttempts.Add(attempt);
                await _context.SaveChangesAsync();

                // AUTO-GENERATE CERTIFICATE IF PASSED
                if (isPassed)
                {
                    await GenerateCertificateAutomatically(attempt, user, course);
                }

                // Send test results email to student
                var studentEmailBody = $@"
            <h2>Test Results: {test.Title}</h2>
            <p>Hello {user.FirstName},</p>
            <p>You have completed the test <strong>{test.Title}</strong> for the course <strong>{course.Title}</strong>.</p>
            <p><strong>Test Results:</strong></p>
            <ul>
                <li><strong>Score:</strong> {score}%</li>
                <li><strong>Status:</strong> {(isPassed ? "PASSED" : "FAILED")}</li>
                <li><strong>Correct Answers:</strong> {correctAnswers}/{test.Questions.Count}</li>
                <li><strong>Passing Score:</strong> {test.PassingScore}%</li>
                <li><strong>Completion Date:</strong> {attempt.AttemptDate.ToString("f")}</li>
            </ul>
            <p>{(isPassed ? "Congratulations on passing the test! You're making great progress." : "Don't worry! You can review the material and try again.")}</p>
            <p>Keep up the good work!</p>";

                await _emailSender
                    .To(user.Email)
                    .Subject($"Test Results: {test.Title} - {(isPassed ? "PASSED" : "FAILED")}")
                    .Body(studentEmailBody, isHtml: true)
                    .SendAsync();

                // Send test results notification to instructor (only if failed or for monitoring)
                if (!isPassed || score < 70) // Only notify instructor for failures or low scores
                {
                    var instructorEmailBody = $@"
                <h2>Student Test Results: {test.Title}</h2>
                <p>Hello {course.Instructor.FirstName},</p>
                <p>A student has completed the test <strong>{test.Title}</strong> in your course <strong>{course.Title}</strong>.</p>
                <p><strong>Student Details:</strong></p>
                <ul>
                    <li><strong>Name:</strong> {user.FirstName} {user.LastName}</li>
                    <li><strong>Email:</strong> {user.Email}</li>
                </ul>
                <p><strong>Test Results:</strong></p>
                <ul>
                    <li><strong>Score:</strong> {score}%</li>
                    <li><strong>Status:</strong> {(isPassed ? "PASSED" : "FAILED")}</li>
                    <li><strong>Correct Answers:</strong> {correctAnswers}/{test.Questions.Count}</li>
                    <li><strong>Completion Date:</strong> {attempt.AttemptDate.ToString("f")}</li>
                </ul>
                <p>This student may need additional support or review of the material.</p>";

                    await _emailSender
                        .To(course.Instructor.Email)
                        .Subject($"Student Test Results: {user.FirstName} {user.LastName} - {test.Title}")
                        .Body(instructorEmailBody, isHtml: true)
                        .SendAsync();
                }

                // Generate success message
                string message = isPassed
                    ? $"Congratulations! You passed the test with a score of {score}%."
                    : $"You scored {score}% but didn't pass. The passing score is {test.PassingScore}%.";

                // Return result
                var result = new TestResultDTO
                {
                    AttemptId = attempt.Id,
                    Score = score,
                    IsPassed = isPassed,
                    CorrectAnswers = correctAnswers,
                    TotalQuestions = test.Questions.Count,
                    Message = message,
                    AttemptDate = attempt.AttemptDate
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error submitting test: {ex.Message}");
                return StatusCode(500, "An error occurred while processing your test submission");
            }
        }

        // GET: api/course/{courseId}/test-attempts
        [HttpGet("{courseId}/test-attempts")]
        public async Task<ActionResult<IEnumerable<TestAttemptDTO>>> GetTestAttempts(string courseId)
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

            var attempts = await _context.TestAttempts
                .Where(ta => ta.EnrollmentId == enrollment.Id)
                .OrderByDescending(ta => ta.AttemptDate)
                .Select(ta => new TestAttemptDTO
                {
                    Id = ta.Id,
                    Score = ta.Score,
                    IsPassed = ta.IsPassed,
                    CorrectAnswers = ta.CorrectAnswers,
                    TotalQuestions = ta.TotalQuestions,
                    AttemptDate = ta.AttemptDate
                })
                .ToListAsync();

            return Ok(attempts);
        }


    }

}