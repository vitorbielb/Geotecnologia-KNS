using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GeotecnologiaKNS.Utils
{
    internal class ArquivoEntityBinder<T> : IModelBinder
        where T : ArquivoViewModel, new()
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var form = await bindingContext.HttpContext.Request.ReadFormAsync();
            bindingContext.Result = ModelBindingResult.Success(new T
            {
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
