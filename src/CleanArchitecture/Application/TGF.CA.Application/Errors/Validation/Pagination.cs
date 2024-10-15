using TGF.Common.ROP.Errors;

namespace TGF.CA.Application.Errors.Validation
{
    public partial class TGFApplicationErrors
    {
        public partial class Validation
        {
            public class Pagination
            {
                public static Error InconsistentParameters => new(
                     "Validation.Pagination.InconsistentParameters",
                     "Both Page and PageSize must be specified or neither must be specified."
                 );
                public const string Page_Code = "Validation.Pagination.Page";
                public const string PageSize_Code = "Validation.Pagination.PageSize";
            }
        }
    }
}
