namespace GeotecnologiaKNS.Models
{
    /// <summary>
    /// Convenção para papéis que não pertencem a nenhuma indústria.
    /// </summary>
    /// <remarks>
    /// Os papéis internos — Administrador, ClienteAdmin, Solicitante, Analista —
    /// são semeados na subida da aplicação e valem para todos os inquilinos.
    /// Ficam com <see cref="TenantId"/> zero, que nenhuma indústria real usa,
    /// porque a coluna é obrigatória e não aceita nulo.
    /// </remarks>
    public static class RoleGlobal
    {
        /// <summary>TenantId reservado aos papéis internos, comuns a todas as indústrias.</summary>
        public const int TenantId = 0;

        public static bool EhGlobal(ApplicationRole role) => role.TenantId == TenantId;
    }
}
