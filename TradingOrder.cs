namespace TradingEngine
{
    public class TradingOrder
    {
        public static TradingOrder New(Order order) => new TradingOrder(order);
        public TradingOrder(Order order)
        {
            Order = order;
            TradableUnits = order.Units;
        }

        public Order Order { get; set; }
        public int TradableUnits { get; set; }
        public bool FullFilled => TradableUnits <= 0;
        public decimal Price => Order.Price;
        public bool IsBid => Order.IsBid;
        public string OrderId => Order.OrderId;
    }
}
