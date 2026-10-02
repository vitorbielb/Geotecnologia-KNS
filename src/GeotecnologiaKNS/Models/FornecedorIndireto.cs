using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    /// <summary>
    /// Imóvel que forneceu animais ao fornecedor direto.
    /// </summary>
    /// <remarks>
    /// É o elo que falta na maior parte do monitoramento de cadeia de carne. A
    /// fazenda que vende o boi gordo ao frigorífico costuma ter comprado o
    /// bezerro de outra, e é na outra que o passivo mora: foi lá que o
    /// desmatamento aconteceu, é lá que o embargo está. Olhar só o fornecedor
    /// direto dá um laudo limpo sobre uma cadeia que não é.
    ///
    /// O sistema não tem como descobrir esses imóveis sozinho — quem os conhece
    /// é o próprio fornecedor, e a prova documental é a GTA, emitida pelo órgão
    /// estadual. Então eles são <b>declarados</b>, e o laudo diz isso com todas
    /// as letras: verificado o que foi informado, e nada além. Dizer mais seria
    /// inventar cobertura que não existe.
    /// </remarks>
    public class FornecedorIndireto : IIndustriaInfo, IPrimaryKeyInfo<int>
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }

        public Industria? Industria { get; set; }

        /// <summary>Fornecedor direto a quem este imóvel vendeu.</summary>
        [ForeignKey(nameof(Propriedade))]
        public int PropriedadeId { get; set; }

        public Propriedade? Propriedade { get; set; }

        [Required(ErrorMessage = "Informe o número do CAR do fornecedor indireto")]
        [Display(Name = "Número do CAR")]
        [StringLength(100)]
        public string CodigoCar { get; set; } = string.Empty;

        [Display(Name = "Produtor")]
        [StringLength(200)]
        public string? NomeProdutor { get; set; }

        /// <summary>
        /// CPF ou CNPJ do fornecedor indireto, quando informado.
        /// </summary>
        /// <remarks>
        /// Sem ele a verificação fica só geográfica, e metade dos embargos do
        /// IBAMA não tem área delimitada — são justamente os que só o documento
        /// encontra. O laudo distingue os dois casos.
        /// </remarks>
        [Display(Name = "CPF/CNPJ")]
        [StringLength(20)]
        public string? Documento { get; set; }

        /// <summary>De onde veio a informação: GTA, declaração do fornecedor, auditoria.</summary>
        [Display(Name = "Origem da informação")]
        [StringLength(200)]
        public string? Origem { get; set; }

        [Display(Name = "Declarado em")]
        public DateTime DeclaradoEm { get; set; } = DateTime.Now;
    }
}
