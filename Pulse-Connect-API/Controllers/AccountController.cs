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
using Pulse_Connect_API.Service;
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
        private readonly RoleManager<Role> _roleManager; // Change from IdentityRole to Role
        private readonly IMapper _mapper;
        private readonly JwtService _jwtService;
        private readonly IMemoryCache _cache;
        private readonly IFluentEmail _emailSender;

        public AccountController(
            UserManager<User> userManager,
            RoleManager<Role> roleManager, // Change from IdentityRole to Role
            IMapper mapper,
            JwtService jwtService,
            IMemoryCache cache,
            IFluentEmail emailSender)
        {
            _userManager = userManager;
            _roleManager = roleManager; // Updated
            _mapper = mapper;
            _jwtService = jwtService;
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

            // Get user roles and generate token directly
            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtService.GenerateToken(user, roles);

            // Send login notification email instead of OTP
            var emailBody = $@"
        <h2>Pulse Connect Login Notification</h2>
        <p>Hello {user.FirstName},</p>
        <p>Your account was successfully logged in at {DateTime.Now.ToString("f")}.</p>
        <p>If this wasn't you, please contact our support team immediately.</p>";

            await _emailSender
                .To(user.Email)
                .Subject("Pulse Connect Login Notification")
                .Body(emailBody, isHtml: true)
                .SendAsync();

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

        // Remove the VerifyOtp endpoint completely
        /// <summary>
        /// Verifies OTP and returns JWT token
        /// </summary>


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
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Invalid or missing user ID in token");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var roles = await _userManager.GetRolesAsync(user);

            // Build full profile picture URL if it exists
            string profilePhotoUrl = null;
            if (!string.IsNullOrEmpty(user.ProfilePicture))
            {
                // Ensure the path starts with a slash
                var cleanPath = user.ProfilePicture.StartsWith("/") ? user.ProfilePicture : $"/{user.ProfilePicture}";
                profilePhotoUrl = $"{Request.Scheme}://{Request.Host}{cleanPath}";
            }

            return Ok(new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                DateOfBirth = user.DateOfBirth?.ToString("yyyy-MM-dd"),
                user.Address,
                user.Race,
                user.Gender,
                ProfilePhoto = profilePhotoUrl, // This should match your frontend interface
                Roles = roles
            });
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileDTO request)
        {
            try
            {
                Console.WriteLine($"=== UPDATE PROFILE REQUEST ===");
                Console.WriteLine($"FirstName: {request.FirstName}");
                Console.WriteLine($"LastName: {request.LastName}");
                Console.WriteLine($"PhoneNumber: {request.PhoneNumber}");
                Console.WriteLine($"DateOfBirth: {request.DateOfBirth}");
                Console.WriteLine($"Address: {request.Address}");
                Console.WriteLine($"Race: {request.Race}");
                Console.WriteLine($"Gender: {request.Gender}");
                Console.WriteLine($"ProfilePicture: {request.ProfilePicture?.FileName ?? "No file"}");

                // Manual validation
                if (string.IsNullOrWhiteSpace(request.FirstName))
                {
                    return BadRequest(new { errors = new[] { "First name is required" } });
                }

                if (string.IsNullOrWhiteSpace(request.LastName))
                {
                    return BadRequest(new { errors = new[] { "Last name is required" } });
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound(new { errors = new[] { "User not found" } });
                }

                // Update fields
                user.FirstName = request.FirstName.Trim();
                user.LastName = request.LastName.Trim();
                user.PhoneNumber = string.IsNullOrEmpty(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

                // Handle DateOfBirth - accept null or empty
                if (!string.IsNullOrEmpty(request.DateOfBirth))
                {
                    if (DateTime.TryParse(request.DateOfBirth, out var dateOfBirth))
                    {
                        user.DateOfBirth = dateOfBirth;
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Could not parse DateOfBirth: {request.DateOfBirth}");
                        user.DateOfBirth = null;
                    }
                }
                else
                {
                    user.DateOfBirth = null;
                }

                user.Address = string.IsNullOrEmpty(request.Address) ? null : request.Address.Trim();
                user.Race = string.IsNullOrEmpty(request.Race) ? null : request.Race.Trim();
                user.Gender = string.IsNullOrEmpty(request.Gender) ? null : request.Gender.Trim();

                // Handle profile picture upload
                if (request.ProfilePicture != null && request.ProfilePicture.Length > 0)
                {
                    Console.WriteLine($"Processing profile picture: {request.ProfilePicture.FileName}");

                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".jfif", ".webp" };
                    var fileExtension = Path.GetExtension(request.ProfilePicture.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return BadRequest(new { errors = new[] { "Invalid file type. Only JPG, JPEG, PNG, GIF, JFIF, and WEBP are allowed." } });
                    }

                    // Validate file size (5MB)
                    if (request.ProfilePicture.Length > 5 * 1024 * 1024)
                    {
                        return BadRequest(new { errors = new[] { "File size too large. Maximum size is 5MB." } });
                    }

                    // Delete old profile picture if exists
                    if (!string.IsNullOrEmpty(user.ProfilePicture))
                    {
                        try
                        {
                            var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfilePicture.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                                Console.WriteLine($"Deleted old profile picture: {oldFilePath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Could not delete old profile picture: {ex.Message}");
                            // Continue with update even if delete fails
                        }
                    }

                    // Save new profile picture
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "profile-pictures");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                        Console.WriteLine($"Created uploads directory: {uploadsFolder}");
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(request.ProfilePicture.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await request.ProfilePicture.CopyToAsync(fileStream);
                    }

                    user.ProfilePicture = $"/profile-pictures/{uniqueFileName}";
                    Console.WriteLine($"Profile picture saved: {user.ProfilePicture}");
                }
                else
                {
                    Console.WriteLine("No profile picture provided, keeping existing one");
                }

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errorMessages = result.Errors.Select(e => e.Description).ToArray();
                    Console.WriteLine($"UserManager update errors: {string.Join(", ", errorMessages)}");

                    return BadRequest(new { errors = errorMessages });
                }

                Console.WriteLine("Profile updated successfully");

                // Build the full profile picture URL for response
                string profilePictureUrl = null;
                if (!string.IsNullOrEmpty(user.ProfilePicture))
                {
                    profilePictureUrl = $"{Request.Scheme}://{Request.Host}{user.ProfilePicture}";
                }

                return Ok(new
                {
                    Message = "Profile updated successfully",
                    ProfilePictureUrl = profilePictureUrl,
                    User = new
                    {
                        user.Id,
                        user.FirstName,
                        user.LastName,
                        user.Email,
                        user.PhoneNumber,
                        DateOfBirth = user.DateOfBirth?.ToString("yyyy-MM-dd"),
                        user.Address,
                        user.Race,
                        user.Gender,
                        ProfilePhoto = profilePictureUrl
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== EXCEPTION IN UPDATEPROFILE ===");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }

                return StatusCode(500, new
                {
                    errors = new[] { "An unexpected error occurred while updating the profile." }
                });
            }
        }


        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = _userManager.Users.ToList();
                var userDtos = new List<UserManagementDTO>();

                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    userDtos.Add(new UserManagementDTO
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        DateOfBirth = (DateTime)user.DateOfBirth,
                        Address = user.Address,
                        Race = user.Race,
                        Gender = user.Gender,
                        EmailConfirmed = user.EmailConfirmed,
                        IsActive = user.LockoutEnd == null || user.LockoutEnd < DateTime.Now,
                        Roles = roles.ToList(),
                        CreatedAt = user.CreatedAt
                    });
                }

                return Ok(userDtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserById(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return NotFound("User not found");

                var roles = await _userManager.GetRolesAsync(user);

                var userDto = new UserManagementDTO
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    DateOfBirth = (DateTime)user.DateOfBirth,
                    Address = user.Address,
                    Race = user.Race,
                    Gender = user.Gender,
                    EmailConfirmed = user.EmailConfirmed,
                    IsActive = user.LockoutEnd == null || user.LockoutEnd < DateTime.Now,
                    Roles = roles.ToList(),
                    CreatedAt = user.CreatedAt,
                    ProfilePicture = user.ProfilePicture != null ?
                        $"{Request.Scheme}://{Request.Host}/{user.ProfilePicture}" : null
                };

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Updates user roles (Admin only)
        /// </summary>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPut("users/{id}/roles")]
        public async Task<IActionResult> UpdateUserRoles(string id, [FromBody] UpdateUserRolesDTO request)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return NotFound("User not found");

                // Get current roles
                var currentRoles = await _userManager.GetRolesAsync(user);

                // Remove existing roles
                var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removeResult.Succeeded)
                    return BadRequest(removeResult.Errors.Select(e => e.Description));

                // Add new roles
                var addResult = await _userManager.AddToRolesAsync(user, request.Roles);
                if (!addResult.Succeeded)
                    return BadRequest(addResult.Errors.Select(e => e.Description));

                // Send notification email to user
                var emailBody = $@"
                    <h2>Your Roles Have Been Updated</h2>
                    <p>Hello {user.FirstName},</p>
                    <p>Your account roles have been updated by an administrator.</p>
                    <p><strong>New Roles:</strong> {string.Join(", ", request.Roles)}</p>
                    <p>If you believe this is an error, please contact our support team.</p>";

                await _emailSender
                    .To(user.Email)
                    .Subject("Pulse Connect - Account Roles Updated")
                    .Body(emailBody, isHtml: true)
                    .SendAsync();

                return Ok(new { Message = "User roles updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Toggles user active status (Admin only)
        /// </summary>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("users/{id}/toggle-active")]
        public async Task<IActionResult> ToggleUserActiveStatus(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return NotFound("User not found");

                if (user.LockoutEnd == null || user.LockoutEnd < DateTime.Now)
                {
                    // Deactivate user
                    await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

                    var emailBody = $@"
                        <h2>Account Deactivated</h2>
                        <p>Hello {user.FirstName},</p>
                        <p>Your Pulse Connect account has been deactivated by an administrator.</p>
                        <p>If you believe this is an error, please contact our support team.</p>";

                    await _emailSender
                        .To(user.Email)
                        .Subject("Pulse Connect - Account Deactivated")
                        .Body(emailBody, isHtml: true)
                        .SendAsync();

                    return Ok(new { Message = "User deactivated successfully" });
                }
                else
                {
                    // Activate user
                    await _userManager.SetLockoutEndDateAsync(user, null);

                    var emailBody = $@"
                        <h2>Account Reactivated</h2>
                        <p>Hello {user.FirstName},</p>
                        <p>Your Pulse Connect account has been reactivated by an administrator.</p>
                        <p>You can now access your account normally.</p>";

                    await _emailSender
                        .To(user.Email)
                        .Subject("Pulse Connect - Account Reactivated")
                        .Body(emailBody, isHtml: true)
                        .SendAsync();

                    return Ok(new { Message = "User activated successfully" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets available roles (Admin only)
        /// </summary>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("roles")]
        public IActionResult GetAvailableRoles()
        {
            try
            {
                var roles = _roleManager.Roles.Select(r => r.Name).ToList();
                return Ok(roles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Deletes a user (Admin only)
        /// </summary>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return NotFound("User not found");

                // Prevent deletion of your own account
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (user.Id == currentUserId)
                    return BadRequest("Cannot delete your own account");

                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                    return BadRequest(result.Errors.Select(e => e.Description));

                // Send notification email
                var emailBody = $@"
            <h2>Account Deleted</h2>
            <p>Hello {user.FirstName},</p>
            <p>Your Pulse Connect account has been deleted by an administrator.</p>
            <p>If you believe this is an error, please contact our support team.</p>";

                await _emailSender
                    .To(user.Email)
                    .Subject("Pulse Connect - Account Deleted")
                    .Body(emailBody, isHtml: true)
                    .SendAsync();

                return Ok(new { Message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }


}


