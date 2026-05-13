namespace ItemTradeApp.Features.Trades.DTOs.Request;

public sealed class UploadTradeImageRequest
{
    public IFormFile Image { get; set; } = default!;
    public bool IsBuyers { get; set; }
}