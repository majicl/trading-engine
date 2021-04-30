using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

namespace TradingEngine
{
    public class Matcher : UntypedActor
    {
        private readonly string _stockId;
        private bool _halted = false;
        private readonly OrderStore _orderStore = new OrderStore();
        private readonly ICollection<TradeSettled> _tradeSettled = new List<TradeSettled>();
        private static void Notify(object @event) => Context.System.EventStream.Publish(@event);

        public Matcher(string stockId)
        {
            _stockId = stockId;
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

                case GetPrice:
                    HandleGetPrice();
                    break;

                case Start:
                    TurnOn();
                    break;

                case Halt:
                    TurnOff();
                    break;

                case GetTrades:
                    HandleGetTrades();
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
            if (_halted)
            {
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
        }

        private void TurnOn()
        {
            Sender.Tell(new StartResult()
            {
                Success = true,
                Reason = "The engine has been started"
            });
            _halted = false;
        }

        private void TurnOff()
        {
            Sender.Tell(new HaltResult()
            {
                Success = true,
                Reason = "The engine has been halted"
            });
            _halted = true;
        }

        private void HandleGetTrades()
        {
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

        public bool Tradable => _orderStore.BestAsk.HasValue && _orderStore.BestBid.HasValue;
        private void HandleGetPrice()
        {
            if (Tradable)
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