using Polly;
using Polly.Extensions.Http;

namespace ItemTradeApp.Policies;

public static class HttpPolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(
        ILogger logger,
        int retryCount = 2)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    logger.LogWarning(
                        "HTTP retry {RetryAttempt} after {Delay}s. StatusCode: {StatusCode}, Exception: {Exception}",
                        retryAttempt,
                        timespan.TotalSeconds,
                        outcome.Result?.StatusCode,
                        outcome.Exception?.Message);
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(
        ILogger logger,
        int handledEventsAllowedBeforeBreaking = 5,
        int durationOfBreakSeconds = 30)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking,
                TimeSpan.FromSeconds(durationOfBreakSeconds),
                onBreak: (outcome, breakDelay) =>
                {
                    logger.LogWarning(
                        "HTTP circuit breaker OPEN for {BreakDelay}s. StatusCode: {StatusCode}, Exception: {Exception}",
                        breakDelay.TotalSeconds,
                        outcome.Result?.StatusCode,
                        outcome.Exception?.Message);
                },
                onReset: () =>
                {
                    logger.LogInformation("HTTP circuit breaker CLOSED");
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("HTTP circuit breaker HALF-OPEN");
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(
        int seconds = 20)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(seconds));
    }
}