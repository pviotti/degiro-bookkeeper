// Simple script to extract some statistics from DeGiro statements
// Requires: F# 5 (.NET 5)
// Usage: dotnet fsi degiro.fsx <path/to/statement.csv> <year>

#r "nuget: FSharp.Data"

open FSharp.Data
open System
open System.IO
open System.Text.RegularExpressions

// Change printed format of specific types in fsi REPL
//fsi.AddPrinter<DateTime>(fun d -> d.ToShortDateString())
//fsi.AddPrinter<TimeSpan>(fun time -> time.ToString("c"))

// Cmd line argument parsing
let argv = Environment.GetCommandLineArgs()

if argv.Length < 4 then
    eprintfn "Error: missing parameter"
    eprintfn "Usage: dotnet fsi %s <path/to/statement.csv> <year>" argv.[1]
    Environment.Exit 1

//let [<Literal>] csvFile = __SOURCE_DIRECTORY__ + "/account.csv"
let csvFile =
    __SOURCE_DIRECTORY__
    + string (Path.DirectorySeparatorChar)
    + argv.[2]

let year = int argv.[3]

[<Literal>]
let accountStatementSampleCsv = """
Date,Time,Value date,Product,ISIN,Description,FX,Change,,Balance,,Order ID
28-10-2020,14:30,28-10-2020,ASSET DESCR,US4780000,FX Credit,1.1719,USD,563.60,USD,0.00,3aca1fc3-d622-46de-8c8b-1bec568feac5
28-10-2020,06:42,27-10-2020,DESC,US4780000,Dividend,,USD,2.50,USD,2.12,
"""

// Entities
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
      Fees: float
      Price: float
      Value: float
      ValueCurrency: Currency
      OrderId: Guid }

type Earning =
    { Date: DateTime
      Product: string
      Value: float
      Percent: float }

// Culture is set to parse dates in the dd-mm-YYY format as `Option<DateTime>` type
type Account = CsvProvider<accountStatementSampleCsv, Schema=",,,,,,,,Price (float),,,OrderId", Culture="en-IRL">
let account: Account = Account.Load(csvFile)

//let firstRow : Account.Row = account.Rows |> Seq.head
//firstRow.Date

// Utility functions
let dateToString (date: Option<DateTime>) =
    match date with
    | Some (x) -> x.ToString "yyyy-MM-dd"
    | None -> "None"

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

        { Date = descRow.Date
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
    with ex -> failwithf "Error: %A - %s \n%A" (Seq.last records) ex.Message ex


// Get all rows corresponding to some order, grouped by their OrderId
let txnsRows: seq<string * seq<Account.Row>> =
    account.Rows
    |> Seq.filter (fun row -> Option.isSome row.OrderId)
    |> Seq.groupBy (fun row ->
        match row.OrderId with
        | Some (x) -> x.ToString().[0..18] // XXX because some Guids are garbled in input csv
        | None -> "")

txnsRows
let txns = txnsRows |> Seq.map buildTxn
txns

// Get all Sell transactions for the given year
let yearSells =
    txns
    |> Seq.sortByDescending (fun x -> x.Date)
    |> Seq.filter (fun x -> x.Date.Year = year && x.Type = Sell)

if Seq.isEmpty yearSells then
    printfn "No sells recorded in %d." year
    Environment.Exit 0

// For each Sell transaction, compute its earning by
//    going back in time to as many Buy transactions as required to match the quantity sold
//    FIXME: make it comply with Irish CGT FIFO rule
let computeEarning (txns: seq<Txn>) (sellTxn: Txn) =
    let buysPrecedingSell =
        txns
        |> Seq.sortByDescending (fun x -> x.Date)
        |> Seq.filter (fun x ->
            x.Type = Buy
            && x.Product = sellTxn.Product
            && x.Date < sellTxn.Date)

    let rec getTotBuyPrice (buys: seq<Txn>) (quantityToSell: int) (totBuyPrice: float) =
        if Seq.isEmpty buys || quantityToSell = 0 then
            totBuyPrice
        else
            let currBuy = Seq.head buys

            let quantityRemaining, newTotalBuyPrice =
                if currBuy.Quantity <= quantityToSell then
                    quantityToSell - currBuy.Quantity, totBuyPrice + currBuy.Price
                else
                    0,
                    (totBuyPrice
                     + (currBuy.Price / float (currBuy.Quantity))
                       * float (quantityToSell))

            getTotBuyPrice (Seq.tail buys) quantityRemaining newTotalBuyPrice

    let totBuyPrice =
        getTotBuyPrice buysPrecedingSell sellTxn.Quantity 0.0

    let earning = sellTxn.Price + totBuyPrice
    earning, earning / (-totBuyPrice) * 100.0

let yearEarnings: seq<Earning> =
    yearSells
    |> Seq.map (fun sell ->
        let earning, earningPercentage = computeEarning txns sell

        { Date = sell.Date
          Product = sell.Product
          Value = earning
          Percent = earningPercentage })

let printEarning (e: Earning) =
    printfn "%s %-40s %7.2f %7.1f%%" (e.Date.ToString("yyyy-MM-dd")) e.Product e.Value e.Percent

printfn "%-10s %-40s %7s %8s" "Date" "Product" "P/L (€)" "%"
Seq.toList yearEarnings |> List.map printEarning

// Compute gain stats for the given year
let yearTotalEarning =
    yearEarnings |> Seq.sumBy (fun x -> x.Value)

let yearAvgPercentEarning =
    yearEarnings |> Seq.averageBy (fun x -> x.Percent)

// Compute CGT to pay
let yearCgt = 0.33 * yearTotalEarning

// Compute total DeGiro Fees (txn fees and stock exchange fees)
let totFees =
    account.Rows
    |> Seq.filter (fun x ->
        x.Date.Year = year
        && (x.Description.Equals "DEGIRO Transaction Fee"
            || x.Description.StartsWith "DEGIRO Exchange Connection Fee"))
    |> Seq.sumBy (fun x -> x.Price)
//Seq.toList totFees |> List.iter (printfn "%A")

// Get deposits amount
let depositTot =
    account.Rows
    |> Seq.filter (fun x -> x.Description.Equals "Deposit")
    |> Seq.sumBy (fun x -> x.Price)
