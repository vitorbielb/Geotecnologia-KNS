using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class RemoveGeozone : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Propriedades_Geozones_GeozoneId",
                table: "Propriedades");

            migrationBuilder.DropTable(
                name: "Geozones");

            migrationBuilder.DropIndex(
                name: "IX_Propriedades_GeozoneId",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "GeozoneId",
                table: "Propriedades");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GeozoneId",
                table: "Propriedades",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Geozones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Utm = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Geozones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Propriedades_GeozoneId",
                table: "Propriedades",
                column: "GeozoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_Propriedades_Geozones_GeozoneId",
                table: "Propriedades",
                column: "GeozoneId",
                principalTable: "Geozones",
                principalColumn: "Id");
        }
    }
}
