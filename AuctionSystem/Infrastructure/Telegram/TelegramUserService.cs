using AuctionSystem.Domain;
using AuctionSystem.Models;
using System.Reflection;
using Telegram.Bot.Types;

namespace AuctionSystem.Infrastructure.Telegram
{
    internal class TelegramUserService : IUserService
    {
        private List<UserAccount> Users { get; set; }
        private readonly object _lock = new();

        public TelegramUserService()
        {
            Users = new();
        }

        public bool TryGetUser(long id, out UserAccount? user)
        {
            lock (_lock)
                user = Users.FirstOrDefault(u => u.Id == id);
            return user != null;
        }

        public void AddUser(UserAccount user)
        {
            lock (_lock)
                Users.Add(user);
        }

        public decimal GetMaxBidAmount(long chatId)
        {
            lock (_lock)
                return Users.Max(u => u.Id == chatId ? u.Balance : 0);
        }
    }
}