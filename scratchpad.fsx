#r "nuget: FSharp.Data"

open FSharp.Data
open System
open System.Text.RegularExpressions


// change printed format of specific types in fsi
fsi.AddPrinter<DateTime>(fun d -> d.ToShortDateString())
fsi.AddPrinter<TimeSpan>(fun time -> time.ToString("c"))

// Entities
type TxnType =
    | Sell
    | Buy

type Currency =
    | USD
    | EUR

type ProductType =
    | Shares
    | Etf

type Txn =
    { Date: DateTime
      Type: TxnType
      Product: string
      ProductId: string
      ProdType: ProductType
      Quantity: int
      Fees: float
      Price: float
      Value: float
      ValueCurrency: Currency
      OrderId: Guid }

[<Literal>]
let CsvFilePath = __SOURCE_DIRECTORY__ + "/account.csv"

// Culture is used to automatically parse dates in the dd-mm-YYY format as `Option<DateTime>` type
type Account = CsvProvider<CsvFilePath, Schema=",,,,,,,,Price,,,OrderId", Culture="en-IRL">
let account: Account = Account.Load(CsvFilePath)

//let firstRow : Account.Row = account.Rows |> Seq.head
//firstRow.Date

// Utility functions
let dateToString (date: Option<DateTime>) =
    match date with
    | Some (x) -> x.ToString "yyyy-MM-dd"
    | None -> "None"

let isSellToString isSell = if isSell then "SELL" else "BUY"

// Build transactions
let buildTxn (txn: string * seq<Account.Row>) =
    let records = snd txn

    try
        let descRow = Seq.last records // Row with txn description is always the last

        let matches =
            Regex.Match(descRow.Description, "^(Buy|Sell) (\d+) .+?(?=@)@([\d\.\d]+) (EUR|USD) \((.+)\)")

        let isSell = matches.Groups.[1].Value.Equals "Sell"
        let quantity = int matches.Groups.[2].Value
        let value = float matches.Groups.[3].Value

        let valueCurrency =
            if matches.Groups.[4].Value.Equals(nameof EUR)
            then EUR
            else USD

        let productId = matches.Groups.[5].Value

        let price =
            match valueCurrency with
            | EUR -> descRow.Price
            | USD ->
                let fxRow =
                    match isSell with
                    | true ->
                        records
                        |> Seq.find (fun x -> x.Description.Equals "FX Credit")
                    | false ->
                        records
                        |> Seq.find (fun x -> x.Description.Equals "FX Debit")

                fxRow.Price

        let degiroFees =
            records
            |> Seq.filter (fun x -> x.Description.Equals "DEGIRO Transaction Fee")
            |> Seq.sumBy (fun x -> x.Price)

        { Date = (Option.defaultValue (DateTime.MinValue) descRow.Date)
          Type = (if isSell then Sell else Buy)
          Product = descRow.Product
          ProductId = productId
          ProdType = Shares // FIXME: tell apart ETF from Shares
          Quantity = quantity
          Fees = degiroFees
          Price = price
          Value = value
          ValueCurrency = valueCurrency
          OrderId = (Option.defaultValue (Guid.Empty) descRow.OrderId) }
    with ex -> failwithf "Error: %A - %s \n%A" (Seq.head records) ex.Message ex


// Get all rows corresponding to some order, grouped by their OrderId
let txns: seq<string * seq<Account.Row>> =
    account.Rows
    |> Seq.filter (fun row -> Option.isSome row.OrderId)
    |> Seq.groupBy (fun row ->
        match row.OrderId with
        | Some (x) -> x.ToString().[0..18]
        | None -> "")

txns
txns |> Seq.map buildTxn


// Get deposits amount
let depositTot =
    account.Rows
    |> Seq.filter (fun x -> x.Description.Equals "Deposit")
    |> Seq.sumBy (fun x -> x.Price)

//let filtered = account.Rows
//                |> Seq.filter (fun x -> not (System.String.IsNullOrEmpty x.Product))
//                |> Seq.map (fun x -> (x.Date, x.Product, x.Description))
//                |> Seq.groupBy (fun (date: Option<DateTime>, product: string, desc: string) -> product)
//    |> Seq.filter (fun x -> !x.Product.Contains("Flatex") )
