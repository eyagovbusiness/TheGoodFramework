namespace TGF.CA.Domain.Primitives
{
    /// <summary>
    /// Represents the base class all value objects derive from.
    /// </summary>
    public abstract class ValueObject : IEquatable<ValueObject>
    {
        public static bool operator ==(ValueObject aFirstObject, ValueObject aSecondObject)
        {
            if (aFirstObject is null && aSecondObject is null)
                return true;

            if (aFirstObject is null || aSecondObject is null)
                return false;

            return aFirstObject.Equals(aSecondObject);
        }

        public static bool operator !=(ValueObject aFirstObject, ValueObject aSecondObject) => !(aFirstObject == aSecondObject);

        public bool Equals(ValueObject aOtherObject) => aOtherObject is not null && GetAtomicValues().SequenceEqual(aOtherObject.GetAtomicValues());

        public override bool Equals(object aObject)
        {
            if (aObject == null)
                return false;

            if (GetType() != aObject.GetType())
                return false;

            if (aObject is not ValueObject valueObject)
                return false;

            return GetAtomicValues().SequenceEqual(valueObject.GetAtomicValues());
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            HashCode lHashCode = default;

            foreach (object lObject in GetAtomicValues())
                lHashCode.Add(lObject);

            return lHashCode.ToHashCode();
        }

        /// <summary>
        /// Gets the atomic values of the value object.
        /// </summary>
        /// <returns>The collection of objects representing the value object values.</returns>
        protected abstract IEnumerable<object> GetAtomicValues();

    }
}
