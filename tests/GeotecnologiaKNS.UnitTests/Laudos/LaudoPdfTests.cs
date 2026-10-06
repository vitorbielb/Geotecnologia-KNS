using FluentAssertions;
using GeotecnologiaKNS.Laudos;
using GeotecnologiaKNS.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace GeotecnologiaKNS.UnitTests.Laudos
{
    /// <summary>
    /// A emissão do laudo em PDF.
    /// </summary>
    /// <remarks>
    /// Existe porque o documento só falha quando é desenhado: o primeiro teste
    /// real quebrou com "uses font families that are not available", e esse é
    /// um erro que nenhuma compilação pega. Num servidor Linux sem as fontes do
    /// Windows, a emissão morreria em produção e não aqui.
    ///
    /// O laudo é o produto que a indústria arquiva e apresenta em auditoria.
    /// Ele precisa sair mesmo quando falta coisa — sem mapa, sem logo, sem
    /// cadeia declarada —, dizendo o que falta, em vez de não sair.
    /// </remarks>
    public class LaudoPdfTests
    {
        static LaudoPdfTests() => QuestPDF.Settings.License = LicenseType.Community;

        private static AnaliseOcorrencia Ocorrencia(
            string codigo, int severidade, double area, double percentual) =>
            new()
            {
                CodigoRegra = codigo,
                Descricao = "Sobreposição com área embargada",
                Severidade = severidade,
                Camada = "Termos de embargo",
                Origem = "IBAMA — Dados Abertos",
                Rotulo = "TAD 623603 — São Félix do Xingu/PA",
                AreaSobrepostaHa = area,
                PercentualDoImovel = percentual,
                Fundamento = "Área embargada por infração ambiental; aquisição vedada."
            };

        private static DadosDoLaudo Dados(
            Status resultado = Status.Bloqueado,
            bool coberturaCompleta = false,
            IReadOnlyList<FornecedorDoLaudo>? cadeia = null,
            byte[]? mapa = null,
            string? mapaIndisponivel = "o imóvel não tem perímetro registrado.",
            string? situacaoCar = "Aguardando análise") =>
            new()
            {
                Analise = new AnaliseAutomatica
                {
                    Id = 27,
                    TenantId = 1,
                    SolicitacaoId = 23,
                    CodigoCar = "PA-1507300-C0A47CCC2AD341DF99781DA138872D27",
                    Politica = "Protocolo padrão",
                    AreaImovelHa = 305.31,
                    Resultado = resultado,
                    Situacao = SituacaoAnalise.Concluida,
                    CoberturaCompleta = coberturaCompleta,
                    RegrasNaoAvaliadas = coberturaCompleta
                        ? null
                        : "IND-001 — Fornecedor indireto com restrição (nenhum fornecedor indireto declarado para o imóvel)",
                    CamadasVerificadas =
                        "Termos de embargo (IBAMA) — 57.953 feições, atualizada em 04/10/2026\n" +
                        "Unidades de Conservação (MMA/CNUC) — 3.511 feições, atualizada em 04/10/2026",
                    PoliticaAplicada =
                        """{"Nome":"Protocolo padrão","Regras":[{"Codigo":"EMB-001","Descricao":"Sobreposição com área embargada","Tipo":"EmbargoAmbiental","Severidade":"Bloqueio","AreaMinimaHa":0.0,"PercentualMinimo":0.0,"AnoMinimo":null,"Fundamento":"Área embargada por infração ambiental."}]}""",
                    IniciadaEm = new DateTime(2026, 10, 6, 2, 10, 0),
                    ConcluidaEm = new DateTime(2026, 10, 6, 2, 14, 0),
                    Ocorrencias = resultado == Status.Liberado
                        ? []
                        : [Ocorrencia("EMB-001", 2, 7.32, 2.40), Ocorrencia("ALE-001", 1, 1.94, 0.63)]
                },
                Solicitacao = new Solicitacao
                {
                    Id = 23,
                    Solicitante = "Equipe de originação",
                    DataSolicitacao = new DateTime(2026, 10, 6)
                },
                Propriedade = new Propriedade
                {
                    CodigoCar = "PA-1507300-C0A47CCC2AD341DF99781DA138872D27",
                    NomePropriedade = "Fazenda de teste",
                    Municipio = "São Félix do Xingu",
                    SituacaoCar = situacaoCar
                },
                Produtor = new Produtor { Nome = "Fulano de Tal", Cpf = "000.008.362-30" },
                Industria = new Industria
                {
                    TenantId = 1,
                    Nome = "Vale Verde Alimentos",
                    NomeResumido = "Vale Verde",
                    RazaoSocial = "Vale Verde Alimentos S.A.",
                    Cnpj = "11.222.333/0001-81"
                },
                CadeiaIndireta = cadeia ?? [],
                Mapa = mapa,
                MapaIndisponivel = mapaIndisponivel,
                CodigoDeConferencia = "A1B2-C3D4-E5F6"
            };

        private static byte[] Gerar(DadosDoLaudo dados) => new LaudoPdf(dados).GeneratePdf();

        [Fact]
        public void Laudo_DeveSairComoPdfValido()
        {
            var pdf = Gerar(Dados());

            pdf.Should().NotBeEmpty();

            // Assinatura do formato. Sem isto o teste passaria com qualquer
            // monte de bytes.
            System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        }

        [Theory]
        [InlineData(Status.Bloqueado)]
        [InlineData(Status.Alerta)]
        [InlineData(Status.Liberado)]
        public void Laudo_DeveSairEmQualquerVeredito(Status resultado)
        {
            // Cada veredito muda cor, resumo e seções; o liberado não tem
            // ocorrência alguma, que é o caminho onde o laudo mais fácil
            // quebraria por lista vazia.
            Gerar(Dados(resultado, coberturaCompleta: resultado == Status.Liberado))
                .Should().NotBeEmpty();
        }

        [Fact]
        public void Laudo_SemMapa_DeveSairDizendoQueNaoTemMapa()
        {
            // A base geoespacial pode estar fora do ar na hora da emissão. Sair
            // sem o mapa é muito melhor que não sair.
            Gerar(Dados(mapa: null, mapaIndisponivel: "o serviço de mapas respondeu 503."))
                .Should().NotBeEmpty();
        }

        [Fact]
        public void Laudo_ComCadeiaDeclarada_DeveSair()
        {
            var cadeia = new[]
            {
                new FornecedorDoLaudo("MT-5107925-AAA", "Fazenda Fornecedora", "11222333000181",
                    "GTA 2026", new DateTime(2026, 10, 1))
            };

            Gerar(Dados(cadeia: cadeia)).Should().NotBeEmpty();
        }

        [Fact]
        public void Laudo_SemLogoDaIndustria_DeveSair()
        {
            // Indústria recém-cadastrada ainda não subiu logo, e o laudo cai
            // para o nome em texto.
            var dados = Dados();
            dados.Industria!.Imagem = null;

            Gerar(dados).Should().NotBeEmpty();
        }

        [Fact]
        public void Laudo_ComRetratoDePoliticaIlegivel_NaoDeveImpedirAEmissao()
        {
            // O retrato é texto gravado meses antes. Ilegível, a seção do
            // protocolo some — o resto do laudo continua verdadeiro, e um
            // documento sem uma seção vale mais que documento nenhum.
            var dados = Dados();
            dados.Analise.PoliticaAplicada = "{isso não é json";

            dados.Protocolo.Should().BeEmpty();
            Gerar(dados).Should().NotBeEmpty();
        }

        [Fact]
        public void Numero_DeveCombinarAnoEIdentificador()
        {
            Dados().Numero.Should().Be("2026/000027");
        }

        [Fact]
        public void Protocolo_DeveSerLidoDoRetratoGravado()
        {
            // Do retrato, e não da política vigente: afrouxar uma regra depois
            // não pode reescrever um bloqueio já emitido.
            var protocolo = Dados().Protocolo;

            protocolo.Should().ContainSingle();
            protocolo[0].Codigo.Should().Be("EMB-001");
            protocolo[0].Severidade.Should().Be("Bloqueio");
        }
    }
}
