using log4net;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Reflection;
using TGF.Common.Extensions;
using EntityState = System.Data.Entity.EntityState;

namespace TGF.Model
{
    /// <summary>
    /// Class to provide custom DataContext from <see cref="System.Data.Entity.DbContext"/> to be used in UnitOfWork and Repository patterns.
    /// </summary>
    public abstract class DataContextBase : DbContext, IDisposable
    {
        private static readonly ILog mLog = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string DefaultSchema { get; protected set; }

        public DataContextBase()
        { }

        public DataContextBase(string aConnectionString)
            : base(aConnectionString)
        {
        }

        /// <summary>
        /// Override that modifies builder conventions and verifies the schema.
        /// </summary>
        /// <param name="aModelBuilder">.</param>
        protected override void OnModelCreating(DbModelBuilder aModelBuilder)
        {
            if (!DefaultSchema.IsNullOrWhiteSpace())
                aModelBuilder.HasDefaultSchema(DefaultSchema);

            aModelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
            aModelBuilder.Conventions.Remove<OneToManyCascadeDeleteConvention>();

            base.OnModelCreating(aModelBuilder);
        }

        private IDictionary<object, object> GetValidationContextItems()
        {
            return new Dictionary<object, object>() { { "context", this } };
        }

        //needs work, maybe fluent validation.
        /// <summary>
        /// Validates a given entity.
        /// </summary>
        /// <param name="aEntity">Entity to validate.</param>
        /// <returns><see cref="ValidationResult"/>.</returns>
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

        /// <summary>
        /// Save changes in this DataContext.
        /// </summary>
        /// <returns>The number of state entries written to the underlying database.</returns>
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
