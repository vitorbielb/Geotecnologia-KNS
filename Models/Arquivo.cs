using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public class Arquivo
    {
        public int Id { get; set; }
        [Display(Name = "Domentos da propriedade")]
        public string Descricao { get; set; }
        public byte[] Dados { get; set; }
        public string ContentType { get; set; }

        [NotMapped] 
        public virtual int VinculoId { get; set; }
    }

    public class ProdutorArquivo : Arquivo
    {
        public int ProdutorId { get; set; }
        public override int VinculoId => ProdutorId;
    }

    public class PropriedadeArquivo : Arquivo
    {
        public int PropriedadeId { get; set; }
        public override int VinculoId => PropriedadeId;
    }
}
