using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Akka.Actor;

namespace TradingEngine
{
    public class Matcher : UntypedActor
    {
        private readonly string _stockId;
        private bool _halted = false;
        private readonly OrderStore _orderStore = new OrderStore();
        private readonly ObservableCollection<TradeSettled> _tradeSettled = new ObservableCollection<TradeSettled>();
        private static void Notify(object @event) => Context.System.EventStream.Publish(@event);

        public Matcher(string stockId)
        {
            _stockId = stockId;
            _tradeSettled.CollectionChanged += _tradeSettled_Changed;
        }
        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Bid bid:
                    HandleBidOrder(bid);
                    break;

                case Ask ask:
                    HandleAskOrder(ask);
                    break;

                case GetPrice getPrice:
                    HandleGetPrice(getPrice);
                    break;

                case Start start:
                    TurnOn(start);
                    break;

                case Halt halt:
                    TurnOff(halt);
                    break;

                case GetTrades getTrades:
                    HandleGetTrades(getTrades);
                    break;
            }
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
            if (order.StockId != _stockId)
            {
                onFailure?.Invoke("StockId doesn't match");
                return;
            }
            if (_halted)
            {
                onFailure?.Invoke($"The Engine is halted for Stock: {_stockId}");
                return;
            }

            var validation = ValidateOrder(order);
            if (!validation.IsValid)
            {
                onFailure?.Invoke(validation.Reason);
                return;
            }

            var isPriceChanging = IsPriceChanging(order);
            _orderStore.Add(order);
            onSuccess?.Invoke();
            NotifyOrderPlaced(order);
            if (isPriceChanging)
            {
                NotifyPriceChanged();
            }

            ResolveMatching(order);
        }

        private void ResolveMatching(Order order)
        {
            if (order.IsBid)
            {
                ResolveBidMatching(order);
            }
            else
            {
                ResolveAskMatching(order);
            }
        }
        private void ResolveBidMatching(Order order)
        {
            var matchingAsks = _orderStore.Asks.Where(a => a.Price <= order.Price);
            foreach (var matchingAsk in matchingAsks)
            {
                var shareToTrade = Math.Min(order.Units, matchingAsk.Units);
                matchingAsk.Units -= shareToTrade;
                order.Units -= shareToTrade;
                var trade = new TradeSettled
                {
                    Price = order.Price,
                    Units = shareToTrade,
                    AskOrderId = matchingAsk.OrderId,
                    StockId = _stockId,
                    BidOrderId = order.OrderId
                };
                _tradeSettled.Add(trade);
            }
        }
        private void ResolveAskMatching(Order order)
        {
            var matchingBids = _orderStore.Bids.Where(a => a.Price >= order.Price);
            foreach (var matchingBid in matchingBids)
            {
                var shareToTrade = Math.Min(order.Units, matchingBid.Units);
                matchingBid.Units -= shareToTrade;
                order.Units -= shareToTrade;
                var trade = new TradeSettled
                {
                    Price = order.Price,
                    Units = shareToTrade,
                    AskOrderId = order.OrderId,
                    StockId = _stockId,
                    BidOrderId = matchingBid.OrderId
                };
                _tradeSettled.Add(trade);
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
                    _orderStore.Single(_=>_.OrderId == ts.AskOrderId),
                    _orderStore.Single(_=>_.OrderId == ts.BidOrderId),
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
        private bool IsPriceChanging(Order order)
        {
            if (order.IsBid)
            {
                return _orderStore.BestBid != order.Price;
            }

            return _orderStore.BestAsk != order.Price;
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

        private static OrderValidationResult ValidateOrder(Order order)
        {
            if (order.Price <= 0 || order.Units <= 0)
            {
                return new OrderValidationResult
                {
                    Reason = $"{order.Units} units with price of {order.Price} is not valid"
                };
            }

            return new OrderValidationResult { IsValid = true };
        }
    }
}