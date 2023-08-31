namespace GeotecnologiaKNS.Infra;
using System.Linq.Expressions;
#pragma warning disable CS8618

public partial class Features
{
    public Tenant Tenants { get; set; }
    public User Users { get; set; }
    public Produtor Produtores { get; set; }
    public Propriedade Propriedades { get; set; }
    public Solicitacao Solicitacoes { get; set; }

    #region display methods
    public bool Everything() => false;
    public bool EverythingExcept<T>(params Expression<Func<Features, T>>[] _) => false;
    public bool OnlyAccess<T>(params Expression<Func<Features, T>>[] _) => false;
    #endregion
}

public static class PermissionsByRole
{
    public const string ApplicationAdminRoleName = "ApplicationAdmin";
    public const string TenantAdminRoleName = "TenantAdmin";
    public const string ProcessorRoleName = "Processor";
    public const string RequesterRoleName = "Requester";

    public static RoleClaims ApplicationAdmin =>
        new(name: ApplicationAdminRoleName, access: p => p.Everything());

    public static RoleClaims TenantAdmin =>
        new(name: TenantAdminRoleName, access: p => p.EverythingExcept(p => p.Tenants));

    public static RoleClaims Requester =>
        new(name: RequesterRoleName, access: p => p.OnlyAccess(
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

    public static RoleClaims Processor => new(name: ProcessorRoleName, access: p => p.OnlyAccess(
            p => p.Produtores.Read,
            p => p.Propriedades.Read,
            p => p.Solicitacoes.Read,
            p => p.Solicitacoes.Update));

    public static IEnumerable<RoleClaims> Permissions => new[]
    {
        ApplicationAdmin,
        TenantAdmin,
        Requester,
        Processor
    };
}

