using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq.Expressions;
using System.Reflection;

namespace GeotecnologiaKNS.Views
{
    public static class ViewHelpers
    {
        public static IHtmlContent DisplayStatusFor<TModel, TResult>(this IHtmlHelper<TModel> htmlHelper, Expression<Func<TModel, TResult>> expression)
        {
            StatusGetter statusGetter = new StatusGetter();
            statusGetter.Visit(expression);
            var status = statusGetter.Value ?? throw new ArgumentNullException();
            return new HtmlString($"<a class=\"btn btn-{_statusDict[status]}\">{status}</a>");
        }

        static Dictionary<Status, string> _statusDict = new()
        {
            { Status.Monitorada, "success" },
            { Status.Solicitado, "primary" },
            { Status.Bloqueado, "danger" },
            { Status.Alerta, "warning" },
        };

        class StatusGetter : ExpressionVisitor
        {
            private Func<object?, object?>? get;

            public Status? Value { get; set; }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Type.Equals(typeof(Status)) && node.Member is PropertyInfo prop)
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
                         .Contains(typeof(Status)) 
                         &&

                    (get?.Invoke(fieldInfos[0].GetValue(node.Value)) is Status value))
                {
                    Value = value;
                };

                return base.VisitConstant(node);
            }
        }
    }
}
