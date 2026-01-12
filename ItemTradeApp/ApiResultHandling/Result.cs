using ItemTradeApp.ApiResultHandling;

public class Result<T>(bool isSuccess, ResultStatus status, T? data, string? message = null)
{
    public bool IsSuccess { get; init; } = isSuccess;
    public ResultStatus Status { get; init; } = status;
    public T? Data { get; init; } = data;
    public string? Message { get; set; } = message;

    public static Result<T> Success(T? data, string? message = null)
        => new(true, ResultStatus.Success, data, message);
    public static Result<T> Created(T? data, string? message = null)
        => new(true, ResultStatus.Created, data, message);
    public static Result<T> NoContent(string? message = null)
        => new(true, ResultStatus.NoContent, default, message);
    public static Result<T> Forbidden(string? message = null) =>
     new(false,ResultStatus.Forbidden,default, message);
    public static Result<T> BadRequest(string? message = null)
        => new(false, ResultStatus.BadRequest, default, message);

    public static Result<T> Unauthorized(string? message = null)
        => new(false, ResultStatus.Unauthorized, default, message);

    public static Result<T> NotFound(string? message = null)
        => new(false, ResultStatus.NotFound, default, message);

    public static Result<T> Conflict(string? message = null)
        => new(false, ResultStatus.Conflict, default, message);

    public static Result<T> InternalServerError(string? message = null)
        => new(false, ResultStatus.InternalServerError, default, message);
}