using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq.Expressions;
using System.Reflection;

namespace GeotecnologiaKNS.Views
{
    public static class ViewHelperValidacao
    {
        public static IHtmlContent DisplayValidacao<TModel>(this IHtmlHelper<TModel> htmlHelper, Validacao validacao)
        {
            return new HtmlString($"<a class=\"btn btn-{_validacaoDict[validacao]}\">{validacao}</a>");
        }


        public static IHtmlContent DisplayValidacaoFor<TModel, TResult>(this IHtmlHelper<TModel> htmlHelper, Expression<Func<TModel, TResult>> expression)
        {
            ValidacaoGetter validacaoGetter = new ValidacaoGetter();
            validacaoGetter.Visit(expression);
            var validacao = validacaoGetter.Value ?? throw new ArgumentNullException();
            return new HtmlString($"<a class=\"btn btn-{_validacaoDict[validacao]}  status-btn\"> {validacao}</a>");
        }


        static Dictionary<Validacao, string> _validacaoDict = new()
        {
            { Validacao.Validado, "success" },
            { Validacao.Inválido, "danger" }
        };

        class ValidacaoGetter : ExpressionVisitor
        {
            private Func<object?, object?>? get;

            public Validacao? Value { get; set; }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Type.Equals(typeof(Validacao)) && node.Member is PropertyInfo prop)
                {
                    this.get = prop.GetValue;
                }

                return base.VisitMember(node);
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                var fieldInfos = node
                    .Value?
                    .GetType()
                    .GetFields();

                if (fieldInfos?.FirstOrDefault() is FieldInfo field &&

                    field.FieldType
                         .GetProperties()
                         .Select(x => x.PropertyType)
                         .Contains(typeof(Validacao))
                         &&

                    (get?.Invoke(fieldInfos[0].GetValue(node.Value)) is Validacao value))
                {
                    Value = value;
                };

                return base.VisitConstant(node);
            }
        }
    }
}
