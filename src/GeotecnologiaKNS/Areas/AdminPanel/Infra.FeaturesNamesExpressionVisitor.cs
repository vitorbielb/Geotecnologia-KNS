using System.Linq.Expressions;
using System.Reflection;

namespace GeotecnologiaKNS.Infra;

/// <summary>
/// Visitor class for processing permission expressions.
/// </summary>
public class FeaturesNamesExpressionVisitor : ExpressionVisitor
{
    private readonly HashSet<string> operationNames = new();

    public ICollection<string> OperationNames => operationNames;

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Member is PropertyInfo propertyInfo && 
            propertyInfo.PropertyType.IsAssignableTo(typeof(IFeature)))
        {
            var memberProperties = propertyInfo
                                       .PropertyType
                                       .GetProperties()
                                       .OfType<PropertyInfo>()
                                       .Where(p => p.PropertyType?.Equals(typeof(IOperation)) ?? false);

            foreach (var property in memberProperties)
            {
                OperationNames.Add(string.Format("{0}.{1}", node.Member.Name, property.Name));
            }

            return node;
        }

        string originalString = node.ToString();
        int firstDotIndex = originalString.IndexOf('.');
        string result = originalString[(firstDotIndex + 1)..];

        if (!OperationNames.Any(val => val.StartsWith(result)))
        {
            OperationNames.Add(result);
        }

        return node;
    }
}
