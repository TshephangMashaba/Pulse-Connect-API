// ContactController.cs
using FluentEmail.Core;
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
    public class ContactController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IFluentEmail _emailSender;
        private readonly IConfiguration _configuration;

        public ContactController(
            AppDbContext context,
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            IFluentEmail emailSender,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _configuration = configuration;
        }

        // POST: api/Contact/submit
        [HttpPost("submit")]
        [AllowAnonymous] // No authentication required
        public async Task<ActionResult<ContactResponseDto>> SubmitContactForm([FromBody] ContactDto contactDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new ContactResponseDto
                {
                    Success = false,
                    Message = "Validation failed",
                    Errors = errors
                });
            }

            try
            {
                // Get all admin users using UserManager
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");

                // If no admins found, try alternative role names
                if (!adminUsers.Any())
                {
                    adminUsers = await _userManager.GetUsersInRoleAsync("Administrator");
                }

                // Use admin emails or fallback to pulseconnecthub@gmail.com
                var adminEmails = adminUsers.Select(u => u.Email).Where(e => !string.IsNullOrEmpty(e)).ToList();

                if (!adminEmails.Any())
                {
                    // Use the configured default admin email
                    adminEmails.Add(_configuration["Contact:DefaultAdminEmail"] ?? "pulseconnecthub@gmail.com");

                    // Log that we're using fallback email
                    Console.WriteLine("No admin users found with Admin/Administrator role. Using fallback email: pulseconnecthub@gmail.com");
                }

                // Get configuration values with fallbacks
                var supportEmail = _configuration["Contact:SupportEmail"] ?? "pulseconnecthub@gmail.com";
                var siteName = _configuration["Site:Name"] ?? "Pulse Connect";
                var siteUrl = _configuration["Site:Url"] ?? "https://pulse-connect3123.web.app";

                // Create email body for admins
                var adminEmailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 0 auto; background: #ffffff; }}
        .header {{ background: linear-gradient(135deg, #10b981, #059669); color: white; padding: 30px 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f8f9fa; }}
        .message-box {{ background: white; padding: 25px; border-radius: 8px; border-left: 4px solid #10b981; margin: 20px 0; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .user-info {{ background: white; padding: 20px; border-radius: 8px; margin: 15px 0; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .footer {{ padding: 25px; text-align: center; color: #6b7280; font-size: 14px; background: #f1f5f9; }}
        .reply-instruction {{ background: #e8f5e8; padding: 20px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #10b981; }}
        .badge {{ display: inline-block; padding: 4px 12px; background: #10b981; color: white; border-radius: 20px; font-size: 12px; margin-left: 10px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0; font-size: 28px;'>📧 New Contact Message</h1>
            <p style='margin: 10px 0 0 0; opacity: 0.9;'>From {siteName} Contact Form</p>
        </div>
        
        <div class='content'>
            <div class='user-info'>
                <h3 style='color: #10b981; margin-top: 0;'>👤 Contact Information</h3>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px 0; width: 120px; font-weight: bold;'>Name:</td>
                        <td style='padding: 8px 0;'>{contactDto.FirstName} {contactDto.LastName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; font-weight: bold;'>Email:</td>
                        <td style='padding: 8px 0;'>
                            <a href='mailto:{contactDto.Email}' style='color: #10b981; text-decoration: none;'>{contactDto.Email}</a>
                            <span class='badge'>Click to Respond</span>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; font-weight: bold;'>Phone:</td>
                        <td style='padding: 8px 0;'>{contactDto.PhoneNumber ?? "Not provided"}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; font-weight: bold;'>Submitted:</td>
                        <td style='padding: 8px 0;'>{DateTime.UtcNow.ToString("f")} UTC</td>
                    </tr>
                </table>
            </div>

            <div class='message-box'>
                <h3 style='color: #10b981; margin-top: 0;'>📝 Message Details</h3>
                <p><strong>Subject:</strong> {contactDto.Subject}</p>
                <div style='background: #f9f9f9; padding: 20px; border-radius: 6px; margin-top: 15px; border: 1px solid #e5e7eb;'>
                    {contactDto.Message.Replace("\n", "<br>")}
                </div>
            </div>

            <div class='reply-instruction'>
                <h3 style='color: #059669; margin-top: 0;'>💡 How to Respond</h3>
                <p>To reply to this user, simply <strong>click the email address above</strong> or send a new email to:</p>
                <p style='text-align: center; font-size: 18px; font-weight: bold; color: #10b981; background: white; padding: 15px; border-radius: 6px; margin: 15px 0;'>
                    📧 {contactDto.Email}
                </p>
                <p style='margin-bottom: 0;'>Your response will go directly to the user who submitted this contact form.</p>
            </div>
        </div>

        <div class='footer'>
            <p style='margin: 0 0 10px 0;'>
                <strong>{siteName}</strong> | <a href='{siteUrl}' style='color: #10b981; text-decoration: none;'>{siteUrl}</a>
            </p>
            <p style='margin: 0; font-size: 12px; opacity: 0.7;'>
                This email was automatically generated from the contact form. 
                Please do not reply to this automated message.
            </p>
        </div>
    </div>
</body>
</html>";

                // Send email to all admins
                var emailTasks = new List<Task>();
                foreach (var adminEmail in adminEmails)
                {
                    if (!string.IsNullOrEmpty(adminEmail))
                    {
                        var emailTask = _emailSender
                            .To(adminEmail)
                            .Subject($"📧 New Contact: {contactDto.Subject} - {contactDto.FirstName} {contactDto.LastName}")
                            .Body(adminEmailBody, isHtml: true)
                            .SendAsync();
                        emailTasks.Add(emailTask);
                    }
                }

                // Wait for all admin emails to be sent
                if (emailTasks.Any())
                {
                    await Task.WhenAll(emailTasks);
                }

                // Send confirmation email to the user
                var userConfirmationBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 0 auto; background: #ffffff; }}
        .header {{ background: linear-gradient(135deg, #10b981, #059669); color: white; padding: 30px 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f8f9fa; }}
        .footer {{ padding: 25px; text-align: center; color: #6b7280; font-size: 14px; background: #f1f5f9; }}
        .info-box {{ background: white; padding: 20px; border-radius: 8px; margin: 15px 0; box-shadow: 0 2px 4px rgba(0,0,0,0.1); border-left: 4px solid #10b981; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0; font-size: 28px;'>✓ Message Received!</h1>
            <p style='margin: 10px 0 0 0; opacity: 0.9;'>Thank you for contacting {siteName}</p>
        </div>
        
        <div class='content'>
            <p>Hello <strong>{contactDto.FirstName}</strong>,</p>
            
            <p>Thank you for reaching out to us! We've successfully received your message and our team will review it shortly.</p>
            
            <div class='info-box'>
                <p style='margin: 0 0 10px 0;'><strong>📋 Message Summary:</strong></p>
                <p style='margin: 5px 0;'><strong>Subject:</strong> {contactDto.Subject}</p>
                <p style='margin: 5px 0;'><strong>Submitted:</strong> {DateTime.UtcNow.ToString("f")}</p>
                <p style='margin: 5px 0;'><strong>Reference:</strong> PC-{DateTime.UtcNow:yyyyMMdd-HHmmss}</p>
            </div>

            <p><strong>📅 What to expect next:</strong></p>
            <ul>
                <li>Our team will review your message promptly</li>
                <li>We typically respond within <strong>24-48 hours</strong></li>
                <li>You'll receive a personal response at: <strong>{contactDto.Email}</strong></li>
            </ul>

            <p>If you need immediate assistance, please feel free to contact us directly at 
               <a href='mailto:{supportEmail}' style='color: #10b981; text-decoration: none;'><strong>{supportEmail}</strong></a>.</p>

            <p>We appreciate you choosing <strong>{siteName}</strong>!</p>
        </div>

        <div class='footer'>
            <p style='margin: 0 0 10px 0;'>
                <strong>{siteName}</strong> | <a href='{siteUrl}' style='color: #10b981; text-decoration: none;'>{siteUrl}</a>
            </p>
            <p style='margin: 0; font-size: 12px; opacity: 0.7;'>
                This is an automated confirmation. Please do not reply to this email.
            </p>
        </div>
    </div>
</body>
</html>";

                await _emailSender
                    .To(contactDto.Email)
                    .Subject($"✓ Confirmation: We've Received Your Message - {siteName}")
                    .Body(userConfirmationBody, isHtml: true)
                    .SendAsync();

                // Log the contact submission
                await LogContactSubmission(contactDto);

                return Ok(new ContactResponseDto
                {
                    Success = true,
                    Message = "Your message has been sent successfully! We've sent a confirmation email to your inbox."
                });
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error sending contact email: {ex.Message}");

                return StatusCode(500, new ContactResponseDto
                {
                    Success = false,
                    Message = "An error occurred while sending your message. Please try again later or contact us directly at pulseconnecthub@gmail.com."
                });
            }
        }

        // GET: api/Contact/admin-users (to check available admin users)
        [HttpGet("admin-users")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<ActionResult> GetAdminUsers()
        {
            try
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                var administratorUsers = await _userManager.GetUsersInRoleAsync("Administrator");

                var allAdmins = adminUsers.Union(administratorUsers).Distinct().ToList();

                var result = allAdmins.Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.UserName,
                    Roles = _userManager.GetRolesAsync(u).Result
                });

                return Ok(new
                {
                    AdminUsers = result,
                    TotalCount = allAdmins.Count,
                    FallbackEmail = _configuration["Contact:DefaultAdminEmail"] ?? "pulseconnecthub@gmail.com"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST: api/Contact/test-email (for testing email functionality)
        [HttpPost("test-email")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<ActionResult> TestEmail([FromBody] TestEmailDto testEmailDto)
        {
            try
            {
                var testBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; }}
        .test-message {{ background: #e8f5e8; padding: 20px; border-radius: 8px; }}
    </style>
</head>
<body>
    <h2>🧪 Test Email - Pulse Connect</h2>
    <div class='test-message'>
        <p>This is a test email to verify that the contact system is working correctly.</p>
        <p><strong>Time sent:</strong> {DateTime.UtcNow.ToString("f")} UTC</p>
        <p><strong>Test message:</strong> {testEmailDto.Message}</p>
        <p><strong>Sent to:</strong> {testEmailDto.Email}</p>
    </div>
    <p>If you received this email, the Pulse Connect contact system is working properly! ✅</p>
</body>
</html>";

                await _emailSender
                    .To(testEmailDto.Email)
                    .Subject("🧪 Test Email - Pulse Connect Contact System")
                    .Body(testBody, isHtml: true)
                    .SendAsync();

                return Ok(new { success = true, message = $"Test email sent successfully to {testEmailDto.Email}!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Failed to send test email: {ex.Message}" });
            }
        }

        private async Task LogContactSubmission(ContactDto contactDto)
        {
            try
            {
                var contactLog = new ContactSubmission
                {
                    Id = Guid.NewGuid().ToString(),
                    FirstName = contactDto.FirstName,
                    LastName = contactDto.LastName,
                    Email = contactDto.Email,
                    PhoneNumber = contactDto.PhoneNumber,
                    Subject = contactDto.Subject,
                    Message = contactDto.Message,
                    SubmittedAt = DateTime.UtcNow,
                    IsProcessed = false
                };

                _context.ContactSubmissions.Add(contactLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to log contact submission to database: {ex.Message}");
            }
        }
    }

    // DTO for test email
    public class TestEmailDto
    {
        public string Email { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}