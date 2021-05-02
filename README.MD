
![build](https://github.com/majicl/trading-engine/actions/workflows/dotnet-core.yml/badge.svg)

# Trading engine:

## Available Functionalities:

- Accept bid (buy) and ask (sell) orders of a specified quantity and price for 1 stock
- Publishes an event when the best bid and ask price changes
- Publishes an event when an order has been accepted (not necessarily matched)
- Publishes an event when a bid order has been settled with a matching ask order, partially or otherwise
- Returns the latest price
- Returns the set of settled orders (some orders may appear multiple times if they are filled by multiple orders)

## Assumptions:
- Bid orders can be settled with one or more than one matching ask order(s)
- Ask orders can be settled with one or more than one matching bid order(s)
- The priority for finding matches is the best price => price comes first even order has to match with more than one.
- Partially filled order happens even when the number of asking units is more than all the available orders units. e.g. we have 3 asks with 10 units for each, in total 30 units are available. if we receive a bid of 40 units. a trade will be made for all 30 units and 10 will be waiting for upcoming asks.
- When the engine is halted, it's still possible to GetPrice and GetTrades. it just makes it read-only!
- When the engine is already halted and you try to halt it again. it does it. another possibility was to return Unsuccess with a reason.
- When the engine is already started and you try to start it again. it does it. another possibility was to return Unsuccess with a reason.
- Checking for changing the best price and publishing the `PriceChanged` event happens after the trade settling process. it could be right after accepting a bid or ask. or even twice

## Issues
There are some issues in the Program file. I've not changed it as you mentioned. but you can see them here:
https://github.com/majicl/trading-engine/issues
