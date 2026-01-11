using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;

namespace ItemTradeApp.Features.Users.UserInfo;

public interface IUserInfoOfferService
{
    Task<Result<PagedResponse<OfferListingDTO>>> GetPagedAsync(int id, int page, int pageSize,
        CancellationToken ct = default);
}

public class UserInfoOfferService(IUserInfoOfferRepository userInfoOfferRepository, IUserInfoRepository userInfoRepository) : IUserInfoOfferService
{
    public async Task<Result<PagedResponse<OfferListingDTO>>> GetPagedAsync(int id, int page, int pageSize, CancellationToken ct = default)
    {
        if (page <= 0) return Result<PagedResponse<OfferListingDTO>>.BadRequest("invalid_page_number");
        if (pageSize <= 0) return Result<PagedResponse<OfferListingDTO>>.BadRequest("invalid_page_size");
        pageSize = pageSize > 100 ? 100 : pageSize;
        var userExists = await userInfoRepository.ExistsByIdAsync(id, ct);
        if (!userExists) return Result<PagedResponse<OfferListingDTO>>.NotFound("user_not_found");
        var (userOffers,userOffersTotalCount) = await userInfoOfferRepository.GetForUserByIdPagedAsync(id,page,pageSize,ct);
        var totalPages = (int)Math.Ceiling(userOffersTotalCount / (double)pageSize);

        var response = new PagedResponse<OfferListingDTO>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = userOffersTotalCount,
            TotalPages = totalPages,
            Elements = userOffers
        };
        return Result<PagedResponse<OfferListingDTO>>.Success(response);
    }
}