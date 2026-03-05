using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Models;

namespace WorkHub.Api.Data;

public static class SeedData
{
    public static async Task SeedAsync(WorkHubDbContext db)
    {
        if (await db.Users.AnyAsync())
            return;

        var users = new[]
        {
            new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@workhub.app",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Name = "Admin User",
                CreatedAt = DateTime.UtcNow,
            },
            new User
            {
                Id = Guid.NewGuid(),
                Email = "user1@workhub.app",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Name = "Team Member 1",
                CreatedAt = DateTime.UtcNow,
            },
            new User
            {
                Id = Guid.NewGuid(),
                Email = "user2@workhub.app",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Name = "Team Member 2",
                CreatedAt = DateTime.UtcNow,
            },
        };

        db.Users.AddRange(users);
        await db.SaveChangesAsync();
    }
}
