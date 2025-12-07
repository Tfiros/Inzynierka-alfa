using System.Net;

namespace ItemTradeApp.ExceptionsHandling;

public enum ResultStatus
{
    Success = HttpStatusCode.OK,
    Unauthorized = HttpStatusCode.Unauthorized,
    BadRequest = HttpStatusCode.BadRequest,
    NotFound = HttpStatusCode.NotFound,
    NoContent = HttpStatusCode.NoContent,
    Conflict = HttpStatusCode.Conflict,
    Created = HttpStatusCode.Created,
    InternalServerError = HttpStatusCode.InternalServerError,
}