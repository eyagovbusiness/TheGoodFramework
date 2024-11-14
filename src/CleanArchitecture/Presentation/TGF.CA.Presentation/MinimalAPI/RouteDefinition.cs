namespace TGF.CA.Presentation.MinimalAPI
{
    public class RouteDefinition(string route, string operationId)
    {
        public string Route { get; } = route;
        public string OperationId { get; } = operationId;
    }
}
