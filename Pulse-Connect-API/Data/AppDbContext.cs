using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pulse_Connect_API.Models;
using Pulse_Connect_API.SeedConfigurations;

namespace Pulse_Connect_API.Data;
  

public class AppDbContext : IdentityDbContext<User, Role, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Apply Role configurations
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasData(
                new Role
                {
                    Id = "1",
                    Name = "User",
                    NormalizedName = "USER",
                    Description = "Standard user role"
                },
                new Role
                {
                    Id = "2",
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    Description = "Administrator role"
                }
            );
        });
    }

    public DbSet<User> Users { get; set; }
}



