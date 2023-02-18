using log4net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using TheGoodFramework.Extensions;

namespace TheGoodFramework.Model
{
    public abstract class DbContextBase : DbContext, IDisposable
    {
        private static readonly ILog mLog = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string DefaultSchema { get; protected set; }

        public DbContextBase()
        { }

        //This needs work
        public DbContextBase(string aConnectionString)
            : base()
        {
            DbContextOptionsBuilder a = new DbContextOptionsBuilder();

            a.UseSqlServer(aConnectionString);
            this.OnConfiguring(a);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder aConfigurationBuilder)
        {
            //aConfigurationBuilder.Conventions.Add(_ => new ..);
            //aConfigurationBuilder.Conventions.Remove(_ => ..);
            //aConfigurationBuilder.Conventions.Replace(...);

            base.ConfigureConventions(aConfigurationBuilder);
        }

        protected override void OnModelCreating(ModelBuilder aModelBuilder)
        {
            if (!DefaultSchema.IsNullOrWhiteSpace())
                aModelBuilder.HasDefaultSchema(DefaultSchema);

            base.OnModelCreating(aModelBuilder);
        }

        private IDictionary<object, object> GetValidationContextItems()
        {
            return new Dictionary<object, object>() { { "context", this } };
        }

        //needs work, maybe fluent validation
        public IEnumerable<ValidationResult> Validate(object aEntity)
        {
            var lValidatableObject = aEntity as IValidatableObject;
            if (lValidatableObject != null)
            {
                var lValidationContext = new ValidationContext(aEntity, this.GetValidationContextItems());
                var x = lValidatableObject.Validate(lValidationContext);
                return x;
            }
            return null;
        }

        public override int SaveChanges()
        {
            var entities = from e in ChangeTracker.Entries()
                           where e.State == EntityState.Added
                                 || e.State == EntityState.Modified
                           select e.Entity;
            foreach (var entity in entities)
            {
                var validationContext = new ValidationContext(entity);
                Validator.ValidateObject(entity, validationContext);
            }

            return base.SaveChanges();
        }

    }
}
