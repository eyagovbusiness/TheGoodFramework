using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace TGF.CA.Presentation.MinimalAPI
{
    public static class EndpointExtensions
    {
        public static RouteHandlerBuilder MapDeleteRoute(this IEndpointRouteBuilder endpoints, RouteDefinition routeInfo, Delegate handler)
        {
            return endpoints.MapDelete(routeInfo.Route, handler)
                            .WithName(routeInfo.OperationId);
        }

        public static RouteHandlerBuilder MapGetRoute(this IEndpointRouteBuilder endpoints, RouteDefinition routeInfo, Delegate handler)
        {
            return endpoints.MapGet(routeInfo.Route, handler)
                            .WithName(routeInfo.OperationId);
        }

        public static RouteHandlerBuilder MapPostRoute(this IEndpointRouteBuilder endpoints, RouteDefinition routeInfo, Delegate handler)
        {
            return endpoints.MapPost(routeInfo.Route, handler)
                            .WithName(routeInfo.OperationId);
        }

        public static RouteHandlerBuilder MapPutRoute(this IEndpointRouteBuilder endpoints, RouteDefinition routeInfo, Delegate handler)
        {
            return endpoints.MapPut(routeInfo.Route, handler)
                            .WithName(routeInfo.OperationId);
        }

        public static RouteHandlerBuilder MapPatchRoute(this IEndpointRouteBuilder endpoints, RouteDefinition routeInfo, Delegate handler)
        {
            return endpoints.MapPatch(routeInfo.Route, handler)
                            .WithName(routeInfo.OperationId);
        }
    }
}
