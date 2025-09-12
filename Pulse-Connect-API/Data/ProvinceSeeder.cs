// Data/ProvinceSeeder.cs
using Microsoft.EntityFrameworkCore;
using Pulse_Connect_API.Models;

namespace Pulse_Connect_API.Data
{
    public static class ProvinceSeeder
    {
        public static void SeedProvinces(ModelBuilder modelBuilder)
        {
            var provinces = new[]
            {
                "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal",
                "Limpopo", "Mpumalanga", "North West", "Northern Cape", "Western Cape"
            };

            foreach (var province in provinces)
            {
                modelBuilder.Entity<UserProvince>().HasData(
                    new UserProvince
                    {
                        Id = Guid.NewGuid().ToString(),
                        Province = province,
                        UserId = "seed-user", // This will be updated when users join
                        IsActive = false, // Initially inactive until users join
                        JoinedAt = DateTime.UtcNow
                    }
                );
            }
        }
    }
}