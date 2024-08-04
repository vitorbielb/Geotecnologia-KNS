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

            var form = await bindingContext.HttpContext.Request.ReadFormAsync();

            if (!int.TryParse(form["vinculoId"], out var vinculoId))
            {
                bindingContext.ModelState.AddModelError("vinculoId", "Vínculo inválido.");
                bindingContext.Result = ModelBindingResult.Failed();
                return;
            }

            var model = new TViewModel
            {
                VinculoId = vinculoId,
                Descricao = form["Descricao"].ToString(),
                ContentType = form["ContentType"].ToString(),
                Dados = ByteArrayExt.ToByteArrayOrEmpty(form["Dados"].FirstOrDefault())
            };

            bindingContext.Result = ModelBindingResult.Success(model);
        }
    }

    internal class CartografiaArquivoEntityBinder : IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var form = await bindingContext.HttpContext.Request.ReadFormAsync();

            if (!int.TryParse(form["vinculoId"], out var vinculoId))
            {
                bindingContext.ModelState.AddModelError("vinculoId", "Vínculo inválido.");
                bindingContext.Result = ModelBindingResult.Failed();
                return;
            }

            var model = new CartografiaArquivoViewModel
            {
                Tipo = form["Tipo"].ToString(),
                VinculoId = vinculoId,
                Descricao = form["Descricao"].ToString(),
                ContentType = form["ContentType"].ToString(),
                Dados = ByteArrayExt.ToByteArrayOrEmpty(form["Dados"].FirstOrDefault())
            };

            bindingContext.Result = ModelBindingResult.Success(model);
        }
    }

    internal static class ByteArrayExt
    {
        public static byte[] ToByteArrayOrEmpty(string? byteString)
        {
            if (string.IsNullOrWhiteSpace(byteString))
                return Array.Empty<byte>();

            var byteValues = byteString.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var byteArray = new byte[byteValues.Length];

            for (var i = 0; i < byteValues.Length; i++)
            {
                if (!byte.TryParse(byteValues[i].Trim(), out var parsedByte))
                    return Array.Empty<byte>();

                byteArray[i] = parsedByte;
            }

            return byteArray;
        }
    }
}