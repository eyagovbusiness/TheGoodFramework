using TGF.Common.ROP.Errors;

namespace TGF.CA.Application.Errors.Validation
{
    public partial class TGFApplicationErrors
    {
        public partial class Validation
        {
            public class Sorting
            {
                public const string SortByEmpty_Code = "Validation.Sorting.SortByEmpty";
                public static Error SortByInvalid => new(
                    "Validation.Sorting.SortByInvalid",
                    "The sortBy field must be a valid property name of the target object."
                );
                public static Error InconsistentParameters => new(
                    "Validation.Sorting.InconsistentParameters",
                    "Both SortBy and SortDirection must be specified or neither must be specified."
                );

            }
        }
    }
}
