using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TGF.CA.Domain.Primitives;

namespace TGF.CA.Infrastructure.DB.DbContext
{
    public class EntitiesDbContext<TDbContext> : Microsoft.EntityFrameworkCore.DbContext 
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public EntitiesDbContext(DbContextOptions options) : base(options)
        {
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var lEntries = ChangeTracker.Entries()
                .Where(e => e.Entity is EntityBase &&
                            (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in lEntries)
            {
                var lEntity = entry.Entity as EntityBase;
                if (lEntity != null)
                {
                    if (entry.State == EntityState.Added)
                    {
                        lEntity.CreatedAt = DateTimeOffset.UtcNow;
                        lEntity.ModifiedAt = lEntity.CreatedAt;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        lEntity.ModifiedAt = DateTimeOffset.UtcNow;
                    }
                }
            }
        }
    }
}
