using AuctionSystem.Application;
using AuctionSystem.Domain;
using AuctionSystem.Infrastructure.Telegram;
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

            TelegramUpdateRouter router = new(sender, auctionService, userService, state);
            TelegramBotHost host = new(client, router);

            host.Start();

            Console.WriteLine("Бот работает. Ctrl+C для выхода.");
            await Task.Delay(Timeout.Infinite);
        }
    }
}