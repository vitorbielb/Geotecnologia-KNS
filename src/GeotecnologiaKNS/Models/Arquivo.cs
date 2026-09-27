using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    /// <summary>
    /// Documento anexado a um produtor, imóvel, análise ou cartografia.
    /// </summary>
    /// <remarks>
    /// O TenantId é repetido aqui, embora já esteja no registro-pai. A razão é
    /// segurança: os endpoints de download buscam o documento pelo Id inteiro, e
    /// sem o tenant na própria linha não havia como aplicar filtro global — era
    /// possível ler e apagar documento de outra indústria iterando o Id.
    ///
    /// A duplicação é segura porque documento não troca de dono: o vínculo é
    /// definido no envio e não muda.
    /// </remarks>
    public abstract class Arquivo : IPrimaryKeyInfo<int>, ITenantInfo
    {
        public int Id { get; set; }

        /// <summary>Indústria dona do documento. Preenchido no envio pelo <c>TenantFilter</c>.</summary>
        public int TenantId { get; set; }

        [DisplayName("Descrição")]
        public string Descricao { get; set; }
        public byte[] Dados { get; set; }
        public string ContentType { get; set; }
        [NotMapped] public abstract int VinculoId { get; set; }
    }

    public class ProdutorArquivo : Arquivo
    {
        [ForeignKey("Produtor")] public override int VinculoId { get; set; }
    }

    public class PropriedadeArquivo : Arquivo
    {
        [ForeignKey("Propriedade")] public override int VinculoId { get; set; }
    }
    public class AnaliseArquivo : Arquivo
    {
        [ForeignKey("Analise")] public override int VinculoId { get; set; }
        public DateTime? DataAnalise { get; set; } = DateTime.Now;
    }
    public class CartografiaArquivo : Arquivo
    {
        [ForeignKey("Cartografia")] public override int VinculoId { get; set; }
        public DateTime? DataCartografia { get; set; } = DateTime.Now;
        public string Tipo { get; set; }
    }
}
