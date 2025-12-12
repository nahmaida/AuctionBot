using AuctionSystem.Domain;
using AuctionSystem.Models;
using Telegram.Bot.Types;

namespace AuctionSystem.Infrastructure.Telegram.Commands
{
    public class ViewBalanceCommand : ITelegramCommand
    {
        public string Name => "Баланс";

        private readonly IUserService _users;
        private readonly IMessageSender _sender;

        public ViewBalanceCommand(IUserService users, IMessageSender sender)
        {
            _users = users;
            _sender = sender;
        }

        public async Task ExecuteAsync(Message message)
        {
            long chatId = message.Chat.Id;
            if (!_users.TryGetUser(chatId, out UserAccount? user))
            {
                await _sender.SendMessage(chatId, "Сначала зарегистрируйтесь! (/start)");
                return;
            }

            decimal balance = user.Balance;
            await _sender.SendMessage(chatId, $"💰Баланс: {balance}₽");
        }
    }
}
