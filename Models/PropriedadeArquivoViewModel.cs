using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Models
{
    [ModelBinder(BinderType = typeof(ArquivoEntityBinder<PropriedadeArquivoViewModel, PropriedadeArquivo>))]
    public class PropriedadeArquivoViewModel : ArquivoViewModel<PropriedadeArquivo>
    {
        public override PropriedadeArquivo Model => new()
        {
            VinculoId = VinculoId,
            ContentType = ContentType,
            Dados = Dados,
            Descricao = Descricao,
            Id = Id,
        };
    }
}
