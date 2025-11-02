using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pulse_Connect_API.Data;
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

        // COMMUNITY ENTITIES - FIXED CASCADE DELETE ISSUES
        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasOne(p => p.Author)
                  .WithMany()
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.NoAction); // Changed to NoAction
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasOne(c => c.Author)
                  .WithMany()
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.NoAction); // Changed to NoAction

            entity.HasOne(c => c.Post)
                  .WithMany(p => p.Comments)
                  .HasForeignKey(c => c.PostId)
                  .OnDelete(DeleteBehavior.NoAction); // Changed from Cascade to NoAction

            entity.HasOne(c => c.ParentComment)
                  .WithMany(c => c.Replies)
                  .HasForeignKey(c => c.ParentCommentId)
                  .OnDelete(DeleteBehavior.NoAction); // Changed to NoAction
        });

        modelBuilder.Entity<PostLike>(entity =>
        {
            entity.HasKey(pl => pl.Id);

            entity.HasOne(pl => pl.Post)
                  .WithMany(p => p.PostLikes)
                  .HasForeignKey(pl => pl.PostId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pl => pl.User)
                  .WithMany()
                  .HasForeignKey(pl => pl.UserId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(pl => new { pl.PostId, pl.UserId }).IsUnique();
        });

        modelBuilder.Entity<UserProvince>(entity =>
        {
            entity.HasOne(up => up.User)
                  .WithMany()
                  .HasForeignKey(up => up.UserId)
                  .OnDelete(DeleteBehavior.NoAction); // Changed to NoAction

            entity.HasIndex(up => new { up.UserId, up.Province }).IsUnique();
        });

     

        // EXISTING COURSE CONFIGURATIONS (keep as is)
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

        modelBuilder.Entity<UserChapterProgress>()
            .HasOne(ucp => ucp.Enrollment)
            .WithMany(e => e.ChapterProgress)
            .HasForeignKey(ucp => ucp.EnrollmentId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserChapterProgress>()
            .HasOne(ucp => ucp.Chapter)
            .WithMany(c => c.UserProgresses)
            .HasForeignKey(ucp => ucp.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

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

        modelBuilder.Entity<QuestionOption>()
            .HasOne(qo => qo.Question)
            .WithMany(tq => tq.Options)
            .HasForeignKey(qo => qo.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CertificateShare>(entity =>
        {
            entity.HasOne(cs => cs.User)
                  .WithMany()
                  .HasForeignKey(cs => cs.UserId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(cs => cs.Certificate)
                  .WithMany()
                  .HasForeignKey(cs => cs.CertificateId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

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

        modelBuilder.Entity<PostImage>(entity =>
        {
            entity.HasOne(pi => pi.Post)
                  .WithMany(p => p.Images)
                  .HasForeignKey(pi => pi.PostId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Address).IsRequired();
            entity.Property(u => u.Race).IsRequired();
            entity.Property(u => u.Gender).IsRequired();
            entity.Property(u => u.DateOfBirth).IsRequired();
        });

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

    // Community entities
    public DbSet<Post> Posts { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<UserProvince> UserProvinces { get; set; }

    public DbSet<PostImage> PostImages { get; set; }

    public DbSet<CertificateShare> CertificateShares { get; set; }


    public DbSet<PostLike> PostLikes { get; set; }

    public DbSet<Notification> Notifications { get; set; }

    public DbSet<BadgeEarning> BadgeEarnings { get; set; }
}
