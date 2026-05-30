using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.ApiResultHandling;

public static class ResultExtensions
{
    public static ActionResult<Result<T>> ToActionResult<T>(this Result<T> result)
        => ResultFactory.Create(result.IsSuccess, result.Status, result.Data, result.Message);
}