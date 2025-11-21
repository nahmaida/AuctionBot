using System.Text;

namespace AuctionSystem
{
    internal class Program
    {
        private static async Task Main()
        {
            // Т.к по умолчанию не выводит кирилицу
            Console.OutputEncoding = Encoding.UTF8;

            AuctionHouse house = new();
            BotHandler botHandler = new(house);
            botHandler.Start();

            Console.WriteLine("Бот работает. Ctrl+C для выхода.");
            await Task.Delay(Timeout.Infinite);
        }
    }
}