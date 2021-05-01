using System.Collections.Generic;
using System.Linq;

namespace TradingEngine
{
    public class OrderStore : List<Order>
    {
        public IEnumerable<Order> Bids => this.Where(_ => _.IsBid && _.Units > 0);
        public IEnumerable<Order> Asks => this.Where(_ => !_.IsBid && _.Units > 0);
        public decimal? BestBid => Bids.DefaultIfEmpty(null).Max(bid => bid?.Price);
        public decimal? BestAsk => Asks.DefaultIfEmpty(null).Min(ask => ask?.Price);
    }
}
