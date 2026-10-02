using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GeotecnologiaKNS.Geo.Ingestao;

/// <summary>
/// O mesmo mecanismo de troca versionada, para as listas por CPF/CNPJ.
/// </summary>
/// <remarks>
/// A lista por documento merece a mesma proteção que a camada geográfica, e por
/// uma razão concreta: ela é o único caminho para os embargos sem área
/// delimitada, que são quase metade do total. Destruí-la por uma carga ruim
/// apaga justamente a verificação que nenhum polígono faria — e a regra
/// continuaria se declarando avaliada.
/// </remarks>
public class TrocaDeListaRestritiva
{
    private readonly GeoDbContext _context;
    private readonly ILogger _logger;

    public TrocaDeListaRestritiva(GeoDbContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Carrega (ou cria) o registro da lista e limpa restos de tentativa anterior.
    /// </summary>
    public async Task<ListaRestritiva> PrepararAsync(
        TipoRestricao tipo,
        string nome,
        string origem,
        int? periodicidadeDias,
        CancellationToken cancellationToken)
    {
        var lista = await _context.ListasRestritivas
            .FirstOrDefaultAsync(x => x.Tipo == tipo, cancellationToken);

        if (lista is null)
        {
            lista = new ListaRestritiva { Tipo = tipo };
            _context.ListasRestritivas.Add(lista);
        }

        lista.Nome = nome;
        lista.Origem = origem;
        lista.PeriodicidadeDias = periodicidadeDias;

        await _context.SaveChangesAsync(cancellationToken);
        await DescartarPendenteAsync(lista, cancellationToken);

        return lista;
    }

    /// <summary>Joga fora a versão gravada que não vai ser publicada.</summary>
    public Task DescartarPendenteAsync(ListaRestritiva lista, CancellationToken cancellationToken) =>
        _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM geo.restricao_documento WHERE tipo = {0} AND versao <> {1}",
            new object[] { (int)lista.Tipo, lista.VersaoAtual },
            cancellationToken);

    public static int ProximaVersao(ListaRestritiva lista) => lista.VersaoAtual + 1;

    public async Task<ResultadoDaTroca> PublicarAsync(
        ListaRestritiva lista, int gravados, CancellationToken cancellationToken)
    {
        var antes = lista.TotalRegistros;
        var novaVersao = ProximaVersao(lista);

        var motivo = GuardaDeCarga.Avaliar(antes, gravados);

        if (motivo != MotivoDaRecusa.Nenhum)
        {
            await _context.Database.ExecuteSqlRawAsync(
                "DELETE FROM geo.restricao_documento WHERE tipo = {0} AND versao = {1}",
                new object[] { (int)lista.Tipo, novaVersao },
                cancellationToken);

            var recusa = new ResultadoDaTroca(false, motivo, antes, gravados, "registros");

            _logger.LogError(
                "Recarga da lista {Tipo} recusada: {Explicacao}", lista.Tipo, recusa.Explicacao);

            return recusa;
        }

        lista.VersaoAtual = novaVersao;
        lista.TotalRegistros = gravados;
        lista.AtualizadaEm = DateTime.UtcNow;

        _context.ListasRestritivas.Update(lista);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM geo.restricao_documento WHERE tipo = {0} AND versao <> {1}",
            new object[] { (int)lista.Tipo, novaVersao },
            cancellationToken);

        _logger.LogInformation(
            "Lista {Tipo} publicada na versão {Versao}: {Depois:N0} registros ({Antes:N0} antes).",
            lista.Tipo, novaVersao, gravados, antes);

        return new ResultadoDaTroca(true, MotivoDaRecusa.Nenhum, antes, gravados, "registros");
    }
}
