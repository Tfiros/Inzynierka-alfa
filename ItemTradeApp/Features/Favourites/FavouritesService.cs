using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Favourites.Repositories;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Favourites;

public interface IFavouritesService
{
    Task<Result<PagedResponse<OfferListingDTO>>> GetFavouritesAsync(
        string auth0UserId, int page, int pageSize, CancellationToken ct = default);
    Task<Result<List<int>>> GetFavouriteIdsAsync(
        string auth0UserId, CancellationToken ct = default);
    Task<Result<bool>> AddFavourite(
        string auth0UserId, int offerId, CancellationToken ct = default);
    Task<Result<bool>> RemoveFavourite(
        string auth0UserId, int offerId, CancellationToken ct = default);
}

public class FavouritesService(IUserRepository userRepository, IFavouritesRepository favouritesRepository, IOffersRepository offersRepository, IUnitOfWork unitOfWork) : IFavouritesService
{
    public async Task<Result<PagedResponse<OfferListingDTO>>> GetFavouritesAsync(
        string auth0UserId, int page, int pageSize, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            return Result<PagedResponse<OfferListingDTO>>.Unauthorized("missing_sub_claim");
        }
        
        if (page <= 0) return Result<PagedResponse<OfferListingDTO>>.BadRequest("invalid_page_number");
        if (pageSize <= 0) return Result<PagedResponse<OfferListingDTO>>.BadRequest("invalid_page_size");
        pageSize = pageSize > 100 ? 100 : pageSize;

        var userId = await userRepository.GetUserIdByAuth0IdAsync(auth0UserId, ct);
        if (userId is null)
        {
            return Result<PagedResponse<OfferListingDTO>>.Unauthorized("user_not_found");
        }

        var (favourites, totalFavouritesCount) =
            await favouritesRepository.GetFavouriteOffersPagedAsync(userId.Value, page, pageSize, ct);
        
        var totalPages = totalFavouritesCount == 0 ? 1 : (int)Math.Ceiling(totalFavouritesCount / (double)pageSize);
        
        var response = new PagedResponse<OfferListingDTO>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalFavouritesCount,
            TotalPages = totalPages,
            Elements = favourites
        };

        return Result<PagedResponse<OfferListingDTO>>.Success(response);
    }

    public async Task<Result<List<int>>> GetFavouriteIdsAsync(
        string auth0UserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            return Result<List<int>>.Unauthorized("missing_sub_claim");
        }

        var userId = await userRepository.GetUserIdByAuth0IdAsync(auth0UserId, ct);
        if (userId is null)
        {
            return Result<List<int>>.Unauthorized("user_not_found");
        }

        var ids = await favouritesRepository.GetFavouriteIdsAsync(userId.Value, ct);

        return Result<List<int>>.Success(ids);
    }

    public async Task<Result<bool>> AddFavourite(
        string auth0UserId, int offerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            return Result<bool>.Unauthorized("missing_sub_claim");
        }
        
        if (offerId <= 0)
        {
            return Result<bool>.BadRequest("incorrect_offer_id");
            
        }
        
        var userId = await userRepository.GetUserIdByAuth0IdAsync(auth0UserId, ct);
        if (userId is null)
        {
            return Result<bool>.Unauthorized("user_not_found");
        }

        if (!await offersRepository.OfferIsActiveAsync(offerId, ct))
        {
            return Result<bool>.BadRequest("offer_not_active");
        }

        if (await favouritesRepository.FavouriteExistsAsync(userId.Value, offerId, ct))
        {
            return Result<bool>.Success(true);
        }

        var favouriteOfferToBeAdded = new UserFavouriteOffer
        {
            User_ID = userId.Value,
            Offer_ID = offerId,
            AddedAt = DateTime.UtcNow
        };

        favouritesRepository.Add(favouriteOfferToBeAdded);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            return Result<bool>.InternalServerError("add_favourite_failed");
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> RemoveFavourite(
        string auth0UserId, int offerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            return Result<bool>.Unauthorized("missing_sub_claim");
        }
        
        if (offerId <= 0)
        {
            return Result<bool>.BadRequest("incorrect_offer_id");
        }
        
        var userId = await userRepository.GetUserIdByAuth0IdAsync(auth0UserId, ct);
        if (userId is null)
        {
            return Result<bool>.Unauthorized("user_not_found");
        }
        
        if (!await offersRepository.OfferExistsAsync(offerId, ct))
        {
            return Result<bool>.BadRequest("offer_not_found");
        }

        await favouritesRepository.RemoveAsync(userId.Value, offerId, ct);

        return Result<bool>.Success(true);
    }
}