using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Dsl;
using Akka.TestKit.Xunit2;
using Xunit;

namespace TradingEngine
{
    public class MatcherTest : TestKit
    {
        private readonly IActorRef _matcher;
        private const string StockId = "MSFT";

        private static string NewId() => Guid.NewGuid().ToString();
        private static Ask NewAsk(int units, decimal price) => Ask.New(NewId(), StockId, units: units, price: price);
        private static Bid NewBid(int units, decimal price) => Bid.New(NewId(), StockId, units: units, price: price);

        public MatcherTest()
        {
            _matcher = Sys.ActorOf(Props.Create(() => new Matcher(StockId)));
        }

        [Fact]
        [Description("Accept ask (sell) orders of a specified quantity and price for 1 stock")]
        public void Valid_Ask_Order_Should_Place()
        {
            //arrange
            var ask = NewAsk(units: 50, price: 99.00m);

            //act
            _matcher.Tell(ask);

            //assert
            ExpectMsg<AskResult>(r => Assert.True(r.Success, r.Reason));
        }

        [Fact]
        [Description("Doesn't accept orders with wrong stockId")]
        public void Valid_Ask_With_Wrong_StockID_Order_Should_NOT_Place()
        {
            //arrange
            var ask = Ask.New(NewId(), "Wrong", units: 4, price: 100);

            //act
            _matcher.Tell(ask);

            //assert
            var askResult = ExpectMsg<AskResult>(r => Assert.False(r.Success, r.Reason));
            Assert.Equal("StockId doesn't match", askResult.Reason);
        }

        [Fact]
        [Description("Accept bid (buy) orders of a specified quantity and price for 1 stock")]
        public void Valid_Bid_Order_Should_Place()
        {
            //arrange
            var bid = NewBid(units: 50, price: 99.00m);

            //act
            _matcher.Tell(bid);

            //assert
            ExpectMsg<BidResult>(r => Assert.True(r.Success, r.Reason));
        }

        [InlineData(0, 99.00)]
        [InlineData(-1, 99.00)]
        [InlineData(1, 0)]
        [InlineData(1, -1)]
        [InlineData(0, 0)]
        [InlineData(-1, -1)]
        [Theory]
        [Description("Don't accept invalid ask (sell) orders of a specified quantity and price for 1 stock")]
        public void Invalid_Ask_Orders_Should_NOT_Place(int units, decimal price)
        {
            //arrange
            var ask = NewAsk(units: units, price: price);

            //act
            _matcher.Tell(ask);

            //assert
            ExpectMsg<AskResult>(r => Assert.False(r.Success, r.Reason));
        }

        [InlineData(0, 99.00)]
        [InlineData(-1, 99.00)]
        [InlineData(1, 0)]
        [InlineData(1, -1)]
        [InlineData(0, 0)]
        [InlineData(-1, -1)]
        [Theory]
        [Description("Don't accept invalid bid (buy) orders of a specified quantity and price for 1 stock")]
        public void Invalid_Bid_orders_should_NOT_place(int units, decimal price)
        {
            //arrange
            var bid = NewBid(units: units, price: price);

            //act
            _matcher.Tell(bid);

            //assert
            ExpectMsg<BidResult>(r => Assert.False(r.Success, r.Reason));
        }

        [Fact]
        [Description("Publishes an event when an order has been accepted (not necessarily matched)")]
        public void Accept_Ask_Orders_Should_Publish_Event()
        {
            //arrange
            OrderPlaced orderPlaced = null;
            SetupSubscribe<OrderPlaced>((op) => orderPlaced = op);
            var ask = NewAsk(units: 10, price: 99.99m);

            //act
            _matcher.Tell(ask);
            Thread.Sleep(500);

            //assert
            Assert.NotNull(orderPlaced);
            Assert.Equal(ask.Order, orderPlaced.Order);
        }

        [Fact]
        [Description("The engine is not halted with wrong stockId")]
        public void Wrong_StockId_Does_NOT_Make_Engine_Halted()
        {
            //act
            _matcher.Tell(new Halt() { StockId = "WrongOne" });

            //assert
            var haltResult = ExpectMsg<HaltResult>(r => Assert.False(r.Success, r.Reason));
            Assert.Equal("StockId doesn't match", haltResult.Reason);
        }

        [Fact]
        [Description("The engine is not started with wrong stockId")]
        public void Wrong_StockId_Does_NOT_Make_Engine_Started()
        {
            //act
            _matcher.Tell(new Start() { StockId = "WrongOne" });

            //assert
            var haltResult = ExpectMsg<StartResult>(r => Assert.False(r.Success, r.Reason));
            Assert.Equal("StockId doesn't match", haltResult.Reason);
        }

        [Fact]
        [Description("Halted engine does NOT accept any order")]
        public void Valid_Ask_Order_Should_NOT_Place_If_Halted()
        {
            //arrange
            _matcher.Tell(new Halt() { StockId = StockId });
            var ask = NewAsk(units: 50, price: 99.00m);

            //act
            _matcher.Tell(ask);

            //assert
            ExpectMsg<HaltResult>(r => Assert.True(r.Success, r.Reason));
            var askResult = ExpectMsg<AskResult>(r => Assert.False(r.Success, r.Reason));
            Assert.Equal($"The Engine is halted for Stock: {StockId}", askResult.Reason);
        }

        [Fact]
        [Description("Halted engine accepts orders after Starting the engine")]
        public void Valid_Orders_Should_Place_After_Starting_Engine()
        {
            //arrange
            _matcher.Tell(new Halt() { StockId = StockId });
            var ask = NewAsk(units: 50, price: 99.00m);

            //act
            _matcher.Tell(ask);

            //assert
            ExpectMsg<HaltResult>();
            var askResult = ExpectMsg<AskResult>(r => Assert.False(r.Success, r.Reason));
            Assert.Equal($"The Engine is halted for Stock: {StockId}", askResult.Reason);

            //arrange
            _matcher.Tell(new Start() { StockId = StockId });
            var ask2 = NewAsk(units: 50, price: 99.00m);

            //act
            _matcher.Tell(ask2);

            //assert
            ExpectMsg<StartResult>(r => Assert.True(r.Success, r.Reason));
            ExpectMsg<AskResult>(r => Assert.True(r.Success, r.Reason));
        }

        [Fact]
        [Description("Publishes an event when an order has been accepted (not necessarily matched)")]
        public void Accept_Bid_Orders_Should_Publish_Event()
        {
            //arrange
            OrderPlaced orderPlaced = null;
            SetupSubscribe<OrderPlaced>((op) => orderPlaced = op);
            var bid = NewBid(units: 10, price: 99.99m);

            //act
            _matcher.Tell(bid);
            Thread.Sleep(500);

            //assert
            Assert.NotNull(orderPlaced);
            Assert.Equal(bid.Order, orderPlaced.Order);
        }

        [Fact]
        [Description("Publishes an event when the best bid price changes")]
        public void Change_Bid_Price_To_The_Best_Should_Publish_Event()
        {
            //arrange
            var bid = NewBid(units: 10, price: 99.98m);
            PriceChanged priceChanged = null;
            SetupSubscribe<PriceChanged>((pc) => priceChanged = pc);

            //act
            _matcher.Tell(bid);
            Thread.Sleep(500);

            //assert
            Assert.NotNull(priceChanged);
            Assert.Equal(99.98m, priceChanged.Bid);

            //arrange
            var bid2 = NewBid(units: 10, price: 99.99m);

            //act
            _matcher.Tell(bid2);
            Thread.Sleep(500);

            //assert
            Assert.Equal(99.99m, priceChanged.Bid);
        }

        [Fact]
        [Description("Price changes is based on the available share price")]
        public void Making_Trades_Affect_Best_Prices()
        {
            //arrange
            var bid = NewBid(units: 10, price: 99.98m);
            PriceChanged priceChanged = null;
            SetupSubscribe<PriceChanged>((pc) => priceChanged = pc);

            //act
            _matcher.Tell(bid);
            Thread.Sleep(500);

            //assert
            Assert.NotNull(priceChanged);
            Assert.Equal(99.98m, priceChanged.Bid);

            //arrange
            var bid2 = NewBid(units: 10, price: 99.99m);

            //act
            _matcher.Tell(bid2);
            Thread.Sleep(500);

            //assert
            Assert.Equal(99.99m, priceChanged.Bid);

            //arrange
            var ask = NewAsk(units: 10, price: 99.99m);

            //act
            _matcher.Tell(ask);
            Thread.Sleep(500);

            //assert
            Assert.Equal(99.98m, priceChanged.Bid);
            Assert.Null(priceChanged.Ask);
        }

        [Theory]
        [InlineData(99.99)] // max is 99.99
        [InlineData(99.98)]
        [InlineData(0)]
        [InlineData(-1)]
        [Description("Publishes an event when the best bid price changes")]
        public void Change_Bid_Price_NOT_To_The_Best_Should_NOT_Publish_Event(decimal price)
        {
            //arrange
            PriceChanged priceChanged = null;
            SetupSubscribe<PriceChanged>((pc) => priceChanged = pc);
            var bid = NewBid(units: 10, price: 99.99m);

            //act
            _matcher.Tell(bid);
            Thread.Sleep(500);

            //assert
            Assert.Equal(99.99m, priceChanged.Bid);
            //arrange
            var bid2 = NewBid(units: 10, price: price);

            //act
            _matcher.Tell(bid2);
            Thread.Sleep(500);

            //assert
            Assert.Equal(99.99m, priceChanged.Bid);
        }

        [Fact]
        [Description("Publishes an event when the best ask price changes")]
        public void Change_Ask_Price_To_The_Best_Should_Publish_Event()
        {
            //arrange
            PriceChanged priceChanged = null;
            SetupSubscribe<PriceChanged>((pc) => priceChanged = pc);
            var ask = NewAsk(units: 10, price: 99.99m);

            //act
            _matcher.Tell(ask);
            Thread.Sleep(500);

            //assert
            Assert.NotNull(priceChanged);
            Assert.Equal(99.99m, priceChanged.Ask);

            //arrange
            var ask2 = NewAsk(units: 10, price: 99.98m);

            //act
            _matcher.Tell(ask2);
            Thread.Sleep(500);

            //assert
            Assert.Equal(99.98m, priceChanged.Ask);
        }

        [Theory]
        [InlineData(99.99)] // max is 99.99
        [InlineData(100.00)]
        [InlineData(0)]
        [InlineData(-1)]
        [Description("Publishes an event when the best ask price changes")]
        public void Change_Ask_Price_NOT_To_The_Best_Should_NOT_Publish_Event(decimal price)
        {
            //arrange
            PriceChanged priceChanged = null;
            SetupSubscribe<PriceChanged>((pc) => priceChanged = pc);
            var ask = NewAsk(units: 10, price: 99.99m);

            //act
            _matcher.Tell(ask);
            Thread.Sleep(500);

            //assert
            Assert.NotNull(priceChanged);
            Assert.Equal(99.99m, priceChanged.Ask);

            //arrange
            var ask2 = NewAsk(units: 10, price: price);

            //act
            _matcher.Tell(ask2);
            Thread.Sleep(500);

            //assert
            Assert.Equal(99.99m, priceChanged.Ask);
        }

        [Fact]
        [Description("Returns the latest price")]
        public async Task Latest_Price_Returns_ExpectedData()
        {
            //arrange
            var bid = NewBid(units: 10, price: 100.00m);
            var ask = NewAsk(units: 10, price: 110.00m);

            //act
            _matcher.Tell(bid);
            _matcher.Tell(ask);

            var message = new GetPrice() { StockId = StockId };
            var result = await _matcher.Ask<GetPriceResult>(message);

            //assert
            Assert.Equal(100.00m, result.Bid);
            Assert.Equal(110.00m, result.Ask);
            Assert.True(result.Success);
            Assert.Null(result.Reason);
        }

        [Theory]
        [InlineData(0, 100.0, 1, 101.0)]
        [InlineData(1, 100.0, 0, 101.0)]
        [InlineData(1, 0, 1, 101.0)]
        [InlineData(1, 100.0, 1, 00.0)]
        [Description("Doesn't return the latest price")]
        public async Task Latest_Price_Returns_Null_Values_With_Reason(int bidUnits, decimal bidPrice, int askUnits, decimal askPrice)
        {
            //arrange
            var bid = NewBid(units: bidUnits, price: bidPrice);
            var ask = NewAsk(units: askUnits, price: askPrice);

            //act
            _matcher.Tell(bid);
            _matcher.Tell(ask);

            var message = new GetPrice() { StockId = StockId };
            var result = await _matcher.Ask<GetPriceResult>(message);

            //assert
            Assert.Null(result.Bid);
            Assert.Null(result.Ask);
            Assert.False(result.Success);
            Assert.Equal("Price is not available at the moment", result.Reason);
        }

        [Fact]
        [Description("Publishes an event when a bid order has been settled with a matching ask order, partially or otherwise")]
        public void Settle_Bid_Order_Should_Publish_Event()
        {
            //arrange
            TradeSettled tradeSettled = null;
            SetupSubscribe<TradeSettled>((ts) => tradeSettled = ts);
            var bid = NewBid(units: 10, price: 99.99m);
            var ask = NewAsk(units: 10, price: 99.99m);

            //act
            _matcher.Tell(bid);
            _matcher.Tell(ask);
            Thread.Sleep(2000);

            //assert
            Assert.NotNull(tradeSettled);
            Assert.Equal(99.99m, tradeSettled.Price);
            Assert.Equal(10, tradeSettled.Units);
            Assert.Equal(ask.Order.OrderId, tradeSettled.AskOrderId);
            Assert.Equal(bid.Order.OrderId, tradeSettled.BidOrderId);
        }

        [Fact]
        [Description("Does NOT add trades when order units get zero")]
        public async Task Settle_Bid_Order_Should_NOT_Add_Trade()
        {
            //arrange
            var ask = NewAsk(units: 10, price: 99.99m);
            var ask2 = NewAsk(units: 10, price: 99.99m);
            var bid = NewBid(units: 10, price: 99.99m);

            //act
            _matcher.Tell(ask);
            _matcher.Tell(ask2);
            _matcher.Tell(bid);
            var message = new GetTrades() { StockId = StockId };
            var result = await _matcher.Ask<GetTradesResult>(message);

            //assert
            Assert.Equal(2, result.Orders.Count);
            Assert.Equal(ask.Order, result.Orders.First());
            Assert.Equal(bid.Order, result.Orders.Last());
        }

        [Fact]
        [Description("Order history should be available after matchings")]
        public async Task Orders_History_Should_Be_Available_After_Matching()
        {
            //arrange
            var ask = NewAsk(units: 10, price: 99.99m);
            var bid = NewBid(units: 4, price: 99.99m);
            var bid2 = NewBid(units: 3, price: 99.99m);

            //act
            _matcher.Tell(ask);
            _matcher.Tell(bid);
            _matcher.Tell(bid2);
            var message = new GetTrades() { StockId = StockId };
            var result = await _matcher.Ask<GetTradesResult>(message);

            //assert
            Assert.Equal(4, result.Orders.Count);
            Assert.Equal(10, ask.Order.Units);
            Assert.Equal(4, bid.Order.Units);
            Assert.Equal(3, bid2.Order.Units);

            Assert.Equal(10, result.Orders.First().Units);
            Assert.Equal(4, result.Orders[1].Units);
            Assert.Equal(10, result.Orders[2].Units);
            Assert.Equal(3, result.Orders.Last().Units);
        }

        [Fact]
        [Description("Returns the set of settled orders (some orders may appear multiple times if they are filled by multiple orders)")]
        public async Task Set_Of_Settled_Orders_Returns_ExpectedData()
        {
            //arrange
            var bid = NewBid(units: 10, price: 110.00m);
            var ask = NewAsk(units: 10, price: 110.00m);

            //act
            _matcher.Tell(bid);
            _matcher.Tell(ask);
            var message = new GetTrades() { StockId = StockId };
            var result = await _matcher.Ask<GetTradesResult>(message);

            //assert
            Assert.NotEmpty(result.Orders);
            Assert.Equal(ask.Order, result.Orders.First());
            Assert.Equal(bid.Order, result.Orders.Last());
        }

        [Fact]
        [Description("Doesn't return the set of settled orders with wrong stockId")]
        public async Task Set_Of_Settled_Orders_Does_NOT_Return_ExpectedData()
        {
            //arrange
            var bid = NewBid(units: 10, price: 110.00m);
            var ask = NewAsk(units: 10, price: 110.00m);

            //act
            _matcher.Tell(bid);
            _matcher.Tell(ask);
            var message = new GetTrades() { StockId = "WrongOne" };
            var result = await _matcher.Ask<GetTradesResult>(message);

            //assert
            Assert.Null(result.Orders);
            Assert.Equal("StockId doesn't match", result.Reason);
        }

        private void SetupSubscribe<T>(Action<T> onReceived)
        {
            var handler = new Action<T, object>((evt, ctx) => onReceived?.Invoke(evt));
            var actorReference = Sys.ActorOf(cfg => cfg.Receive(handler));
            Sys.EventStream.Subscribe(actorReference, typeof(T));
        }
    }
}
