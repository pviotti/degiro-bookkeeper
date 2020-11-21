open System.Transactions

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

// Get deposits amount
let depositTot =
    account.Rows
    |> Seq.filter (fun x -> x.Description.Equals "Deposit")
    |> Seq.sumBy (fun x -> x.Price)

// Get all Sell transactions for the given year
let yearSells =
    txns
    |> Seq.sortByDescending (fun x -> x.Date)
    |> Seq.filter (fun x -> x.Date.Year = 2020 && x.Type = Sell)

// For each Sell transaction, compute its earning by
//    going back in time to as many Buy transactions as required to match the quantity sold
//    FIXME: make it comply with Irish CGT FIFO rule
let computeEarning txns sellTxn =
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

    let totBuyPrice = getTotBuyPrice buysPrecedingSell sellTxn.Quantity 0.0
    sellTxn.Price + totBuyPrice
    
let yearEarnings = yearSells |> Seq.map (fun sell ->
                                     let earning = computeEarning txns sell
                                     sell.Product, earning)
Seq.iter (fun x -> printfn "%A" x) yearEarnings

// Compute total gain in a given year
let yearTotalEarning = Seq.sumBy snd yearEarnings

// Compute CGT to pay
let yearCgt = 0.33 * yearTotalEarning
