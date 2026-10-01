using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegraTrabalhoEscravo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mesma razão da EMB-002: regra nova que é proteção entra também nas
            // políticas já personalizadas. Quem não quiser, desativa na tela —
            // o que não pode é a indústria achar que está coberta e não estar.
            migrationBuilder.Sql(@"
                INSERT INTO Regras (PoliticaId, Codigo, Descricao, Tipo, Restricao,
                                    Severidade, AreaMinimaHa, PercentualMinimo, Fundamento, Ativa)
                SELECT p.Id,
                       'TRB-001',
                       'Cadastro de Empregadores (trabalho análogo à escravidão)',
                       0,
                       2,
                       2,
                       0,
                       0,
                       'Portaria Interministerial MTE/MDHC/MIR nº 18/2024. Compromissos de cadeia produtiva vedam a aquisição.',
                       1
                  FROM Politicas p
                 WHERE NOT EXISTS (SELECT 1 FROM Regras r
                                    WHERE r.PoliticaId = p.Id AND r.Codigo = 'TRB-001');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
