using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Models
{
    [ModelBinder(BinderType = typeof(CartografiaArquivoEntityBinder))]
    public class CartografiaArquivoViewModel : ArquivoViewModel<CartografiaArquivo>
    {
        public string Tipo { get; set; }
        public override CartografiaArquivo Model => new()
        {
            VinculoId = VinculoId,
            ContentType = ContentType,
            Dados = Dados,
            Descricao = Descricao,
            Id = Id,
            Tipo = Tipo
        };
    }
}
