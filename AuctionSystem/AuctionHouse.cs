namespace AuctionSystem
{
    public class AuctionHouse
    {
        public List<AuctionItem> AuctionItems { get; set; }
        public event Func<AuctionItem, Task>? AuctionEnded;
        private readonly Task _backgroundTimer;
        private readonly ReaderWriterLockSlim _rwl;

        public AuctionHouse()
        {
            AuctionItems = new();
            _rwl = new();
            _backgroundTimer = Task.Run(async () =>
            {
                while (true)
                {
                    // раз в 30 секунд проверяем истекшие аукционы
                    await Task.Delay(TimeSpan.FromSeconds(30));
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