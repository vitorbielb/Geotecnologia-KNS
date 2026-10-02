using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GeotecnologiaKNS.Geo.Ingestao;

/// <summary>
/// Publica uma nova versão de camada só quando ela resiste a conferência.
/// </summary>
/// <remarks>
/// A carga grava a versão seguinte ao lado da que está no ar; a análise continua
/// enxergando a antiga até a troca. Isso resolve dois problemas de uma vez.
///
/// O primeiro é a quebra no meio: antes, a carga apagava tudo e inseria, então
/// uma falha deixava a camada pela metade — e parcial parece carregada. A
/// análise diria "nenhuma sobreposição" sobre dados que nunca chegaram.
///
/// O segundo é o arquivo ruim na origem, tratado em <see cref="GuardaDeCarga"/>.
/// </remarks>
public class TrocaDeCamada
{
    private readonly GeoDbContext _context;
    private readonly ILogger _logger;

    public TrocaDeCamada(GeoDbContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Número da versão em que a nova carga deve gravar.</summary>
    public static int ProximaVersao(CamadaReferencia camada) => camada.VersaoAtual + 1;

    /// <summary>Joga fora a versão gravada que não vai ser publicada.</summary>
    public Task DescartarPendenteAsync(CamadaReferencia camada, CancellationToken cancellationToken) =>
        LimparTentativaAnteriorAsync(camada, cancellationToken);

    /// <summary>
    /// Remove restos de uma carga anterior que não chegou a ser publicada.
    /// </summary>
    /// <remarks>
    /// Uma tentativa interrompida deixa feições da versão seguinte gravadas sem
    /// nunca terem entrado no ar. Limpar antes evita que a contagem da próxima
    /// tentativa venha somada com a anterior.
    /// </remarks>
    public async Task LimparTentativaAnteriorAsync(
        CamadaReferencia camada, CancellationToken cancellationToken)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM geo.feicao_referencia WHERE camada_id = {0} AND versao <> {1}",
            new object[] { camada.Id, camada.VersaoAtual },
            cancellationToken);
    }

    /// <summary>
    /// Confere a nova versão e, se ela resistir, publica e descarta a anterior.
    /// </summary>
    public async Task<ResultadoDaTroca> PublicarAsync(
        CamadaReferencia camada, int gravados, CancellationToken cancellationToken)
    {
        var antes = camada.TotalFeicoes;
        var novaVersao = ProximaVersao(camada);

        var motivo = GuardaDeCarga.Avaliar(antes, gravados);

        if (motivo != MotivoDaRecusa.Nenhum)
        {
            await _context.Database.ExecuteSqlRawAsync(
                "DELETE FROM geo.feicao_referencia WHERE camada_id = {0} AND versao = {1}",
                new object[] { camada.Id, novaVersao },
                cancellationToken);

            var recusa = new ResultadoDaTroca(false, motivo, antes, gravados);

            _logger.LogError(
                "Recarga da camada {Chave} recusada: {Explicacao}",
                camada.Chave, recusa.Explicacao);

            return recusa;
        }

        // A troca em si: a partir daqui a análise passa a enxergar a nova
        // versão, e só então a anterior é descartada.
        camada.VersaoAtual = novaVersao;
        camada.TotalFeicoes = gravados;
        camada.AtualizadaEm = DateTime.UtcNow;

        _context.Camadas.Update(camada);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM geo.feicao_referencia WHERE camada_id = {0} AND versao <> {1}",
            new object[] { camada.Id, novaVersao },
            cancellationToken);

        _logger.LogInformation(
            "Camada {Chave} publicada na versão {Versao}: {Depois:N0} feições ({Antes:N0} antes).",
            camada.Chave, novaVersao, gravados, antes);

        return new ResultadoDaTroca(true, MotivoDaRecusa.Nenhum, antes, gravados);
    }
}
