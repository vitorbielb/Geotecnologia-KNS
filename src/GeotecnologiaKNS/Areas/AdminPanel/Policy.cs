using Microsoft.AspNetCore.Authorization;
using System.Linq.Expressions;

namespace GeotecnologiaKNS.Infra
{
    ///<summary>
    ///Extension methods for authorization policies.
    ///</summary>
    public static class PolicyExtension
    {
        ///<summary>
        ///Requires an operation for authorization policy builder.
        ///</summary>
        ///<param name="policy">Authorization policy builder.</param>
        ///<param name="operation">Operation of the feature list.</param>
        ///<returns>A builder with the required operation(s).</returns>
        public static AuthorizationPolicyBuilder RequireOperation(this AuthorizationPolicyBuilder policy, Expression<Func<Features.List, IOperation>> operation)
        {
            var visitor = new FeaturesNamesExpressionVisitor();
            visitor.Visit(operation);

            foreach (var item in visitor.OperationNames)
                policy.RequireClaim(item, Enabled);

            return policy;
        }

        ///<summary>
        ///Requires a feature for authorization policy builder.
        ///</summary>
        ///<param name="policy">Authorization policy builder.</param>
        ///<param name="feature">Feature of the feature list.</param>
        ///<returns>A builder with the required feature(s).</returns>
        public static AuthorizationPolicyBuilder RequireFeature(this AuthorizationPolicyBuilder policy, Expression<Func<Features.List, IFeature>> feature)
        {
            var visitor = new FeaturesNamesExpressionVisitor();
            visitor.Visit(feature);

            foreach (var item in visitor.OperationNames)
                policy.RequireClaim(item, Enabled);

            return policy;
        }
    }
}