namespace AuctionSystem
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            AuctionHouse house = new AuctionHouse();
            BotHandler botHandler = new BotHandler(house);
            botHandler.Start();

            Console.WriteLine("Бот работает. Ctrl+C для выхода.");
            await Task.Delay(Timeout.Infinite);
        }
    }
}
