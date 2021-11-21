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
              |> Array.filter (fun case -> case.Name = s) with
        | [| case |] -> FSharpValue.MakeUnion(case, [||]) :?> 'a
        | _ -> failwith (s + " not recognized as a valid parameter.")

type ProductType =
    | Shares
    | Etf

type TxnType =
    | Sell
    | Buy
    override this.ToString() = Utils.toString this
    static member FromString s = Utils.fromString<TxnType> s

type Currency =
    | USD
    | EUR
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
      Price: decimal
      Value: decimal
      ValueCurrency: Currency
      OrderId: Guid }

type Earning =
    { Date: DateTime
      Product: string
      ProductId: string
      Value: decimal
      Percent: decimal }

type Dividend =
    { Year: int
      Product: string
      ProductId: string
      Value: decimal
      ValueTax: decimal
      Currency: Currency }

type Period =
    | Initial = 1
    | Later = 2
    | All = 3
