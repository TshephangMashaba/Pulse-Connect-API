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
}