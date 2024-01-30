using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GeotecnologiaKNS.Utils
{
    internal class ArquivoEntityBinder<TViewModel, TModel> : IModelBinder
        where TModel : Arquivo
        where TViewModel : ArquivoViewModel<TModel>, new()
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var form = await bindingContext.HttpContext.Request.ReadFormAsync();
            bindingContext.Result = ModelBindingResult.Success(new TViewModel
            {
                VinculoId = int.Parse(form["vinculoId"]),
                Descricao = form["Descricao"],
                ContentType = form["ContentType"],
                Dados = form["Dados"].FirstOrDefault()?.ToByteArray(),
            });
        }
    }

    internal class CartografiaArquivoEntityBinder : IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var form = await bindingContext.HttpContext.Request.ReadFormAsync();
            bindingContext.Result = ModelBindingResult.Success(new CartografiaArquivoViewModel
            {
                Tipo = form["Tipo"], 
                VinculoId = int.Parse(form["vinculoId"]),
                Descricao = form["Descricao"],
                ContentType = form["ContentType"],
                Dados = form["Dados"].FirstOrDefault()?.ToByteArray(),
            });
        }
    }

    static class ByteArrayExt
    {
        public static byte[] ToByteArray(this string byteString)
        {
            string[] byteValues = byteString.Split(',');
            byte[] byteArray = new byte[byteValues.Length];

            for (int i = 0; i < byteValues.Length; i++)
            {
                byteArray[i] = Convert.ToByte(byteValues[i]);
            }

            return byteArray;
        }
    }
}
