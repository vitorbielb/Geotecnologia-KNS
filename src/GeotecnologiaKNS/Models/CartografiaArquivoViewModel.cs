using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Models
{
    [ModelBinder(BinderType = typeof(ArquivoEntityBinder<CartografiaArquivoViewModel, CartografiaArquivo>))]
    public class CartografiaArquivoViewModel : ArquivoViewModel<CartografiaArquivo>
    {
        public override CartografiaArquivo Model => new()
        {
            VinculoId = VinculoId,
            ContentType = ContentType,
            Dados = Dados,
            Descricao = Descricao,
            Id = Id,
        };
    }
}
