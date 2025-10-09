namespace ItemTradeApp.ExceptionsHandling;

public readonly record struct AppError(int StatusCode, string Body, string? Message = null);

public readonly struct Result<T>
{
    public bool IsSuccess { get;}
    public T? Value { get; }
    public AppError? Error { get;}


    private Result(T value) {IsSuccess = true;Value = value; Error = null;}
    private Result(AppError error) {IsSuccess = false;Value = default; Error = error;}
    
    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(AppError error) => new(error);
    public Result<U> Map<U>(Func<T, U> f) =>
        IsSuccess ? Result<U>.Ok(f(Value!)) : Result<U>.Fail(Error!.Value);

    public U Match<U>(Func<T, U> onOk, Func<AppError, U> onErr) =>
        IsSuccess ? onOk(Value!) : onErr(Error!.Value);
    
}