using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Data;
using Pulse_Connect_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Pulse_Connect_API.SeedConfigurations
{
    public class RoleConfigurations
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.HasData(
                new Role
                {
                    Id = "1",
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    Description = "Administrator role with full access"
                },
                new Role
                {
                    Id = "2",
                    Name = "User",
                    NormalizedName = "USER",
                    Description = "User uses the system for learning and day to day tasks"
                }
            );
        }
    }
}
