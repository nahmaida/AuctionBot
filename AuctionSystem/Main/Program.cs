using AuctionSystem.Application;
using AuctionSystem.Domain;
using AuctionSystem.Infrastructure.Telegram;
using AuctionSystem.Infrastructure.Telegram.Commands;
using AuctionSystem.Models;
using System.Text;
using Telegram.Bot;

namespace AuctionSystem.Main
{
    internal class Program
    {
        private static async Task Main()
        {
            // Т.к по умолчанию не выводит кирилицу
            Console.OutputEncoding = Encoding.UTF8;

            const string token = "YOUR_TOKEN_HERE";
            TelegramBotClient client = new TelegramBotClient(token);

            AuctionHouse house = new();

            IMessageSender sender = new TelegramMessageSender(client);
            IAuctionService auctionService = new TelegramAuctionService(house, sender);
            IUserService userService = new TelegramUserService();
            IConversationStateStore state = new InMemoryConversationStateStore();

            PostCommand post = new(state, sender);
            StartCommand start = new(userService, sender);
            ViewCommand view = new(auctionService, sender);
            ViewBalanceCommand viewBalance = new(userService, sender);
            ViewWonCommand viewWon = new(auctionService, sender);

            IEnumerable<ITelegramCommand> commands = new List<ITelegramCommand> { post, start, view, viewBalance, viewWon };

            TelegramUpdateRouter router = new(commands, sender, auctionService, userService, state);
            TelegramBotHost host = new(client, router);

            host.Start();

            Console.WriteLine("Бот работает. Ctrl+C для выхода.");
            await Task.Delay(Timeout.Infinite);
        }
    }
}