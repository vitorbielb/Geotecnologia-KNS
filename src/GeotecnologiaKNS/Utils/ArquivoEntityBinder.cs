using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GeotecnologiaKNS.Utils
{
    internal class ArquivoEntityBinder<TViewModel, TModel> : IModelBinder
        where TModel : Arquivo
        where TViewModel : ArquivoViewModel<TModel>, new()
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var upload = await ArquivoUpload.LerAsync(bindingContext);

            if (upload is null)
            {
                return;
            }

            var model = new TViewModel
            {
                VinculoId = upload.VinculoId,
                Descricao = upload.Descricao,
                ContentType = upload.ContentType,
                Dados = upload.Dados
            };

            bindingContext.Result = ModelBindingResult.Success(model);
        }
    }

    internal class CartografiaArquivoEntityBinder : IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var upload = await ArquivoUpload.LerAsync(bindingContext);

            if (upload is null)
            {
                return;
            }

            var model = new CartografiaArquivoViewModel
            {
                Tipo = upload.Form["Tipo"].ToString(),
                VinculoId = upload.VinculoId,
                Descricao = upload.Descricao,
                ContentType = upload.ContentType,
                Dados = upload.Dados
            };

            bindingContext.Result = ModelBindingResult.Success(model);
        }
    }

    /// <summary>
    /// Leitura comum do formulário de upload: valida o vínculo e lê o arquivo
    /// enviado como multipart (campo <c>Dados</c>).
    /// </summary>
    internal sealed class ArquivoUpload
    {
        /// <summary>Tamanho máximo aceito por arquivo (os dados são gravados na própria tabela).</summary>
        public const int TamanhoMaximoEmBytes = 10 * 1024 * 1024;

        private ArquivoUpload(IFormCollection form, int vinculoId, string descricao, string contentType, byte[] dados)
        {
            Form = form;
            VinculoId = vinculoId;
            Descricao = descricao;
            ContentType = contentType;
            Dados = dados;
        }

        public IFormCollection Form { get; }
        public int VinculoId { get; }
        public string Descricao { get; }
        public string ContentType { get; }
        public byte[] Dados { get; }

        /// <summary>
        /// Retorna o upload lido, ou <c>null</c> quando o binding falhou
        /// (nesse caso o <paramref name="bindingContext"/> já contém o erro e o resultado).
        /// </summary>
        public static async Task<ArquivoUpload?> LerAsync(ModelBindingContext bindingContext)
        {
            var request = bindingContext.HttpContext.Request;

            if (!request.HasFormContentType)
            {
                return Falhar(bindingContext, "Dados", "Requisição inválida para envio de arquivo.");
            }

            var form = await request.ReadFormAsync();

            if (!int.TryParse(form["vinculoId"], out var vinculoId) || vinculoId <= 0)
            {
                return Falhar(bindingContext, "vinculoId", "Vínculo inválido.");
            }

            var arquivo = form.Files["Dados"] ?? form.Files.FirstOrDefault();

            if (arquivo is null || arquivo.Length == 0)
            {
                return Falhar(bindingContext, "Dados", "Arquivo é obrigatório.");
            }

            if (arquivo.Length > TamanhoMaximoEmBytes)
            {
                return Falhar(bindingContext, "Dados", $"O arquivo excede o limite de {TamanhoMaximoEmBytes / (1024 * 1024)} MB.");
            }

            using var memoryStream = new MemoryStream((int)arquivo.Length);
            await arquivo.CopyToAsync(memoryStream);

            var descricao = form["Descricao"].ToString();
            var contentType = form["ContentType"].ToString();

            return new ArquivoUpload(
                form,
                vinculoId,
                string.IsNullOrWhiteSpace(descricao) ? arquivo.FileName : descricao,
                string.IsNullOrWhiteSpace(contentType) ? arquivo.ContentType : contentType,
                memoryStream.ToArray());
        }

        private static ArquivoUpload? Falhar(ModelBindingContext bindingContext, string campo, string mensagem)
        {
            bindingContext.ModelState.AddModelError(campo, mensagem);
            bindingContext.Result = ModelBindingResult.Failed();
            return null;
        }
    }
}
