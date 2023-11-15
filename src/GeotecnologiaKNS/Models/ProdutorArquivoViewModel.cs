using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Models
{
    [ModelBinder(BinderType = typeof(ArquivoEntityBinder<ProdutorArquivoViewModel, ProdutorArquivo>))]
    public class ProdutorArquivoViewModel : ArquivoViewModel<ProdutorArquivo>
    {
        public override ProdutorArquivo Model => new()
        {
            VinculoId = VinculoId,
            ContentType = ContentType,
            Dados = Dados,
            Descricao = Descricao,
            Id = Id,
        };
    }
}
