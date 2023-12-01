namespace GeotecnologiaKNS.Infra;

public static partial class Roles
{
    public static RoleClaims Administrador => new(
        access: p => p.Everything());

    public static RoleClaims ClienteAdmin => new(
        access: p => p.EverythingExcept(
            p => p.Tenant,
            p => p.Solicitacao.Update));

    public static RoleClaims Solicitante => new(
        access: p => p.OnlyAccess(
            p => p.Produtor.Read,
            p => p.Produtor.Create,
            p => p.Produtor.Update,
            p => p.Produtor.Delete,

            p => p.Propriedade.Read,
            p => p.Propriedade.Create,
            p => p.Propriedade.Update,
            p => p.Propriedade.Delete,

            p => p.Solicitacao.Read,
            p => p.Solicitacao.Create,
            p => p.Solicitacao.Delete));

    public static RoleClaims Analista => new(
        access: p => p.OnlyAccess(
            p => p.Produtor.Read,
            p => p.Propriedade.Read,
            p => p.Solicitacao.Read,
            p => p.Solicitacao.Update));
}
