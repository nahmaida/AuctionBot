using Telegram.Bot.Types;

namespace AuctionSystem.Infrastructure.Telegram.Commands
{
    public interface ITelegramCommand
    {
        string Name { get; }                     // /start, Баланс и тд
        Task ExecuteAsync(Message message);
    }
}

