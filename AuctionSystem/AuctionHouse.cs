namespace AuctionSystem
{
    public class AuctionHouse
    {
        public List<AuctionItem> AuctionItems { get; set; }
        private readonly ReaderWriterLockSlim _rwl;

        public AuctionHouse()
        {
            AuctionItems = new List<AuctionItem>();
            _rwl = new ReaderWriterLockSlim();
        }

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
    }
}