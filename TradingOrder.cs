namespace TradingEngine
{
    public class TradingOrder
    {
        public static TradingOrder New(Order order) => new TradingOrder(order);
        public TradingOrder(Order order)
        {
            Order = order;
            SharedUnits = order.Units;
        }

        public Order Order { get; set; }
        public int SharedUnits { get; set; }
        public bool IsSoldOut => SharedUnits <= 0;
        public decimal Price => Order.Price;
        public bool IsBid => Order.IsBid;
        public string OrderId => Order.OrderId;
    }
}
