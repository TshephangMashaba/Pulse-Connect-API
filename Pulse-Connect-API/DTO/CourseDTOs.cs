using Pulse_Connect_API.Migrations;

namespace Pulse_Connect_API.DTOs
{
    public class CreateCourseDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string ThumbnailUrl { get; set; }
        public int EstimatedDuration { get; set; }
    }

    public class CourseDTO
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string InstructorId { get; set; }
        public string InstructorName { get; set; }
        public string ThumbnailUrl { get; set; }
        public int EstimatedDuration { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public int ChapterCount { get; set; }
        public int EnrollmentCount { get; set; }
    }

    public class CreateChapterDTO
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string MediaUrl { get; set; }
        public string MediaType { get; set; }
    }

    public class ChapterDTO
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public int Order { get; set; }
        public string MediaUrl { get; set; }
        public string MediaType { get; set; }
        public string CourseId { get; set; }
    }

    public class CreateTestDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public int PassingScore { get; set; }
        public List<CreateQuestionDTO> Questions { get; set; }
    }

    public class CreateQuestionDTO
    {
        public string QuestionText { get; set; }
        public int Order { get; set; }
        public List<CreateOptionDTO> Options { get; set; }
    }

    public class CreateOptionDTO
    {
        public string OptionText { get; set; }
        public bool IsCorrect { get; set; }
        public int Order { get; set; }
    }

    public class EnrollmentDTO
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string CourseId { get; set; }
        public string CourseTitle { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public bool IsCompleted { get; set; }
        public int ProgressPercentage { get; set; }
        public int CompletedChapters { get; set; }
        public int TotalChapters { get; set; }
    }

    public class MarkChapterCompleteDTO
    {
        public string ChapterId { get; set; }
        public TimeSpan TimeSpent { get; set; }
    }

    public class SubmitTestDTO
    {
        public List<TestAnswerDTO> Answers { get; set; }
    }

    public class TestAnswerDTO
    {
        public string QuestionId { get; set; }
        public string SelectedOptionId { get; set; }
    }


    public class CreateTestBasicDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public int PassingScore { get; set; }
    }

    public class CourseTestDTO
    {
        public string Id { get; set; }
        public string CourseId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int PassingScore { get; set; }
    }

    // Add these to your DTOs section
    public class SubmitTestRequestDTO
    {
        public string TestId { get; set; }
        public List<TestAnswerDTO> Answers { get; set; }
    }

    public class TestResultDTO
    {
        public string AttemptId { get; set; }
        public int Score { get; set; }
        public bool IsPassed { get; set; }
        public int CorrectAnswers { get; set; }
        public int TotalQuestions { get; set; }
        public string Message { get; set; }
        public DateTime AttemptDate { get; set; }
    }

    public class TestAttemptDTO
    {
        public string Id { get; set; }
        public int Score { get; set; }
        public bool IsPassed { get; set; }
        public int CorrectAnswers { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime AttemptDate { get; set; }
    }


    
}
