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