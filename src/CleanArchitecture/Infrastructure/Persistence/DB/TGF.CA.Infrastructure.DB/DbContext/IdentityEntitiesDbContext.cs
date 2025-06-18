using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TGF.CA.Domain.Primitives;

namespace TGF.CA.Infrastructure.DB.DbContext {
    public class IdentityEntitiesDbContext<TDbContext, TUser>(DbContextOptions options) : IdentityDbContext<TUser>(options)
       where TDbContext : Microsoft.EntityFrameworkCore.DbContext
       where TUser : IdentityUser {
        public override int SaveChanges() {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps() {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is EntityBase &&
                            (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries) {
                if (entry.Entity is EntityBase entity) {
                    if (entry.State == EntityState.Added) {
                        entity.CreatedAt = DateTimeOffset.UtcNow;
                        entity.ModifiedAt = entity.CreatedAt;
                    } else if (entry.State == EntityState.Modified) {
                        entity.ModifiedAt = DateTimeOffset.UtcNow;
                    }
                }
            }
        }
    }
}
