using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pulse_Connect_API.Models;

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

        // Configure relationships
        modelBuilder.Entity<Course>()
            .HasOne(c => c.Instructor)
            .WithMany()
            .HasForeignKey(c => c.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Enrollment>()
            .HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Enrollment>()
            .HasOne(e => e.Course)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserChapterProgress relationships
        modelBuilder.Entity<UserChapterProgress>()
            .HasOne(ucp => ucp.Enrollment)
            .WithMany(e => e.ChapterProgress)
            .HasForeignKey(ucp => ucp.EnrollmentId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserChapterProgress>()
            .HasOne(ucp => ucp.Chapter)
            .WithMany(c => c.UserProgresses) // Specify inverse navigation property
            .HasForeignKey(ucp => ucp.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserChapterProgress>()
            .Property(ucp => ucp.ChapterId)
            .HasColumnName("ChapterId"); // Explicitly map ChapterId

        modelBuilder.Entity<UserChapterProgress>()
            .Property(ucp => ucp.EnrollmentId)
            .HasColumnName("EnrollmentId"); // Explicitly map EnrollmentId

        // TestAttempt relationships
        modelBuilder.Entity<TestAttempt>()
            .HasOne(ta => ta.Enrollment)
            .WithMany(e => e.TestAttempts)
            .HasForeignKey(ta => ta.EnrollmentId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<TestAttempt>()
            .HasOne(ta => ta.Test)
            .WithMany()
            .HasForeignKey(ta => ta.TestId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserAnswer relationships
        modelBuilder.Entity<UserAnswer>()
            .HasOne(ua => ua.TestAttempt)
            .WithMany(ta => ta.UserAnswers)
            .HasForeignKey(ua => ua.TestAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserAnswer>()
            .HasOne(ua => ua.Question)
            .WithMany()
            .HasForeignKey(ua => ua.QuestionId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserAnswer>()
            .HasOne(ua => ua.SelectedOption)
            .WithMany()
            .HasForeignKey(ua => ua.SelectedOptionId)
            .OnDelete(DeleteBehavior.NoAction);

        // Other relationships
        modelBuilder.Entity<QuestionOption>()
            .HasOne(qo => qo.Question)
            .WithMany(tq => tq.Options)
            .HasForeignKey(qo => qo.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestQuestion>()
            .HasOne(tq => tq.Test)
            .WithMany(ct => ct.Questions)
            .HasForeignKey(tq => tq.TestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Chapter>()
            .HasOne(ch => ch.Course)
            .WithMany(c => c.Chapters)
            .HasForeignKey(ch => ch.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CourseTest>()
            .HasOne(ct => ct.Course)
            .WithOne(c => c.CourseTest)
            .HasForeignKey<CourseTest>(ct => ct.CourseId)
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<Certificate>()
    .HasOne(c => c.User)
    .WithMany()
    .HasForeignKey(c => c.UserId)
    .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Certificate>()
            .HasOne(c => c.Course)
            .WithMany()
            .HasForeignKey(c => c.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Certificate>()
            .HasOne(c => c.TestAttempt)
            .WithMany()
            .HasForeignKey(c => c.TestAttemptId)
            .OnDelete(DeleteBehavior.NoAction);
    }
    public DbSet<User> Users { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Chapter> Chapters { get; set; }
    public DbSet<CourseTest> CourseTests { get; set; }
    public DbSet<TestQuestion> TestQuestions { get; set; }
    public DbSet<QuestionOption> QuestionOptions { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<UserChapterProgress> UserChapterProgresses { get; set; }
    public DbSet<TestAttempt> TestAttempts { get; set; }
    public DbSet<UserAnswer> UserAnswers { get; set; }

    public DbSet<Certificate> Certificates { get; set; }
}