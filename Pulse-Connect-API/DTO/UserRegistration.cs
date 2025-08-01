using System.ComponentModel.DataAnnotations;

namespace Pulse_Connect_API.DTO
{
    public class UserRegistrationDTO
    {
        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; }

        [Compare("Password")]
        public string ConfirmPassword { get; set; }

        public string PhoneNumber { get; set; }

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        public string Race { get; set; }

        [Required]
        public string Gender { get; set; }
    }

    public class UserLoginDTO
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }

    public class VerifyOtpDTO
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, StringLength(6, MinimumLength = 6)]
        public string Otp { get; set; }
    }

    public class ForgotPasswordDTO
    {
        [Required, EmailAddress]
        public string Email { get; set; }
    }

    public class ResetPasswordDTO
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Token { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; }

        [Compare("NewPassword")]
        public string ConfirmPassword { get; set; }
    }

    public class UpdateProfileDTO
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Address { get; set; }
        public string Race { get; set; }
        public string Gender { get; set; }
        public IFormFile ProfilePicture { get; set; } // Added for profile picture
    }

    public class RegistrationResponseDTO
    {
        public bool IsSuccessful { get; set; }
        public IEnumerable<string> Errors { get; set; }
    }

    public class AuthResponseDTO
    {
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; }
        public string Token { get; set; }
        public object User { get; set; }
    }
}