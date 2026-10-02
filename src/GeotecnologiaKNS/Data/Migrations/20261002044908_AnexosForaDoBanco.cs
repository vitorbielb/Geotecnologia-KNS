using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnexosForaDoBanco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "Dados",
                table: "PropriedadesArquivos",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AddColumn<string>(
                name: "Chave",
                table: "PropriedadesArquivos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Tamanho",
                table: "PropriedadesArquivos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<byte[]>(
                name: "Dados",
                table: "ProdutoresArquivos",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AddColumn<string>(
                name: "Chave",
                table: "ProdutoresArquivos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Tamanho",
                table: "ProdutoresArquivos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<byte[]>(
                name: "Dados",
                table: "AnalisesArquivos",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AddColumn<string>(
                name: "Chave",
                table: "AnalisesArquivos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Tamanho",
                table: "AnalisesArquivos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Chave",
                table: "PropriedadesArquivos");

            migrationBuilder.DropColumn(
                name: "Tamanho",
                table: "PropriedadesArquivos");

            migrationBuilder.DropColumn(
                name: "Chave",
                table: "ProdutoresArquivos");

            migrationBuilder.DropColumn(
                name: "Tamanho",
                table: "ProdutoresArquivos");

            migrationBuilder.DropColumn(
                name: "Chave",
                table: "AnalisesArquivos");

            migrationBuilder.DropColumn(
                name: "Tamanho",
                table: "AnalisesArquivos");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Dados",
                table: "PropriedadesArquivos",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "Dados",
                table: "ProdutoresArquivos",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "Dados",
                table: "AnalisesArquivos",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)",
                oldNullable: true);
        }
    }
}
