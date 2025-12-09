using AuctionSystem.Domain;

namespace AuctionSystem.Models
{
    internal interface IUserService
    {
        void AddUser(UserAccount user);
        decimal GetMaxBidAmount(long chatId);
        bool TryGetUser(long id, out UserAccount? user);
    }
}