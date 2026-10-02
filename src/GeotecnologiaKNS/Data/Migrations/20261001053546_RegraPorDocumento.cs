using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegraPorDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Restricao",
                table: "Regras",
                type: "int",
                nullable: true);

            // A EMB-002 entra nas políticas já personalizadas, e não só no
            // protocolo padrão. Política própria normalmente não herda mudança
            // do padrão — e está certo, é dela. Mas aqui a regra nova é uma
            // proteção: deixar de fora significaria a indústria achar que está
            // coberta contra embargo em nome do produtor e não estar, sem o
            // laudo sequer dizer que a regra existe. Quem não quiser, desativa.
            migrationBuilder.Sql(@"
                INSERT INTO Regras (PoliticaId, Codigo, Descricao, Tipo, Restricao,
                                    Severidade, AreaMinimaHa, PercentualMinimo, Fundamento, Ativa)
                SELECT p.Id,
                       'EMB-002',
                       'Embargo ambiental em nome do produtor',
                       0,
                       1,
                       2,
                       0,
                       0,
                       'Autuado com termo de embargo vigente pelo IBAMA.',
                       1
                  FROM Politicas p
                 WHERE NOT EXISTS (SELECT 1 FROM Regras r
                                    WHERE r.PoliticaId = p.Id AND r.Codigo = 'EMB-002');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Restricao",
                table: "Regras");
        }
    }
}
