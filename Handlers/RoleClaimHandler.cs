using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GeotecnologiaKNS.Handlers;

public class RoleClaimHandler : EntityHandlerBase<IdentityRoleClaim<string>>, IEntityHandler
{
    public RoleClaimHandler()
    {
    }

    protected override void Handle(object sender, EntityEntryEventArgs e, IdentityRoleClaim<string> current)
    {
        throw new NotImplementedException();
    }
}
