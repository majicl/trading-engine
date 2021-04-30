using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TradingEngine
{
    public class OrderStore : ObservableCollection<Order>
    {
        public OrderStore()
        {
           
        }

        private void OrderStore_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        public List<Order> Bids => this.Where(_ => _.IsBid).ToList();
        public IEnumerable<Order> Asks => this.Where(_ => !_.IsBid);
        public decimal? BestBid => Bids.DefaultIfEmpty(null).Max(bid => bid?.Price);
        public decimal? BestAsk => Asks.DefaultIfEmpty(null).Min(ask => ask?.Price);
    }
}
