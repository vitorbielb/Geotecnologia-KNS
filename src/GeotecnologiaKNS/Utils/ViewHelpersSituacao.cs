using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq.Expressions;
using System.Reflection;

namespace GeotecnologiaKNS.Views
{
    public static class ViewHelpersSituacao
    {
        public static IHtmlContent DisplaySituacao<TModel>(this IHtmlHelper<TModel> htmlHelper, Situacao situacao)
        {
            return new HtmlString($"<a class=\"btn btn-{_situacaoDict[situacao]}\">{situacao}</a>");
        }


        public static IHtmlContent DisplaySituacaoFor<TModel, TResult>(this IHtmlHelper<TModel> htmlHelper, Expression<Func<TModel, TResult>> expression)
        {
            SituacaoGetter situacaoGetter = new SituacaoGetter();
            situacaoGetter.Visit(expression);
            var situacao = situacaoGetter.Value ?? throw new ArgumentNullException();
            return new HtmlString($"<a class=\"btn btn-{_situacaoDict[situacao]}  status-btn\"> {situacao}</a>");
        }


        static Dictionary<Situacao, string> _situacaoDict = new()
        {
            { Situacao.Validado, "success" },
            { Situacao.Inválido, "danger" }
        };

        class SituacaoGetter : ExpressionVisitor
        {
            private Func<object?, object?>? get;

            public Situacao? Value { get; set; }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Type.Equals(typeof(Situacao)) && node.Member is PropertyInfo prop)
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
                         .Contains(typeof(Situacao))
                         &&

                    (get?.Invoke(fieldInfos[0].GetValue(node.Value)) is Situacao value))
                {
                    Value = value;
                };

                return base.VisitConstant(node);
            }
        }
    }
}
