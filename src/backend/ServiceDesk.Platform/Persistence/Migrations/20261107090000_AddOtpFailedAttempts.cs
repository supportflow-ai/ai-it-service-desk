using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Platform.Persistence.Migrations
{
    /// <summary>Đếm số lần nhập sai cho từng mã OTP để chặn đoán thử mã đặt lại mật khẩu.</summary>
    public partial class AddOtpFailedAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_attempts",
                schema: "public",
                table: "otp_codes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_attempts",
                schema: "public",
                table: "otp_codes");
        }
    }
}
