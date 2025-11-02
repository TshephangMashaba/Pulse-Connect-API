// ProfileController.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pulse_Connect_API.Models;
using System.Security.Claims;

namespace Pulse_Connect_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ProfileController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _environment;

        public ProfileController(UserManager<User> userManager, IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
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

                return Ok(new
                {
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.PhoneNumber,
                    user.DateOfBirth,
                    ProfilePicture = user.ProfilePicture,
                    user.Address,
                    user.Race,
                    user.Gender
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving profile: {ex.Message}");
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileDto updateProfileDto)
        {
            try
            {
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

                // Update user properties
                user.FirstName = updateProfileDto.FirstName ?? user.FirstName;
                user.LastName = updateProfileDto.LastName ?? user.LastName;
                user.PhoneNumber = updateProfileDto.PhoneNumber ?? user.PhoneNumber;
                user.DateOfBirth = updateProfileDto.DateOfBirth ?? user.DateOfBirth;
                user.Address = updateProfileDto.Address ?? user.Address;
                user.Race = updateProfileDto.Race ?? user.Race;
                user.Gender = updateProfileDto.Gender ?? user.Gender;

                // Handle profile picture upload
                if (updateProfileDto.ProfilePicture != null)
                {
                    var uploadResult = await HandleFileUpload(updateProfileDto.ProfilePicture);
                    if (uploadResult.Success)
                    {
                        user.ProfilePicture = uploadResult.FilePath;
                    }
                    else
                    {
                        return BadRequest(uploadResult.ErrorMessage);
                    }
                }

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(new { Message = "Failed to update profile", Errors = result.Errors });
                }

                return Ok(new
                {
                    Message = "Profile updated successfully",
                    User = new
                    {
                        user.Id,
                        user.FirstName,
                        user.LastName,
                        user.Email,
                        user.PhoneNumber,
                        user.DateOfBirth,
                        ProfilePicture = user.ProfilePicture,
                        user.Address,
                        user.Race,
                        user.Gender
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating profile: {ex.Message}");
            }
        }

        private async Task<(bool Success, string FilePath, string ErrorMessage)> HandleFileUpload(IFormFile file)
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                    return (false, null, "No file provided");

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                    return (false, null, "Invalid file type. Only JPG, PNG, and GIF are allowed.");

                // Validate file size (5MB)
                if (file.Length > 5 * 1024 * 1024)
                    return (false, null, "File size too large. Maximum size is 5MB.");

                // Create uploads directory if it doesn't exist
                var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
                if (!Directory.Exists(uploadsDir))
                    Directory.CreateDirectory(uploadsDir);

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsDir, fileName);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Return relative path for database storage
                var relativePath = $"/uploads/profiles/{fileName}";
                return (true, relativePath, null);
            }
            catch (Exception ex)
            {
                return (false, null, $"Error uploading file: {ex.Message}");
            }
        }
    }

    public class UpdateProfileDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Address { get; set; }
        public string? Race { get; set; }
        public string? Gender { get; set; }
        public IFormFile? ProfilePicture { get; set; }
    }
}