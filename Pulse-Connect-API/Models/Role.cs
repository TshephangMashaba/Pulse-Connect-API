using Microsoft.AspNetCore.Identity;

namespace Pulse_Connect_API.Models
{
    public class Role : IdentityRole<string>
    {
        public string Description { get; set; }
    }
}