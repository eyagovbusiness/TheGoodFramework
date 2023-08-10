using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace TGF.CA.Presentation.Middleware
{

    /// <summary>
    /// Extension class that brings support setting ednpoint metadata for the different responses it may return to the requests.
    /// </summary>
    public static class EndpointRouteHandlerBuilderExtensions
    {
        /// <summary>
        /// Private class of this extension class to support unification logic unification in one single method for both <see cref="SetResponseMetadata"/> and <see cref="SetResponseMetadata{T}"/>.
        /// This class is allowing us to unify the logic of both generic and not geenric method into one single privte method <see cref="SetResponseMetadataInternal{T}"/> where internally this class (<see cref="InternalNoSuccessResultType"/>) is used as a placeholder to support the not generic variant of the method "SetResponseMetadata".
        /// </summary>
        private class InternalNoSuccessResultType { }

        /// <summary>
        /// Sets the response metadata of all possible responses this endpoint can send.
        /// </summary>
        /// <typeparam name="TSuccessResultType">The Type used to populate the response body on successful responses.</typeparam>
        /// <param name="aRouteHandlerBuilder"><see cref="RouteHandlerBuilder"/> of the endpoint source where the metadata will be set.</param>
        /// <param name="aPossibleResponseStatusCodes">List of pameters that defines all the possible Http response codes this endpoint can use in the responses. This includes both, success or failure response codes.</param>
        /// <returns><see cref="RouteHandlerBuilder"/></returns>
        public static RouteHandlerBuilder SetResponseMetadata<TSuccessResultType>(
            this RouteHandlerBuilder aRouteHandlerBuilder, params int[] aPossibleResponseStatusCodes)
        => SetResponseMetadataInternal<TSuccessResultType>(aRouteHandlerBuilder, aPossibleResponseStatusCodes);


        /// <summary>
        /// Sets the response metadata of all possible responses this endpoint can send.
        /// </summary>
        /// <param name="aRouteHandlerBuilder"><see cref="RouteHandlerBuilder"/> of the endpoint source where the metadata will be set.</param>
        /// <param name="aPossibleResponseStatusCodes">List of pameters that defines all the possible Http response codes this endpoint can use in the responses. This includes both, success or failure response codes.</param>
        /// <returns><see cref="RouteHandlerBuilder"/></returns>
        public static RouteHandlerBuilder SetResponseMetadata(
            this RouteHandlerBuilder aRouteHandlerBuilder, params int[] aPossibleResponseStatusCodes)
        => SetResponseMetadataInternal<InternalNoSuccessResultType>(aRouteHandlerBuilder, aPossibleResponseStatusCodes);


        /// <summary>
        /// Private implementation with the unified logic for both "SetResponseMetadata" methods.
        /// </summary>
        private static RouteHandlerBuilder SetResponseMetadataInternal<TSuccessResultType>(
            RouteHandlerBuilder aRouteHandlerBuilder, int[] aPossibleStatusCodes)
        {
            if (aPossibleStatusCodes != null)
            {
                foreach (int statusCode in aPossibleStatusCodes)
                {
                    if (statusCode >= 200 && statusCode <= 299)
                    {
                        if (typeof(TSuccessResultType) != typeof(InternalNoSuccessResultType))
                            aRouteHandlerBuilder.Produces<TSuccessResultType>(statusCode);
                        else
                            aRouteHandlerBuilder.Produces(statusCode);
                    }
                    else
                        aRouteHandlerBuilder.ProducesProblem(statusCode);
                }
            }

            return aRouteHandlerBuilder.ProducesProblem(500);
        }
    }

}
