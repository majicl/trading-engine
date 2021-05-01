using System.Collections.Generic;
using System.Linq;

namespace TradingEngine
{
    public class OrderStore : List<TradingOrder>
    {
        private IEnumerable<TradingOrder> AvailableOrders => this.Where(_ => !_.FullFilled);
        public IEnumerable<TradingOrder> Bids => AvailableOrders.Where(_ => _.IsBid);
        public IEnumerable<TradingOrder> Asks => AvailableOrders.Where(_ => !_.IsBid);
        public decimal? BestBid => Bids.DefaultIfEmpty(null).Max(bid => bid?.Price);
        public decimal? BestAsk => Asks.DefaultIfEmpty(null).Min(ask => ask?.Price);
    }
}
