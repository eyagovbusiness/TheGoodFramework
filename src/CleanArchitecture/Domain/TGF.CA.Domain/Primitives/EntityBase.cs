
using System.ComponentModel.DataAnnotations;

namespace TGF.CA.Domain.Primitives
{
    public abstract class EntityBase
    {
        [Required]
        /// <summary>
        /// When the entity was created in DB
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }
        [Required]
        /// <summary>
        /// The last time when the entity was modified, by default it is initially set to <see cref="CreatedAt"/> when the entity is created.
        /// </summary>
        public DateTimeOffset ModifiedAt { get; set; }
    }
}
