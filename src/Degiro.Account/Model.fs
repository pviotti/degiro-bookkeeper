namespace Degiro

open System

type TxnType =
    | Sell
    | Buy

type ProductType =
    | Shares
    | Etf

type Currency =
    | USD
    | EUR

type Txn =
    { Date: DateTime
      Type: TxnType
      Product: string
      ProductId: string
      ProdType: ProductType
      Quantity: int
      Fees: decimal
      Price: decimal
      Value: decimal
      ValueCurrency: Currency
      OrderId: Guid }

type Earning =
    { Date: DateTime
      Product: string
      Value: decimal
      Percent: decimal }

type Period =
    | Initial = 1
    | Later = 2
    | All = 3
