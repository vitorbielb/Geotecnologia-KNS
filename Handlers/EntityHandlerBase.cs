using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GeotecnologiaKNS.Handlers
{
    public abstract class EntityHandlerBase<T> : IEntityHandler
        where T : class
    {
        public void Handle(object sender, EntityEntryEventArgs e)
        {
            if (e.Entry.Entity is not T entity)
            {
                return;
            }

            Handle(sender, e, entity);
        }

        protected abstract void Handle(object sender, EntityEntryEventArgs e, T current);
    }
}
