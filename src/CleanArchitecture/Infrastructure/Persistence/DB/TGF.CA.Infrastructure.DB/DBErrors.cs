using System.Net;
using TGF.Common.ROP.Errors;

namespace TGF.CA.Infrastructure.DB
{
    public static class DBErrors
    {
        public static class Repository
        {
            public static class Entity
            {
                public static HttpError NotFound => new(
                new Error("Entity.NotFound",
                    "The entity was not found in the DB."),
                HttpStatusCode.NotFound);

            }

            public static class Save
            {
                public static HttpError Error => new(
                new Error("Save.Error",
                    "Error saving the entity changes in the DB."),
                HttpStatusCode.InternalServerError);

                public static HttpError NoChanges => new(
                new Error("Save.NoChanges",
                    "Error, there were no changes to save in DB but they were expected."),
                HttpStatusCode.InternalServerError);

            }
        }


    }
}
