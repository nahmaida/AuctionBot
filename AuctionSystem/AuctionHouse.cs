using Telegram.Bot.Types;

namespace AuctionSystem
{
    public class AuctionHouse
    {
        public List<AuctionItem> AuctionItems { get; set; }
        public event Func<AuctionItem, Task>? AuctionEnded;
        private readonly Task _backgroundTimer;
        private readonly ReaderWriterLockSlim _rwl;
        private readonly int _timerSeconds = 30;

        public AuctionHouse()
        {
            AuctionItems = new();
            _rwl = new();
            _backgroundTimer = Task.Run(async () =>
            {
                while (true)
                {
                    // раз в 30 секунд проверяем истекшие аукционы
                    Console.WriteLine($"[Timer] Жду {_timerSeconds} секунд...");
                    await Task.Delay(TimeSpan.FromSeconds(_timerSeconds));
                    Console.WriteLine("[Timer] Проверяю истёкшие аукционы");
                    await CheckExpiredAuctionsAsync();
                }
            });
        }

        /// <summary>
        /// Добавляет новый лот в аукцион
        /// </summary>
        /// <param name="item">Лот для добавления</param>
        public void AddAuctionItem(AuctionItem item)
        {
            _rwl.EnterWriteLock();
            try
            {
                AuctionItems.Add(item);
            }
            finally
            {
                _rwl.ExitWriteLock();
            }
        }

        /// <summary>
        /// Получаем лоты, выигранные пользователем
        /// </summary>
        /// <param name="id">Id текущего чата</param>
        /// <returns>List<AuctionItem> выигранных лотов</returns>
        public List<AuctionItem> GetWonItems(long id)
        {
            _rwl.EnterReadLock();
            try
            {
                // возвращаем копию
                return AuctionItems.Where(a => !a.IsActive && a.HighestBidder.Id == id && a.Creator.Id != id).ToList();
            }
            finally { _rwl.ExitReadLock(); }
        }

        /// <summary>
        /// Получаем активные лоты
        /// </summary>
        /// <returns>List<AuctionItem> активных лотов</returns>
        public List<AuctionItem> GetActiveItems()
        {
            _rwl.EnterReadLock();
            try
            {
                // возвращаем копию
                return AuctionItems.Where(x => x.IsActive).ToList();
            }
            finally { _rwl.ExitReadLock(); }
        }

        /// <summary>
        /// Проверяет и завершает истекшие аукционы
        /// </summary>
        public async Task CheckExpiredAuctionsAsync()
        {
            List<AuctionItem> expiredItems = new();

            _rwl.EnterReadLock();
            try
            {
                expiredItems = AuctionItems.Where(item => item.IsActive && item.EndTime <= DateTime.Now).ToList();
            }
            finally
            {
                _rwl.ExitReadLock();
            }

            foreach (var item in expiredItems)
            {
                item.EndAuction();

                if (AuctionEnded is not null)
                {
                    await AuctionEnded.Invoke(item);
                }
            }
        }
    }
}