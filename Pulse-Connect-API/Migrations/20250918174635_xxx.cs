using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pulse_Connect_API.Migrations
{
    /// <inheritdoc />
    public partial class xxx : Migration
    {
        /// <inheritdoc />
        // In your migration's Up method
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First drop the foreign key constraints
            migrationBuilder.DropForeignKey(
                name: "FK_TestAttempts_Enrollments_EnrollmentId",
                table: "TestAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_UserChapterProgresses_Enrollments_EnrollmentId",
                table: "UserChapterProgresses");

            // Then recreate them with NO ACTION
            migrationBuilder.AddForeignKey(
                name: "FK_TestAttempts_Enrollments_EnrollmentId",
                table: "TestAttempts",
                column: "EnrollmentId",
                principalTable: "Enrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_UserChapterProgresses_Enrollments_EnrollmentId",
                table: "UserChapterProgresses",
                column: "EnrollmentId",
                principalTable: "Enrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
