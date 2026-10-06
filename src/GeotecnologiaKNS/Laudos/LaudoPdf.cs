using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GeotecnologiaKNS.Laudos;

/// <summary>
/// O laudo de análise socioambiental, em PDF.
/// </summary>
/// <remarks>
/// O documento é o produto. A indústria o arquiva, anexa a contrato e apresenta
/// em auditoria — e, quando o assunto vira questionamento do Ministério
/// Público, é ele que responde. Por isso a ordem das seções não é estética:
///
/// O veredito vem primeiro, porque é o que se procura. Logo abaixo, o que
/// <b>não</b> foi verificado, antes de qualquer ocorrência — um laudo sem
/// achados se lê como aprovação, e é exatamente aí que a omissão engana.
/// Depois as evidências, e só então o protocolo aplicado e as bases
/// consultadas, que são o lastro de quem precisar conferir.
///
/// Nada aqui é recalculado: tudo vem do que ficou gravado na execução. Um laudo
/// que mudasse ao ser reimpresso não defenderia ninguém.
/// </remarks>
public class LaudoPdf : IDocument
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private const string Azul = "#127CC3";
    private const string AzulClaro = "#E8F4FE";
    private const string Carvao = "#1F2D37";
    private const string Grafite = "#44586A";
    private const string Neutro = "#6C8394";
    private const string Linha = "#DDE7EF";
    private const string Liberado = "#1F9D57";
    private const string Alerta = "#C98A00";
    private const string Bloqueado = "#C7442F";

    private readonly DadosDoLaudo _dados;

    public LaudoPdf(DadosDoLaudo dados) => _dados = dados;

    private string CorDoResultado => _dados.Analise.Resultado switch
    {
        Status.Liberado => Liberado,
        Status.Alerta => Alerta,
        Status.Bloqueado => Bloqueado,
        _ => Neutro
    };

    private static string CorDaSeveridade(int severidade) => severidade switch
    {
        2 => Bloqueado,
        1 => Alerta,
        _ => Neutro
    };

    private static string NomeDaSeveridade(int severidade) => severidade switch
    {
        2 => "BLOQUEIO",
        1 => "ALERTA",
        _ => "INFORMATIVO"
    };

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Laudo de análise socioambiental {_dados.Numero}",
        Author = _dados.Industria?.RazaoSocial ?? _dados.Industria?.Nome ?? "KNS Ambiental",
        Subject = $"Imóvel {_dados.Propriedade.CodigoCar}",
        Keywords = "CAR, análise socioambiental, cadeia de fornecimento"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(1.6f, Unit.Centimetre);
            // Sem declarar família: a fonte embarcada na própria biblioteca
            // renderiza igual no Windows e no Linux. Pedir Calibri ou Arial
            // quebra a emissão num servidor que não as tenha instaladas — e foi
            // o que aconteceu no primeiro teste.
            pagina.DefaultTextStyle(x => x.FontSize(9).FontColor(Carvao));

            pagina.Header().Element(Cabecalho);
            pagina.Content().Element(Conteudo);
            pagina.Footer().Element(Rodape);
        });
    }

    private void Cabecalho(IContainer container)
    {
        container.PaddingBottom(10).Column(coluna =>
        {
            coluna.Item().Row(linha =>
            {
                if (_dados.Industria?.Imagem is { Length: > 0 } logo)
                {
                    // A logo da indústria, não a do sistema: o laudo é dela, e
                    // é ela que o assina perante quem o recebe.
                    linha.ConstantItem(120).MaxHeight(38).AlignLeft().AlignMiddle()
                         .Image(logo).FitArea();
                }
                else
                {
                    linha.ConstantItem(120).AlignLeft().AlignMiddle()
                         .Text(_dados.Industria?.NomeResumido ?? string.Empty)
                         .FontSize(13).Bold().FontColor(Azul);
                }

                linha.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().Text("LAUDO DE ANÁLISE SOCIOAMBIENTAL")
                            .FontSize(12).Bold().FontColor(Azul).LetterSpacing(0.03f);

                    c.Item().Text($"Nº {_dados.Numero}").FontSize(10).FontColor(Grafite);

                    c.Item().Text(t =>
                    {
                        t.Span("Emitido em ").FontSize(7.5f).FontColor(Neutro);
                        t.Span(DateTime.Now.ToString("dd/MM/yyyy 'às' HH:mm", PtBr))
                         .FontSize(7.5f).FontColor(Neutro);
                    });
                });
            });

            coluna.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Azul);
        });
    }

    private void Rodape(IContainer container)
    {
        container.PaddingTop(8).Column(coluna =>
        {
            coluna.Item().LineHorizontal(0.5f).LineColor(Linha);

            coluna.Item().PaddingTop(5).Row(linha =>
            {
                linha.RelativeItem().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7).FontColor(Neutro));
                    t.Span(_dados.Industria?.RazaoSocial ?? _dados.Industria?.Nome ?? string.Empty);
                    t.Span("   ·   Conferência: ");
                    t.Span(_dados.CodigoDeConferencia).SemiBold();
                });

                linha.ConstantItem(90).AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7).FontColor(Neutro));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        });
    }

    private void Conteudo(IContainer container)
    {
        container.Column(coluna =>
        {
            coluna.Spacing(14);

            coluna.Item().Element(Veredito);
            coluna.Item().Element(Identificacao);

            // Antes das ocorrências, de propósito: um laudo sem achados se lê
            // como aprovação, e é aí que a omissão engana.
            if (!_dados.Analise.CoberturaCompleta)
            {
                coluna.Item().Element(Ressalva);
            }

            if (_dados.Mapa is { Length: > 0 } || _dados.MapaIndisponivel is not null)
            {
                coluna.Item().Element(Mapa);
            }

            coluna.Item().Element(Ocorrencias);
            coluna.Item().Element(Cadeia);
            coluna.Item().Element(Protocolo);
            coluna.Item().Element(Bases);
            coluna.Item().Element(Encerramento);
        });
    }

    private void Veredito(IContainer container)
    {
        container.Background(AzulClaro).Padding(12).Row(linha =>
        {
            linha.ConstantItem(150).Column(c =>
            {
                c.Item().Text("RESULTADO").FontSize(7.5f).FontColor(Neutro).LetterSpacing(0.08f);

                c.Item().PaddingTop(2).Background(CorDoResultado)
                 .PaddingVertical(5).PaddingHorizontal(10).AlignCenter()
                 .Text(_dados.Analise.Resultado.ToString().ToUpperInvariant())
                 .FontSize(14).Bold().FontColor("#FFFFFF");
            });

            linha.RelativeItem().PaddingLeft(14).Column(c =>
            {
                c.Item().Text(ResumoDoVeredito()).FontSize(9).FontColor(Grafite).LineHeight(1.35f);

                c.Item().PaddingTop(6).Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(8).FontColor(Neutro));
                    t.Span("Protocolo aplicado: ").SemiBold();
                    t.Span(_dados.Analise.Politica);
                    t.Span("   ·   ");
                    t.Span($"{_dados.Protocolo.Count - _dados.NaoAvaliadas.Count} de " +
                           $"{_dados.Protocolo.Count} regra(s) aplicada(s)");
                });
            });
        });
    }

    private string ResumoDoVeredito()
    {
        var achados = _dados.Analise.Ocorrencias.Count;

        return _dados.Analise.Resultado switch
        {
            Status.Bloqueado =>
                $"Foram encontradas {achados} ocorrência(s), das quais ao menos uma veda a " +
                "aquisição segundo o protocolo vigente nesta indústria.",

            Status.Alerta when achados > 0 =>
                $"Foram encontradas {achados} ocorrência(s) que exigem verificação antes da " +
                "aquisição, sem que nenhuma, isoladamente, a vede.",

            Status.Alerta =>
                "Nenhuma ocorrência foi encontrada nas bases consultadas, mas parte das regras " +
                "não pôde ser verificada — o resultado não é uma liberação.",

            Status.Liberado =>
                "Nenhuma ocorrência restritiva foi encontrada nas bases consultadas, e todas as " +
                "regras com poder de decisão puderam ser aplicadas.",

            _ => "Análise em processamento."
        };
    }

    private void Identificacao(IContainer container)
    {
        container.Column(coluna =>
        {
            coluna.Item().Element(c => Titulo(c, "1. Identificação"));

            coluna.Item().Table(tabela =>
            {
                // Rótulo estreito e valor largo: o código do CAR tem quarenta
                // caracteres e quebrava em duas linhas, o que atrapalha quem
                // precisa conferi-lo contra outro documento.
                tabela.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(88);
                    c.RelativeColumn(1.35f);
                    c.ConstantColumn(80);
                    c.RelativeColumn();
                });

                var p = _dados.Propriedade;

                Par(tabela, "Imóvel (CAR)", p.CodigoCar, "Município", $"{p.Municipio} / {p.UnidadeFederativa}");
                Par(tabela, "Denominação", Ou(p.NomePropriedade, "não informada"),
                            "Área do imóvel", $"{_dados.Analise.AreaImovelHa.ToString("N2", PtBr)} ha");

                Par(tabela, "Produtor", Ou(_dados.Produtor?.Nome, "não informado"),
                            "CPF/CNPJ", Ou(_dados.Produtor?.Cpf, "não informado"));

                // A situação do cadastro entra aqui porque muda o peso de tudo
                // o mais: análise sobre cadastro cancelado descreve um registro
                // que foi anulado.
                Par(tabela, "Situação do CAR", Ou(p.SituacaoCar, "não informada"),
                            "Solicitante", Ou(_dados.Solicitacao.Solicitante, "não informado"));

                Par(tabela, "Solicitado em",
                            _dados.Solicitacao.DataSolicitacao?.ToString("dd/MM/yyyy", PtBr) ?? "—",
                            "Analisado em",
                            _dados.Analise.ConcluidaEm?.ToString("dd/MM/yyyy 'às' HH:mm", PtBr) ?? "—");
            });

            if (!string.IsNullOrWhiteSpace(_dados.Propriedade.SituacaoCar) &&
                _dados.Propriedade.SituacaoCar.Contains("Cancelado", StringComparison.OrdinalIgnoreCase))
            {
                coluna.Item().PaddingTop(6).Background("#FBEBE8").Padding(8).Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Bloqueado));
                    t.Span("Atenção: ").Bold();
                    t.Span("o cadastro ambiental deste imóvel consta como cancelado na base do " +
                           "SICAR. A análise abaixo descreve o perímetro registrado, mas o " +
                           "cadastro em si foi anulado pelo órgão competente.");
                });
            }
        });
    }

    private void Ressalva(IContainer container)
    {
        container.Background("#FCF4E0").Padding(10).Column(coluna =>
        {
            coluna.Item().Text("VERIFICAÇÃO INCOMPLETA")
                  .FontSize(8).Bold().FontColor(Alerta).LetterSpacing(0.06f);

            coluna.Item().PaddingTop(4).Text(
                "As regras abaixo não puderam ser aplicadas. O que não aparece neste laudo não " +
                "foi verificado — não é ausência de restrição.")
                .FontSize(8.5f).FontColor(Grafite).LineHeight(1.3f);

            foreach (var regra in _dados.NaoAvaliadas)
            {
                coluna.Item().PaddingTop(3).Text($"•  {regra}").FontSize(8.5f).FontColor(Grafite);
            }
        });
    }

    private void Mapa(IContainer container)
    {
        // Inteiro numa página só: mapa separado da própria legenda deixa o
        // leitor sem saber o que cada cor significa.
        container.ShowEntire().Column(coluna =>
        {
            coluna.Item().Element(c => Titulo(c, "2. Localização e sobreposições"));

            if (_dados.Mapa is { Length: > 0 } imagem)
            {
                // Largura cheia, altura vinda da proporção pedida na origem.
                // Sem o teto, uma imagem de outra proporção ocuparia meia
                // página e empurraria as ocorrências — que são o conteúdo.
                coluna.Item().Border(1).BorderColor(Linha)
                      .MaxHeight(215).Image(imagem).FitWidth();

                coluna.Item().PaddingTop(6).Row(linha =>
                {
                    Legenda(linha, 2, "#0E91EF", "Perímetro do imóvel");
                    Legenda(linha, 3, Bloqueado, "Embargo, terra indígena ou unidade de conservação");
                    Legenda(linha, 2, "#E06C2A", "Desmatamento");
                    Legenda(linha, 2, Alerta, "Demais sobreposições");
                });
            }
            else
            {
                coluna.Item().Background("#F4FAFF").Padding(10)
                      .Text($"Mapa não disponível: {_dados.MapaIndisponivel}")
                      .FontSize(8.5f).FontColor(Neutro);
            }
        });
    }

    private static void Legenda(RowDescriptor linha, float peso, string cor, string texto)
    {
        // A largura acompanha o tamanho do rótulo: com todas iguais, a legenda
        // mais longa era cortada e o resto dela aparecia sozinho na página
        // seguinte.
        linha.RelativeItem(peso).PaddingRight(6).Row(r =>
        {
            r.ConstantItem(8).PaddingTop(1).Height(8).Background(cor);
            r.RelativeItem().PaddingLeft(4).Text(texto).FontSize(6.2f).FontColor(Neutro);
        });
    }

    private void Ocorrencias(IContainer container)
    {
        container.Column(coluna =>
        {
            coluna.Item().Element(c => Titulo(c, "3. Ocorrências"));

            if (_dados.Analise.Ocorrencias.Count == 0)
            {
                coluna.Item().Background("#E6F6ED").Padding(10).Text(
                    "Nenhuma sobreposição restritiva foi encontrada nas bases consultadas.")
                    .FontSize(8.5f).FontColor(Grafite);

                return;
            }

            // Agrupadas por regra: um imóvel pode tocar dezenas de polígonos da
            // mesma camada, e listar um a um torna o laudo ilegível sem
            // acrescentar informação.
            var grupos = _dados.Analise.Ocorrencias
                .GroupBy(o => new { o.CodigoRegra, o.Camada })
                .OrderByDescending(g => g.Max(o => o.Severidade))
                .ThenByDescending(g => g.Sum(o => o.AreaSobrepostaHa));

            foreach (var grupo in grupos)
            {
                var primeira = grupo.First();
                var severidade = grupo.Max(o => o.Severidade);
                var area = grupo.Sum(o => o.AreaSobrepostaHa);
                var percentual = grupo.Sum(o => o.PercentualDoImovel);
                var porArea = area > 0;

                coluna.Item().PaddingTop(6).Border(1).BorderColor(Linha).Column(c =>
                {
                    c.Item().Background(CorDaSeveridade(severidade)).PaddingVertical(4)
                     .PaddingHorizontal(8).Row(r =>
                     {
                         r.AutoItem().Text(NomeDaSeveridade(severidade))
                          .FontSize(7.5f).Bold().FontColor("#FFFFFF").LetterSpacing(0.06f);

                         r.RelativeItem().PaddingLeft(10).Text($"{primeira.CodigoRegra} — {primeira.Descricao}")
                          .FontSize(9).Bold().FontColor("#FFFFFF");
                     });

                    c.Item().Padding(8).Column(d =>
                    {
                        d.Item().Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Grafite));
                            t.Span(porArea ? "Camada: " : "Fonte: ").SemiBold();
                            t.Span($"{primeira.Camada} ({primeira.Origem})");
                        });

                        d.Item().PaddingTop(2).Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Grafite));

                            if (porArea)
                            {
                                t.Span("Sobreposição: ").SemiBold();
                                t.Span($"{area.ToString("N2", PtBr)} ha " +
                                       $"({percentual.ToString("N2", PtBr)}% do imóvel) " +
                                       $"em {grupo.Count()} polígono(s)");
                            }
                            else
                            {
                                t.Span("Registros: ").SemiBold();
                                t.Span($"{grupo.Count()}. Independe da localização do imóvel.");
                            }
                        });

                        var rotulos = grupo.Where(o => !string.IsNullOrWhiteSpace(o.Rotulo))
                                           .Select(o => o.Rotulo!)
                                           .Distinct()
                                           .Take(6)
                                           .ToList();

                        if (rotulos.Count > 0)
                        {
                            d.Item().PaddingTop(2).Text(t =>
                            {
                                t.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Grafite));
                                t.Span(porArea ? "Feições: " : "Registros: ").SemiBold();
                                t.Span(string.Join("; ", rotulos) +
                                       (grupo.Count() > rotulos.Count ? "; ..." : string.Empty));
                            });
                        }

                        if (!string.IsNullOrWhiteSpace(primeira.Fundamento))
                        {
                            d.Item().PaddingTop(4).BorderTop(0.5f).BorderColor(Linha)
                             .PaddingTop(4).Text(t =>
                             {
                                 t.DefaultTextStyle(x => x.FontSize(8).FontColor(Neutro).Italic());
                                 t.Span("Fundamento: ");
                                 t.Span(primeira.Fundamento);
                             });
                        }
                    });
                });
            }
        });
    }

    private void Cadeia(IContainer container)
    {
        container.Column(coluna =>
        {
            coluna.Item().Element(c => Titulo(c, "4. Cadeia de fornecimento indireto"));

            if (_dados.CadeiaIndireta.Count == 0)
            {
                coluna.Item().Background("#FCF4E0").Padding(10).Text(
                    "Nenhum fornecedor indireto foi declarado para este imóvel. A cadeia " +
                    "indireta não foi verificada — o que não significa que não exista.")
                    .FontSize(8.5f).FontColor(Grafite).LineHeight(1.3f);

                return;
            }

            coluna.Item().Text(
                "A verificação alcança apenas os fornecedores informados. Fornecedores não " +
                "declarados não foram verificados e não estão refletidos neste resultado.")
                .FontSize(8).FontColor(Neutro).LineHeight(1.3f);

            coluna.Item().PaddingTop(5).Table(tabela =>
            {
                tabela.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                });

                Cabeca(tabela, "CAR do fornecedor", "Produtor", "CPF/CNPJ", "Origem da informação");

                foreach (var f in _dados.CadeiaIndireta)
                {
                    Celula(tabela, f.CodigoCar);
                    Celula(tabela, Ou(f.NomeProdutor, "—"));
                    Celula(tabela, Ou(f.Documento, "não informado"));
                    Celula(tabela, Ou(f.Origem, "—"));
                }
            });
        });
    }

    private void Protocolo(IContainer container)
    {
        if (_dados.Protocolo.Count == 0)
        {
            return;
        }

        container.Column(coluna =>
        {
            coluna.Item().Element(c => Titulo(c, "5. Protocolo aplicado"));

            coluna.Item().Text(
                "Retrato das regras vigentes no momento da análise. Alterações posteriores na " +
                "política desta indústria não modificam este laudo.")
                .FontSize(8).FontColor(Neutro).LineHeight(1.3f);

            coluna.Item().PaddingTop(5).Table(tabela =>
            {
                tabela.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(48);
                    c.RelativeColumn(4);
                    c.ConstantColumn(62);
                    c.ConstantColumn(52);
                    c.ConstantColumn(44);
                });

                Cabeca(tabela, "Regra", "Descrição", "Consequência", "Área mín.", "% mín.");

                foreach (var regra in _dados.Protocolo)
                {
                    Celula(tabela, regra.Codigo);
                    Celula(tabela, regra.Descricao);
                    Celula(tabela, regra.Severidade);
                    Celula(tabela, regra.AreaMinimaHa > 0 ? $"{regra.AreaMinimaHa.ToString("N0", PtBr)} ha" : "—");
                    Celula(tabela, regra.PercentualMinimo > 0 ? $"{regra.PercentualMinimo.ToString("N0", PtBr)}%" : "—");
                }
            });
        });
    }

    private void Bases(IContainer container)
    {
        container.Column(coluna =>
        {
            coluna.Item().Element(c => Titulo(c, "6. Bases consultadas"));

            coluna.Item().Text(
                "Origem e data de carga de cada base usada no cruzamento. Ausência de " +
                "sobreposição só significa \"nada encontrado\" nas bases listadas abaixo.")
                .FontSize(8).FontColor(Neutro).LineHeight(1.3f);

            if (_dados.Camadas.Count == 0)
            {
                coluna.Item().PaddingTop(4).Text("Nenhuma base registrada nesta análise.")
                      .FontSize(8.5f).FontColor(Neutro);

                return;
            }

            foreach (var camada in _dados.Camadas)
            {
                coluna.Item().PaddingTop(3).Text($"•  {camada.Descricao}")
                      .FontSize(8).FontColor(Grafite).LineHeight(1.25f);
            }
        });
    }

    private void Encerramento(IContainer container)
    {
        container.PaddingTop(4).BorderTop(1).BorderColor(Linha).PaddingTop(8).Column(coluna =>
        {
            coluna.Item().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Neutro).LineHeight(1.35f));

                t.Span("Este laudo foi produzido por análise automatizada, a partir das bases " +
                       "públicas e das regras listadas acima, e descreve o que foi verificado " +
                       "na data de sua execução. ");

                t.Span("Não substitui vistoria em campo nem parecer jurídico, e não constitui " +
                       "assinatura digital para fins da ICP-Brasil.").SemiBold();
            });

            coluna.Item().PaddingTop(6).Background("#F4FAFF").Padding(8).Row(linha =>
            {
                linha.RelativeItem().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Grafite));
                    t.Span("Código de conferência\n").FontColor(Neutro);
                    t.Span(_dados.CodigoDeConferencia).FontSize(11).Bold().LetterSpacing(0.12f);
                });

                linha.RelativeItem(2).PaddingLeft(10).Text(
                    "O código acima é derivado do conteúdo desta análise. O mesmo laudo emitido " +
                    "novamente produz o mesmo código; qualquer alteração no veredito, nas datas " +
                    "ou nas ocorrências produz outro.")
                    .FontSize(7).FontColor(Neutro).LineHeight(1.3f);
            });
        });
    }

    private static void Titulo(IContainer container, string texto)
    {
        container.PaddingBottom(5).Column(coluna =>
        {
            coluna.Item().Text(texto).FontSize(10).Bold().FontColor(Azul);
            coluna.Item().PaddingTop(2).LineHorizontal(0.8f).LineColor(Linha);
        });
    }

    private static void Par(TableDescriptor tabela, string r1, string v1, string r2, string v2)
    {
        Rotulo(tabela, r1);
        Valor(tabela, v1);
        Rotulo(tabela, r2);
        Valor(tabela, v2);
    }

    private static void Rotulo(TableDescriptor tabela, string texto) =>
        tabela.Cell().PaddingVertical(2.5f).Text(texto).FontSize(8).FontColor(Neutro);

    private static void Valor(TableDescriptor tabela, string texto) =>
        tabela.Cell().PaddingVertical(2.5f).PaddingRight(8).Text(texto).FontSize(8.5f).SemiBold();

    private static void Cabeca(TableDescriptor tabela, params string[] colunas)
    {
        foreach (var coluna in colunas)
        {
            tabela.Cell().Background(AzulClaro).Padding(4)
                  .Text(coluna).FontSize(7.5f).Bold().FontColor(Azul);
        }
    }

    private static void Celula(TableDescriptor tabela, string texto) =>
        tabela.Cell().BorderBottom(0.5f).BorderColor(Linha).Padding(4)
              .Text(texto).FontSize(8).FontColor(Grafite);

    private static string Ou(string? valor, string padrao) =>
        string.IsNullOrWhiteSpace(valor) ? padrao : valor;
}
