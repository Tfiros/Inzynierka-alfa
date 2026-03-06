using System.Security.Claims;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Chat.DTOs;
using ItemTradeApp.Features.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.Chat;

[ApiController]
[Route("[controller]")]
public sealed class ChatController(IChatService chatService) : ControllerBase
{
    private string? GetAuth0UserId()
        => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpPost("dm/{otherUserId:int}")]
    [Authorize]
    public async Task<ActionResult<Result<CreateDmChatResponse>>> CreateDm(
        [FromRoute] int otherUserId,
        CancellationToken ct)
    {
        var res = await chatService.CreateDmAsync(otherUserId, GetAuth0UserId(), ct);
        return res.ToActionResult();
    }

    [HttpGet("threads")]
    [Authorize]
    public async Task<ActionResult<Result<IReadOnlyList<ChatThreadListItemDto>>>> GetThreads(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var res = await chatService.GetThreadsAsync(page, pageSize, search, GetAuth0UserId(), ct);
        return res.ToActionResult();
    }

    [HttpGet("threads/{chatId:int}/messages")]
    [Authorize]
    public async Task<ActionResult<Result<IReadOnlyList<ChatMessageDto>>>> GetMessages(
        [FromRoute] int chatId,
        [FromQuery] long? beforeMessageId = null,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var res = await chatService.GetMessagesAsync(chatId, beforeMessageId, pageSize, GetAuth0UserId(), ct);
        return res.ToActionResult();
    }
    
    [HttpPut("messages/{messageId:long}")]
    [Authorize]
    public async Task<ActionResult<Result<ChatMessageDto>>> EditMessage(
        [FromRoute] long messageId,
        [FromBody] EditMessageRequest? request,
        CancellationToken ct = default)
    {
        var res = await chatService.EditMessageAsync(messageId, request, GetAuth0UserId(), ct);
        return res.ToActionResult();
    }

    [HttpDelete("messages/{messageId:long}")]
    [Authorize]
    public async Task<ActionResult<Result<string>>> DeleteMessage(
        [FromRoute] long messageId,
        CancellationToken ct = default)
    {
        var res = await chatService.DeleteMessageAsync(messageId, GetAuth0UserId(), ct);
        return res.ToActionResult();
    }

    [HttpPost("threads/{chatId:int}/read")]
    [Authorize]
    public async Task<ActionResult<Result<ChatReadStateDto>>> MarkRead(
        [FromRoute] int chatId,
        [FromBody] MarkReadRequest? request,
        CancellationToken ct = default)
    {
        var res = await chatService.MarkReadAsync(chatId, request, GetAuth0UserId(), ct);
        return res.ToActionResult();
    }
}
