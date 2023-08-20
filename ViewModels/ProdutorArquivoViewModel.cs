using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.ViewModels
{
    [ModelBinder(BinderType = typeof(ArquivoEntityBinder<ProdutorArquivoViewModel>))]
    public class ProdutorArquivoViewModel : ArquivoViewModel
    {
        public static implicit operator ProdutorArquivo(ProdutorArquivoViewModel arquivo) => new()
        {
            ContentType = arquivo.ContentType,
            Dados = arquivo.Dados,
            Descricao = arquivo.Descricao,
            Id = arquivo.Id
        };
    }
}
