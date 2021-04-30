using System;
using System.ComponentModel;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using Xunit;

namespace TradingEngine
{
    public class MatcherTest : TestKit
    {
        private readonly IActorRef _matcher;

        public MatcherTest()
        {
            _matcher = Sys.ActorOf(Props.Create(() => new Matcher("MSFT")));
        }

        [Fact]
        [Description("Accept ask (sell) orders of a specified quantity and price for 1 stock")]
        public void Valid_Ask_Order_Should_Place()
        {
            _matcher.Tell(Ask.New(Guid.NewGuid().ToString(), "MSFT", units: 50, price: 99.00m));
            ExpectMsg<AskResult>(r => Assert.True(r.Success, r.Reason));
        }

        [Fact]
        [Description("Accept bid (buy) orders of a specified quantity and price for 1 stock")]
        public void Valid_Bid_Order_Should_Place()
        {
            _matcher.Tell(Bid.New(Guid.NewGuid().ToString(), "MSFT", units: 50, price: 99.00m));
            ExpectMsg<AskResult>(r => Assert.True(r.Success, r.Reason));
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
          
        }

        [Fact]
        [Description("Publishes an event when the best bid price changes")]
        public void Best_Bid_Price_Should_Publish_Event()
        {

        }

        [Fact]
        [Description("Publishes an event when the best ask price changes")]
        public void Best_Ask_Price_Should_Publish_Event()
        {

        }

        [Fact]
        [Description("Publishes an event when an order has been accepted (not necessarily matched)")]
        public void Accept_Orders_Should_Publish_Event()
        {

        }

        [Fact]
        [Description("Publishes an event when a bid order has been settled with a matching ask order, partially or otherwise")]
        public void settle_Bid_Order_Should_Publish_Event()
        {

        }

        [Fact]
        [Description("Returns the latest price")]
        public void Latest_Price_Returns_ExpectedData()
        {

        }

        [Fact]
        [Description("Returns the set of settled orders (some orders may appear multiple times if they are filled by multiple orders)")]
        public void Set_Of_Settled_Orders_Returns_ExpectedData()
        {

        }
    }
}
