using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GeotecnologiaKNS.Handlers
{
    public interface IEntityHandler
    {
        void Handle(object sender, EntityEntryEventArgs e);
    }
}
