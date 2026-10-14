using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceDesk.Infrastructure.Persistence;

#nullable disable

namespace ServiceDesk.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261014230000_AddRequesterCreatedAtIdIndex")]
    public partial class AddRequesterCreatedAtIdIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_tickets_requester_id_created_at_id",
                schema: "public",
                table: "tickets",
                columns: new[] { "requester_id", "created_at", "Id" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tickets_requester_id_created_at_id",
                schema: "public",
                table: "tickets");
        }
    }
}
