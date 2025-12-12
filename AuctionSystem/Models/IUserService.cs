using AuctionSystem.Domain;

namespace AuctionSystem.Models
{
    public interface IUserService
    {
        void AddUser(UserAccount user);
        decimal GetMaxBidAmount(long chatId);
        bool TryGetUser(long id, out UserAccount? user);
    }
}