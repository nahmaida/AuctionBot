using AuctionSystem.Domain;
using AuctionSystem.Models;

namespace AuctionSystem.Infrastructure.Telegram
{
    public class TelegramAuctionService: IAuctionService
    {
        private readonly AuctionHouse House;
        private readonly IMessageSender sender;
        private readonly object _lock = new();
        private readonly ReaderWriterLockSlim _rwl = new();

        public TelegramAuctionService(AuctionHouse auctionHouse, IMessageSender messageSender)
        {
            House = auctionHouse;
            sender = messageSender;
        }

        public bool TryGetItem(Guid itemId, out AuctionItem? auctionItem)
        {
            lock (_lock)
                auctionItem = House.GetActiveItems().FirstOrDefault(item => item.Id == itemId);
            return auctionItem != null;
        }

        public List<AuctionItem> GetActiveItems()
        {
            return House.GetActiveItems();
        }

        public List<AuctionItem> GetWonItems(long chatId)
        {
            return House.GetWonItems(chatId);
        }

        public void AddItem(AuctionItem item)
        {
            House.AddAuctionItem(item);
        }

        /// <summary>
        /// Уведомляем победителя лота
        /// </summary>
        /// <param name="item">Завершенный лот</param>
        public async Task EndAuctionAsync(AuctionItem item)
        {
            _rwl.EnterWriteLock();
            try
            {
                item.EndAuction();

                UserAccount winner = item.HighestBidder;
                // Уведомляем создателя
                await sender.SendMessage(
                    chatId: item.Creator.Id,
                    message: $"🎉Аукцион по вашему лоту <b>\"{item.Name}\"</b> завершен!\n\n<b>Итоговая цена:</b> {item.CurrentPrice}₽\n<b>Победитель:</b> @{winner.Username}"
                );

                // Уведомляем победителя, если он не создатель
                if (winner != null && winner.Id != item.Creator.Id)
                {
                    await sender.SendMessage(
                        chatId: item.HighestBidder.Id,
                        message: $"🎉Вы выиграли аукцион по лоту <b>\"{item.Name}\"</b> за <b>{item.CurrentPrice}₽</b>\nСвяжитесь с владельцем: <b>@{item.Creator.Username}</b>"
                    );
                }
            }
            finally
            {
                _rwl.ExitWriteLock();
            }
        }
    }
}
