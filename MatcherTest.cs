﻿using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Akka.Actor;
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
        [Description("Publishes an event when the best bid price changes")]
        public void Change_Bid_Price_To_The_Best_Should_Publish_Event()
        { 
            //arrange
            var bid = NewBid(units: 10, price: 99.98m);

            //act
            _matcher.Tell(bid);

            //assert
            ExpectMsg<PriceChanged>(msg => msg.Bid == 99.98m);

            //arrange
            var bid2 = NewBid(units: 10, price: 99.99m);

            //act
            _matcher.Tell(bid2);

            //assert
            ExpectMsg<PriceChanged>(msg => msg.Bid == 99.99m);
        }

        [Fact]
        [Description("Publishes an event when the best bid price changes")]
        public void Change_Bid_Price_NOT_To_The_Best_Should_NOT_Publish_Event()
        { //arrange
            var bid = NewBid(units: 10, price: 99.99m);

            //act
            _matcher.Tell(bid);

            //assert
            ExpectMsg<PriceChanged>(msg => msg.Bid == 99.99m);

            //arrange
            var bid2 = NewBid(units: 10, price: 99.99m);

            //act
            _matcher.Tell(bid2);

            //assert
            ExpectNoMsg();
        }

        [Fact]
        [Description("Publishes an event when the best ask price changes")]
        public void Change_Ask_Price_To_The_Best_Should_Publish_Event()
        {
            //arrange
            var ask = NewAsk(units: 10, price: 99.99m);

            //act
            _matcher.Tell(ask);

            //assert
            ExpectMsg<PriceChanged>(msg => msg.Ask == 99.99m);

            //arrange
            var ask2 = NewAsk(units: 10, price: 99.98m);

            //act
            _matcher.Tell(ask2);

            //assert
            ExpectMsg<PriceChanged>(msg => msg.Ask == 99.98m);
        }

        [Fact]
        [Description("Publishes an event when the best ask price changes")]
        public void Change_Ask_Price_NOT_To_The_Best_Should_NOT_Publish_Event()
        {
            //arrange
            var ask = NewAsk(units: 10, price: 99.99m);

            //act
            _matcher.Tell(ask);

            //assert
            ExpectMsg<PriceChanged>(msg => msg.Ask == 99.99m);

            //arrange
            var ask2 = NewAsk(units: 10, price: 99.98m);

            //act
            _matcher.Tell(ask2);

            //assert
            ExpectNoMsg();
        }

        [Fact]
        [Description("Publishes an event when an order has been accepted (not necessarily matched)")]
        public void Accept_Ask_Orders_Should_Publish_Event()
        {
            //arrange
            var ask = NewAsk(units: 10, price: 99.99m);

            //act
            _matcher.Tell(ask);

            //assert
            ExpectMsg<OrderPlaced>(msg => msg.Order.OrderId == ask.Order.OrderId);
        }

        [Fact]
        [Description("Publishes an event when an order has been accepted (not necessarily matched)")]
        public void Accept_Bid_Orders_Should_Publish_Event()
        {
            //arrange
            var bid = NewBid(units: 10, price: 99.99m);

            //act
            _matcher.Tell(bid);

            //assert
            ExpectMsg<OrderPlaced>(msg => msg.Order.OrderId == bid.Order.OrderId);
        }

        [Fact]
        [Description("Publishes an event when a bid order has been settled with a matching ask order, partially or otherwise")]
        public void Settle_Bid_Order_Should_Publish_Event()
        {
            //arrange
            var bid = NewBid(units: 10, price: 99.99m);

            //act
            _matcher.Tell(bid);

            //assert
            ExpectMsg<BidResult>(msg => msg.Success);
        }

        [Fact]
        [Description("Returns the latest price")]
        public async Task Latest_Price_Returns_ExpectedData()
        {
            //arrange
            var bid = NewBid(units: 10, price: 110.00m);
            var ask = NewAsk(units: 10, price: 100.00m);

            //act
            _matcher.Tell(bid);
            _matcher.Tell(ask);

            var message = new GetPrice();
            var result = await _matcher.Ask<GetPriceResult>(message);

            //assert
            Assert.Equal(110.00m, result.Bid);
            Assert.Equal(100.00m, result.Ask);
        }

        [Fact]
        [Description("Returns the set of settled orders (some orders may appear multiple times if they are filled by multiple orders)")]
        public async Task Set_Of_Settled_Orders_Returns_ExpectedData()
        {
            //arrange
            var bid = NewBid(units: 10, price: 110.00m);
            var ask = NewAsk(units: 10, price: 100.00m);

            //act
            _matcher.Tell(bid);
            _matcher.Tell(ask);
            var message = new GetTrades();
            var result = await _matcher.Ask<GetTradesResult>(message);

            //assert
            Assert.NotEmpty(result.Orders);
        }
    }
}
