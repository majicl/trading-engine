using System.Collections.Generic;
using System.Linq;

namespace TradingEngine
{
    public class OrderStore : List<TradingOrder>
    {
        //Todo: Needs to move
        public void Add(Order order) => this.Add(new TradingOrder(order));
        private IEnumerable<TradingOrder> AvailableOrders => this.Where(_ => !_.IsSoldOut);
        public IEnumerable<TradingOrder> Bids => AvailableOrders.Where(_ => _.IsBid);
        public IEnumerable<TradingOrder> Asks => AvailableOrders.Where(_ => !_.IsBid);
        public decimal? BestBid => Bids.DefaultIfEmpty(null).Max(bid => bid?.Price);
        public decimal? BestAsk => Asks.DefaultIfEmpty(null).Min(ask => ask?.Price);
    }
}
