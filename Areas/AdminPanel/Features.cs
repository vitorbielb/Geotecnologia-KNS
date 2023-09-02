namespace GeotecnologiaKNS.Infra;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
#pragma warning disable CS8618

public partial class Features
{
    public Tenant Tenants { get; set; }
    public User Users { get; set; }
    public Produtor Produtores { get; set; }
    public Propriedade Propriedades { get; set; }
    public Solicitacao Solicitacoes { get; set; }
    public Role Roles { get; set; }

    #region Static methods
    public static IEnumerable<Claim> GetAll(string defaultValue)
    {
        return typeof(Features)
            .GetProperties()
            .SelectMany(p => p.PropertyType
            .GetProperties()
            .Where(p1 => p1.Name != nameof(IFeature.FeatureName) && !p1.GetCustomAttributes<NonFeatureAttribute>().Any())
            .Select(p1 => p.Name + "." + p1.Name))
            .Select(name => new Claim(name, defaultValue));
    }
    #endregion

    #region Display methods
    public bool Everything() => false;
    public bool EverythingExcept<T>(params Expression<Func<Features, T>>[] _) => false;
    public bool OnlyAccess<T>(params Expression<Func<Features, T>>[] _) => false;
    #endregion
}

public static class Roles
{
    public static RoleClaims ApplicationAdmin =>
        new(name: nameof(ApplicationAdmin), access: p => p.Everything());

    public static RoleClaims TenantAdmin =>
        new(name: nameof(TenantAdmin), access: p => p.EverythingExcept(p => p.Tenants));

    public static RoleClaims Requester =>
        new(name: nameof(Requester), access: p => p.OnlyAccess(
            p => p.Produtores.Read,
            p => p.Produtores.Create,
            p => p.Produtores.Update,
            p => p.Produtores.Delete,

            p => p.Propriedades.Read,
            p => p.Propriedades.Create,
            p => p.Propriedades.Update,
            p => p.Propriedades.Delete,

            p => p.Solicitacoes.Read,
            p => p.Solicitacoes.Create,
            p => p.Solicitacoes.Delete));

    public static RoleClaims Processor => 
        new(name: nameof(Processor), access: p => p.OnlyAccess(
            p => p.Produtores.Read,
            p => p.Propriedades.Read,
            p => p.Solicitacoes.Read,
            p => p.Solicitacoes.Update));

    #region Static methods
    public static IEnumerable<RoleClaims> GetRoleClaims()
    {
        return typeof(Roles)
        .GetProperties()
        .Select(PropertyValue);
    }

    private static RoleClaims PropertyValue(PropertyInfo property) =>
        (RoleClaims)property.GetValue(null)!;
    #endregion
}

