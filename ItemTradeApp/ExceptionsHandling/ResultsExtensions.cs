using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.ExceptionsHandling;

public static class ResultMvcExtensions
{
    public static IActionResult Matching<T>(
        this Result<T> res,
        Func<T, IActionResult> onOk,
        Func<AppError, IActionResult> onErr)
        => res.IsSuccess ? onOk(res.Value!) : onErr(res.Error!.Value);
}