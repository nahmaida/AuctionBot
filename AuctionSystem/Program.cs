using System.Text;

namespace AuctionSystem
{
    internal class Program
    {
        static async Task Main()
        {
            Console.OutputEncoding = Encoding.UTF8;

            AuctionHouse house = new();
            BotHandler botHandler = new(house);
            botHandler.Start();

            Console.WriteLine("Бот работает. Ctrl+C для выхода.");
            await Task.Delay(Timeout.Infinite);
        }
    }
}
