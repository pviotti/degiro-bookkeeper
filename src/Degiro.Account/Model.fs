namespace Degiro

open System
open Microsoft.FSharp.Reflection

module Utils =
    let toString (x: 'a) =
        match FSharpValue.GetUnionFields(x, typeof<'a>) with
        | case, _ -> case.Name

    // Create discriminated unions from string - http://fssnip.net/9l
    let fromString<'a> (s: string) =
        match FSharpType.GetUnionCases typeof<'a>
              |> Array.filter (fun case -> case.Name = s)
            with
        | [| case |] -> FSharpValue.MakeUnion(case, [||]) :?> 'a
        | _ -> failwith (s + " not recognized as a valid parameter.")

type ProductType =
    | Shares
    | ETF

type TxnType =
    | Sell
    | Buy

    override this.ToString() = Utils.toString this
    static member FromString s = Utils.fromString<TxnType> s

type Currency =
    | USD
    | EUR
    | CAD

    override this.ToString() = Utils.toString this
    static member FromString s = Utils.fromString<Currency> s

type Txn =
    { Date: DateTime
      Type: TxnType
      Product: string
      ISIN: string // International Security Identification Number
      ProdType: ProductType
      Quantity: int
      Fees: decimal
      Price: decimal // Total value of the transaction (always in €)
      Value: decimal // Unit price of the stock (in any currency as per Currency)
      ValueCurrency: Currency
      OrderId: Guid }

type Earning =
    { Date: DateTime
      Product: string
      ISIN: string
      ProdType: ProductType
      Value: decimal
      Percent: decimal }

type Dividend =
    { Year: int
      Product: string
      ISIN: string
      //ProdType: ProductType -- can't tell product type from a dividend row (without looking up ISIN)
      Value: decimal
      ValueTax: decimal
      Currency: Currency }

/// Irish CGT period
type Period =
    | Initial = 1
    | Later = 2
    | All = 3

/// Models stock splits, ISIN or name changes
type StockChange =
    { Date: DateTime
      IsinBefore: string
      IsinAfter: string
      ProductBefore: string
      ProductAfter: string
      Multiplier: int }