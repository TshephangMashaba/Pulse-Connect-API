using AutoMapper;
using FluentEmail.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using Pulse_Connect_API.DTO;
using Pulse_Connect_API.Models;
using Pulse_Connect_API.Services;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Pulse_Connect_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly IMapper _mapper;
        private readonly JwtHandler _jwtHandler;
        private readonly IMemoryCache _cache;
        private readonly IFluentEmail _emailSender;

        public AccountController(
            UserManager<User> userManager,
            IMapper mapper,
            JwtHandler jwtHandler,
            IMemoryCache cache,
            IFluentEmail emailSender)
        {
            _userManager = userManager;
            _mapper = mapper;
            _jwtHandler = jwtHandler;
            _cache = cache;
            _emailSender = emailSender;
        }

        /// <summary>
        /// Registers a new user for Pulse Connect
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegistrationDTO request)
        {
            try
            {
                if (request == null || !ModelState.IsValid)
                    return BadRequest("Invalid registration request.");

                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                    return BadRequest("Email is already registered.");

                // Map DTO to User entity
                var user = new User
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    UserName = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    DateOfBirth = request.DateOfBirth,
                    Address = request.Address,
                    Race = request.Race,
                    Gender = request.Gender
                };

                // Create user
                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    return BadRequest(new RegistrationResponseDTO
                    {
                        IsSuccessful = false,
                        Errors = result.Errors.Select(e => e.Description)
                    });
                }

                // Assign default "User" role
                await _userManager.AddToRoleAsync(user, "USER");

                // Send welcome email
                var emailBody = new StringBuilder();
                emailBody.AppendLine($"<h2>Welcome to Pulse Connect, {user.FirstName}!</h2>");
                emailBody.AppendLine("<p>Your account has been successfully created.</p>");
                emailBody.AppendLine("<h3>Account Details:</h3>");
                emailBody.AppendLine("<ul>");
                emailBody.AppendLine($"<li><strong>Name:</strong> {user.FirstName} {user.LastName}</li>");
                emailBody.AppendLine($"<li><strong>Email:</strong> {user.Email}</li>");
                emailBody.AppendLine("</ul>");
                emailBody.AppendLine("<p>You can now start learning on Pulse Connect!</p>");
                emailBody.AppendLine("<p>If you have any questions, please contact our support team.</p>");

                await _emailSender
                    .To(user.Email)
                    .Subject("Welcome to Pulse Connect!")
                    .Body(emailBody.ToString(), isHtml: true)
                    .SendAsync();

                return Ok(new { Message = "User registered successfully. Welcome email sent." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    innerException = ex.InnerException?.Message,
                    trace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Authenticates a user and generates JWT token
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDTO request)
        {
            if (request == null || !ModelState.IsValid)
                return BadRequest("Invalid login request");

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
                return Unauthorized(new AuthResponseDTO { ErrorMessage = "Invalid email or password" });

            // Generate OTP for 2FA
            var otp = new Random().Next(100000, 999999).ToString();
            var cacheKey = $"OTP_{user.Email}";
            _cache.Set(cacheKey, otp, TimeSpan.FromMinutes(5)); // OTP valid for 5 minutes

            // Send OTP email
            var emailBody = $@"
                <h2>Pulse Connect Login Verification</h2>
                <p>Hello {user.FirstName},</p>
                <p>Your verification code is: <strong>{otp}</strong></p>
                <p>This code will expire in 5 minutes.</p>
                <p>If you didn't request this, please ignore this email.</p>";

            await _emailSender
                .To(user.Email)
                .Subject("Your Pulse Connect Verification Code")
                .Body(emailBody, isHtml: true)
                .SendAsync();

            return Ok(new { Message = "OTP sent to email", Email = user.Email });
        }

        /// <summary>
        /// Verifies OTP and returns JWT token
        /// </summary>
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDTO request)
        {
            var cacheKey = $"OTP_{request.Email}";
            if (!_cache.TryGetValue(cacheKey, out string? cachedOtp) || cachedOtp != request.Otp)
                return Unauthorized(new AuthResponseDTO { ErrorMessage = "Invalid or expired OTP" });

            var user = await _userManager.FindByEmailAsync(request.Email);
            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtHandler.GenerateToken(user, roles);

            _cache.Remove(cacheKey); // Clear OTP after successful verification

            return Ok(new AuthResponseDTO
            {
                IsSuccessful = true,
                Token = token,
                User = new
                {
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.PhoneNumber,
                    Roles = roles
                }
            });
        }

        /// <summary>
        /// Handles password reset requests
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Ok(); // Don't reveal if user doesn't exist

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var encodedEmail = WebUtility.UrlEncode(user.Email);

            // Frontend password reset URL
            var resetUrl = $"http://localhost:4200/reset-password?token={encodedToken}&email={encodedEmail}";

            var emailBody = $@"
                <h2>Pulse Connect Password Reset</h2>
                <p>Hello {user.FirstName},</p>
                <p>We received a request to reset your password. Click the button below to proceed:</p>
                <div style='margin: 20px 0;'>
                    <a href='{resetUrl}' 
                       style='background-color: #4CAF50; 
                              color: white; 
                              padding: 10px 20px; 
                              text-decoration: none; 
                              border-radius: 5px;
                              display: inline-block;'>
                        Reset Password
                    </a>
                </div>
                <p>If you didn't request this, please ignore this email.</p>";

            await _emailSender
                .To(user.Email)
                .Subject("Pulse Connect Password Reset")
                .Body(emailBody, isHtml: true)
                .SendAsync();

            return Ok(new { Message = "Password reset link sent to email" });
        }

        /// <summary>
        /// Resets user password with provided token
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return BadRequest("Invalid request");

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!result.Succeeded)
                return BadRequest(result.Errors.Select(e => e.Description));

            // Send confirmation email
            var emailBody = $@"
                <h2>Password Changed Successfully</h2>
                <p>Hello {user.FirstName},</p>
                <p>Your Pulse Connect password has been successfully changed.</p>
                <p>If you didn't make this change, please contact our support team immediately.</p>";

            await _emailSender
                .To(user.Email)
                .Subject("Your Pulse Connect Password Has Been Changed")
                .Body(emailBody, isHtml: true)
                .SendAsync();

            return Ok(new { Message = "Password reset successfully" });
        }

        /// <summary>
        /// Gets current user profile
        /// </summary>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            // Get user from the token claims
            var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();


            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                user.DateOfBirth,
                user.Address,
                user.Race,
                user.Gender,
                Roles = roles
            });
        }

        /// <summary>
        /// Updates user profile
        /// </summary>
        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileDTO request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound("User not found");

            // Update basic profile fields
            user.FirstName = request.FirstName ?? user.FirstName;
            user.LastName = request.LastName ?? user.LastName;
            user.PhoneNumber = request.PhoneNumber ?? user.PhoneNumber;
            user.DateOfBirth = request.DateOfBirth ?? user.DateOfBirth;
            user.Address = request.Address ?? user.Address;
            user.Race = request.Race ?? user.Race;
            user.Gender = request.Gender ?? user.Gender;

            // Handle profile picture upload
            if (request.ProfilePicture != null && request.ProfilePicture.Length > 0)
            {
                // Delete old profile picture if exists
                if (!string.IsNullOrEmpty(user.ProfilePicture))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfilePicture);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // Save new profile picture
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "profile-pictures");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{request.ProfilePicture.FileName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await request.ProfilePicture.CopyToAsync(fileStream);
                }

                user.ProfilePicture = Path.Combine("profile-pictures", uniqueFileName);
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors.Select(e => e.Description));

            return Ok(new
            {
                Message = "Profile updated successfully",
                ProfilePictureUrl = user.ProfilePicture != null ?
                    $"{Request.Scheme}://{Request.Host}/{user.ProfilePicture}" : null
            });
        }
    }
}