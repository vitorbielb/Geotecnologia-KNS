using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq.Expressions;

namespace GeotecnologiaKNS.Views
{
    public static class ViewHelperValidacao
    {
        private static readonly IReadOnlyDictionary<Validacao, string> ValidacaoCssClassMap =
            new Dictionary<Validacao, string>
            {
                [Validacao.Validado] = "success",
                [Validacao.Pendente] = "secondary"
            };

        public static IHtmlContent DisplayValidacao<TModel>(
            this IHtmlHelper<TModel> htmlHelper,
            Validacao validacao)
        {
            var cssClass = GetCssClass(validacao);
            return new HtmlString($"<a class=\"btn btn-{cssClass} status-btn\">{validacao}</a>");
        }

        public static IHtmlContent DisplayValidacaoFor<TModel>(
            this IHtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, Validacao>> expression)
        {
            ArgumentNullException.ThrowIfNull(htmlHelper);
            ArgumentNullException.ThrowIfNull(expression);

            var model = htmlHelper.ViewData.Model;

            if (model is null)
                return HtmlString.Empty;

            var validacao = expression.Compile().Invoke(model);
            var cssClass = GetCssClass(validacao);

            return new HtmlString($"<a class=\"btn btn-{cssClass} status-btn\">{validacao}</a>");
        }

        private static string GetCssClass(Validacao validacao)
        {
            return ValidacaoCssClassMap.TryGetValue(validacao, out var cssClass)
                ? cssClass
                : "secondary";
        }
    }
}