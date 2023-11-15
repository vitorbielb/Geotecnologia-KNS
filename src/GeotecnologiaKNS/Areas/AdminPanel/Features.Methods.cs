namespace GeotecnologiaKNS.Infra;

using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;

public partial class Features
{
    #region Static methods
    public static IEnumerable<Claim> GetAll(string defaultValue)
    {
        return typeof(Features.List)
            .GetProperties()
            .SelectMany(p => p.PropertyType
            .GetProperties()
            .Where(p1 => !p1.GetCustomAttributes<NonFeatureAttribute>().Any())
            .Select(p1 => p.PropertyType.Name + "." + p1.Name))
            .Select(name => new Claim(name, defaultValue));
    }
    #endregion

    #region Display methods
    public bool Everything() => false;
    public bool EverythingExcept(params Expression<Func<Features.List, object>>[] _) => false;
    public bool OnlyAccess(params Expression<Func<Features.List, object>>[] _) => false;
    #endregion
}
