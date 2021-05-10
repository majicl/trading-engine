using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Akka.Actor;
using Akka.Event;

namespace TradingEngine
{
    public class Matcher : ReceiveActor
    {
        private readonly string _stockId;
        private bool _halted = false;
        private readonly OrderStore _orderStore = new OrderStore();
        private readonly ObservableCollection<TradeSettled> _tradeSettled = new ObservableCollection<TradeSettled>();
        public ILoggingAdapter Log { get; } = Context.GetLogger();
        protected override void PreStart() => Log.Info("Engine started");
        protected override void PostStop() => Log.Info("Engine stopped");

        private static void Notify(object @event) => Context.System.EventStream.Publish(@event);

        public Matcher(string stockId)
        {
            _stockId = stockId;
            _tradeSettled.CollectionChanged += _tradeSettled_Changed;

            Receive<Bid>(HandleBidOrder);
            Receive<Ask>(HandleAskOrder);
            Receive<GetPrice>(HandleGetPrice);
            Receive<Start>(TurnOn);
            Receive<Halt>(TurnOff);
            Receive<GetTrades>(HandleGetTrades);
        }

        private void HandleAskOrder(Ask ask) => HandlerOrder(ask.Order, () =>
        {
            Sender.Tell(new AskResult
            {
                Success = true
            });
        }, (reason) =>
        {
            Sender.Tell(new AskResult
            {
                Reason = reason
            });
        });
        private void HandleBidOrder(Bid bid) => HandlerOrder(bid.Order, () =>
            {
                Sender.Tell(new BidResult
                {
                    Success = true
                });
            }, (reason) =>
            {
                Sender.Tell(new BidResult
                {
                    Reason = reason
                });
            });
        private void HandlerOrder(Order order, Action onSuccess, Action<string> onFailure)
        {
            var validation = ValidateOrder(order);
            if (!validation.IsValid)
            {
                onFailure?.Invoke(validation.Reason);
                return;
            }

            var bestBidPrice = _orderStore.BestBid;
            var bestAskPrice = _orderStore.BestAsk;

            // Accept bid (buy) and ask (sell) orders of a specified quantity and price for 1 stock
            var tradingOrder = TradingOrder.New(order);
            _orderStore.Add(tradingOrder);
            onSuccess?.Invoke();

            // Publishes an event when an order has been accepted (not necessarily matched)
            NotifyOrderPlaced(order);

            // Find if any matched, Publishes an event when a bid order has been settled with a matching ask order, partially or otherwise
            ResolveMatching(tradingOrder);

            // Publishes an event when the best bid and ask price changes
            if (IsPriceChanging(bestBidPrice, bestAskPrice))
            {
                NotifyPriceChanged();
            }
        }

        private void ResolveMatching(TradingOrder tradingOrder)
        {
            if (tradingOrder.IsBid)
            {
                ResolveBidMatching(tradingOrder);
            }
            else
            {
                ResolveAskMatching(tradingOrder);
            }
        }
        private void ResolveBidMatching(TradingOrder tradingOrder)
        {
            var matchingAsks = _orderStore
                .Asks
                .Where(a => a.Price <= tradingOrder.Price)
                .OrderBy(_ => _.Price)
                .ToList(); // best price comes first

            ResolveMatching(tradingOrder, matchingAsks, (matchingAsk, shareToTrade) =>
            {
                var trade = new TradeSettled
                {
                    Price = tradingOrder.Price,
                    Units = shareToTrade,
                    AskOrderId = matchingAsk.OrderId,
                    StockId = _stockId,
                    BidOrderId = tradingOrder.OrderId
                };
                _tradeSettled.Add(trade);
            });
        }
        private void ResolveAskMatching(TradingOrder tradingOrder)
        {
            var matchingBids = _orderStore
                .Bids
                .Where(a => a.Price >= tradingOrder.Price)
                .OrderByDescending(_ => _.Price) // best price comes first
                .ToList();

            ResolveMatching(tradingOrder, matchingBids, (matchingBid, shareToTrade) =>
            {
                var trade = new TradeSettled
                {
                    Price = tradingOrder.Price,
                    Units = shareToTrade,
                    AskOrderId = tradingOrder.OrderId,
                    StockId = _stockId,
                    BidOrderId = matchingBid.OrderId
                };
                _tradeSettled.Add(trade);
            });
        }
        private static void ResolveMatching(TradingOrder tradingOrder, IEnumerable<TradingOrder> matchingOrders, Action<TradingOrder, int> onFound)
        {
            foreach (var matchingBid in matchingOrders)
            {
                if (tradingOrder.FullFilled)
                {
                    break;
                }
                var shareToTrade = Math.Min(tradingOrder.TradableUnits, matchingBid.TradableUnits);
                matchingBid.TradableUnits -= shareToTrade;
                tradingOrder.TradableUnits -= shareToTrade;
                onFound?.Invoke(matchingBid, shareToTrade);
            }
        }

        private void TurnOn(Start start)
        {
            if (start.StockId == _stockId)
            {
                Sender.Tell(new StartResult()
                {
                    Success = true
                });
                _halted = false;
            }
            else
            {
                Sender.Tell(new StartResult()
                {
                    Success = false,
                    Reason = "StockId doesn't match"
                });
            }
        }
        private void TurnOff(Halt halt)
        {
            if (halt.StockId == _stockId)
            {
                Sender.Tell(new HaltResult()
                {
                    Success = true
                });
                _halted = true;
            }
            else
            {
                Sender.Tell(new HaltResult()
                {
                    Success = false,
                    Reason = "StockId doesn't match"
                });
            }
        }

        private void HandleGetTrades(GetTrades query)
        {
            if (query.StockId != _stockId)
            {
                Sender.Tell(new GetTradesResult
                {
                    Reason = "StockId doesn't match"
                });
                return;
            }

            Sender.Tell(new GetTradesResult()
            {
                Success = true,
                Orders = _tradeSettled.SelectMany(ts => new List<Order>()
                {
                    _orderStore.Single(_=>_.OrderId == ts.AskOrderId).Order,
                    _orderStore.Single(_=>_.OrderId == ts.BidOrderId).Order,
                }).ToList()
            });
        }
        private static void _tradeSettled_Changed(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add) return;
            foreach (var newTradeSettled in e.NewItems)
            {
                NotifyTradeSettled(newTradeSettled as TradeSettled);
            }
        }

        private void HandleGetPrice(GetPrice query)
        {
            if (query.StockId != _stockId)
            {
                Sender.Tell(new GetPriceResult
                {
                    Reason = "StockId doesn't match"
                });
                return;
            }

            if (_orderStore.BestAsk.HasValue && _orderStore.BestBid.HasValue)
            {
                Sender.Tell(new GetPriceResult
                {
                    Success = true,
                    Ask = _orderStore.BestAsk,
                    Bid = _orderStore.BestBid,
                });
            }
            else
            {
                Sender.Tell(new GetPriceResult
                {
                    Reason = "Price is not available at the moment"
                });
            }
        }

        private bool IsPriceChanging(decimal? oldBestBidPrice, decimal? oldBestAskPrice)
        {
            return _orderStore.BestBid != oldBestBidPrice ||
                _orderStore.BestAsk != oldBestAskPrice;
        }

        private void NotifyPriceChanged() => Notify(new PriceChanged
        {
            StockId = _stockId,
            Ask = _orderStore.BestAsk,
            Bid = _orderStore.BestBid
        });
        private static void NotifyOrderPlaced(Order order) => Notify(new OrderPlaced()
        {
            Order = order
        });
        private static void NotifyTradeSettled(TradeSettled trade) => Notify(trade);

        private OrderValidationResult ValidateOrder(Order order)
        {
            if (_halted)
            {
                return new OrderValidationResult
                {
                    Reason = $"The Engine is halted for Stock: {_stockId}"
                };
            }
            if (order.StockId != _stockId)
            {
                return new OrderValidationResult
                {
                    Reason = "StockId doesn't match"
                };
            }
            if (order.Price <= 0 || order.Units <= 0)
            {
                return new OrderValidationResult
                {
                    Reason = $"Ordering {order.Units} units with price of {order.Price} for {order.StockId} is not valid."
                };
            }

            return new OrderValidationResult { IsValid = true };
        }
    }
}