using System.Net;

namespace ItemTradeApp.ApiResultHandling;

public enum ResultStatus
{
    Success = HttpStatusCode.OK,
    Unauthorized = HttpStatusCode.Unauthorized,
    BadRequest = HttpStatusCode.BadRequest,
    NotFound = HttpStatusCode.NotFound,
    NoContent = HttpStatusCode.NoContent,
    Conflict = HttpStatusCode.Conflict,
    Created = HttpStatusCode.Created,
    Forbidden = HttpStatusCode.Forbidden,
    InternalServerError = HttpStatusCode.InternalServerError,
}