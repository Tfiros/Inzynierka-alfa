using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.ApiResultHandling;

public class ResultFactory
{
    public static ActionResult<Result<T>> Create<T>(
        bool isSuccess,
        ResultStatus status,
        T? data,
        string? message = null)
    {
        var res = new Result<T>(isSuccess, status, data, message);
        return ToActionResult(res);
    }
    private static ActionResult<Result<T>> ToActionResult<T>(Result<T> result)
    {
        return result.Status switch
        {
            ResultStatus.Success => new OkObjectResult(result),

            ResultStatus.NoContent => new NoContentResult(),

            ResultStatus.BadRequest => new BadRequestObjectResult(result),

            ResultStatus.Unauthorized => new UnauthorizedObjectResult(result),

            ResultStatus.NotFound => new NotFoundObjectResult(result),

            ResultStatus.Conflict => new ConflictObjectResult(result),
            
            ResultStatus.Created => new ObjectResult(result)
            {
                StatusCode = (int)HttpStatusCode.Created
            },
            ResultStatus.InternalServerError => new ObjectResult(result)
            {
                StatusCode = (int)HttpStatusCode.InternalServerError
            },

            _ => new ObjectResult(result)
            {
                StatusCode = (int)HttpStatusCode.InternalServerError
            }
        };
    }
   
}