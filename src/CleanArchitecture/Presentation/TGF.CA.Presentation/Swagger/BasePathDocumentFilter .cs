using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TGF.CA.Presentation.Swagger
{
    public class BasePathDocumentFilter : IDocumentFilter
    {
        private readonly string _basePath;

        public BasePathDocumentFilter(string basePath)
        {
            _basePath = basePath;
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            swaggerDoc.Servers.Add(new OpenApiServer { Url = _basePath });
        }
    }
}
