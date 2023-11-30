using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Models
{
    [ModelBinder(BinderType = typeof(ArquivoEntityBinder<AnaliseArquivoViewModel, AnaliseArquivo>))]
    public class AnaliseArquivoViewModel : ArquivoViewModel<AnaliseArquivo>
    {
        public override AnaliseArquivo Model => new()
        {
            VinculoId = VinculoId,
            ContentType = ContentType,
            Dados = Dados,
            Descricao = Descricao,
            Id = Id,
        };
    }
}
