namespace Pulse_Connect_API.DTO
{
    public class CertificateDTO
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string CourseId { get; set; }
        public string CourseTitle { get; set; }
        public string UserName { get; set; }
        public string CertificateNumber { get; set; }
        public DateTime IssueDate { get; set; }
        public int Score { get; set; }
        public string DownloadUrl { get; set; }
        public bool IsEmailed { get; set; }
    }

    public class GenerateCertificateDTO
    {
        public string TestAttemptId { get; set; }
        public bool SendEmail { get; set; } = true;
    }

    public class CertificateEmailDTO
    {
        public string CertificateId { get; set; }
        public string RecipientEmail { get; set; }
    }


    public class BadgeDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public bool Earned { get; set; }
        public int Progress { get; set; }
        public int Target { get; set; }
        public string Category { get; set; }
    }

    public class AchievementResponseDto
    {
        public List<BadgeDto> Badges { get; set; }
        public List<BadgeDto> EarnedBadges { get; set; }
        public List<BadgeDto> PendingBadges { get; set; }
        public int TotalBadges { get; set; }
        public int EarnedCount { get; set; }
    }
}