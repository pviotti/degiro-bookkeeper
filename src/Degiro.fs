// namespace StockWatch

open System
open System.IO
open System.Text
open System.Text.RegularExpressions

open FSharp.Data

open StockWatch

let txnDescriptionRegExp = "^(Buy|Sell) (\d+) .+?(?=@)@([\d\.\d]+) (EUR|USD)"

[<Literal>]
let accountStatementSampleCsv = """
    Date,Time,Value date,Product,ISIN,Description,FX,Change,,Balance,,Order ID
    28-10-2020,14:30,28-10-2020,ASSET DESCR,US4780000,FX Credit,1.1719,USD,563.60,USD,0.00,3aca1fc3-d622-46de-8c8b-1bec568feac5
    12-09-2020,07:42,21-09-2020,,,FX Debit,,EUR,1.15,EUR,1018.47, 
    31-12-2020,01:31,31-12-2020,,,Flatex Interest,,EUR,-0.79,EUR,470.07,"""

// Culture is set to parse dates in the dd-mm-YYY format as `Option<DateTime>` type
type Account = CsvProvider<accountStatementSampleCsv, Schema=",,,,,,,,Price (float),,,OrderId", Culture="en-IRL">

module DegiroAccount =

    // Get all rows corresponding to some order, grouped by their OrderId
    let getAllTxnRowsGrouped (account: Account) : seq<string * seq<Account.Row>> =
        account.Rows
        |> Seq.filter (fun row -> Option.isSome row.OrderId)
        |> Seq.groupBy (fun row ->
            match row.OrderId with
            | Some (x) -> x.ToString().[0..18] // XXX because some Guids can be malformed in input csv
            | None -> "")

    // Build transactions (Txn) (i.e. rows corresponding to DeGiro orders)
    let buildTxn (txn: string * seq<Account.Row>) =
        let records = snd txn

        try
            let descRow = Seq.last records // Row with txn description is always the last

            let matches =
                Regex.Match(descRow.Description, txnDescriptionRegExp)

            let txnType = if matches.Groups.[1].Value.Equals "Sell" then Sell else Buy
            let quantity = int matches.Groups.[2].Value
            let value = float matches.Groups.[3].Value

            let valueCurrency =
                if matches.Groups.[4].Value.Equals(nameof EUR)
                then EUR
                else USD

            let productId = descRow.ISIN

            let price =
                match valueCurrency with
                | EUR -> descRow.Price
                | USD ->
                    let fxRow =
                        match txnType with
                        | Sell ->
                            records
                            |> Seq.find (fun x -> x.Description.Equals "FX Credit")
                        | Buy ->
                            records
                            |> Seq.find (fun x -> x.Description.Equals "FX Debit")

                    fxRow.Price

            let degiroFees =
                records
                |> Seq.filter (fun x -> x.Description.Equals "DEGIRO Transaction Fee")
                |> Seq.sumBy (fun x -> x.Price)

            { Date = descRow.Date
              Type = txnType
              Product = descRow.Product
              ProductId = productId
              ProdType = Shares // FIXME: tell apart ETF from Shares
              Quantity = quantity
              Fees = degiroFees
              Price = price
              Value = value
              ValueCurrency = valueCurrency
              OrderId = (Option.defaultValue (Guid.Empty) descRow.OrderId) }
        with ex ->
            failwithf "Error: %A - %s \n%A" (Seq.last records) ex.Message ex

    // Get all sell transactions for the given period
    let getSellTxnsInPeriod (txns: seq<Txn>) (year: int) (period: Period) =
        txns
        |> Seq.sortByDescending (fun x -> x.Date)
        |> Seq.filter (fun x ->
                match period with
                | Period.Initial -> x.Date.Month < 12 && x.Date.Year = year && x.Type = Sell
                | Period.Later -> x.Date.Month = 12 && x.Date.Year = year && x.Type = Sell
                | _ -> x.Date.Year = year && x.Type = Sell)


    // For a given Sell transaction, compute its earning by
    // going back in time to as many Buy transactions as required to match the quantity sold
    // FIXME: make it comply with Irish CGT FIFO rule
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

    // Return the Earning objects for a given sequence of sells
    let getSellsEarnings (sells: seq<Txn>) (allTxns: seq<Txn>): seq<Earning> =
        sells
        |> Seq.map (fun sell ->
            let earning, earningPercentage = computeEarning allTxns sell

            { Date = sell.Date
              Product = sell.Product
              Value = earning
              Percent = earningPercentage })

    // TODO Compute CGT to pay
    // let yearCgt = 0.33 * yearTotalEarning

    // Compute total DeGiro Fees (txn fees and stock exchange fees)
    let getTotalYearFees (account: Account) (year: int) =
        account.Rows
        |> Seq.filter (fun x ->
            x.Date.Year = year
            && (x.Description.Equals "DEGIRO Transaction Fee"
                || x.Description.StartsWith "DEGIRO Exchange Connection Fee"))
        |> Seq.sumBy (fun x -> x.Price)

    // Get deposits amounts
    let getTotalDeposits (account: Account) =
        account.Rows
        |> Seq.filter (fun x ->
            x.Description.Equals "Deposit"
            || x.Description.Equals "flatex Deposit")
        |> Seq.sumBy (fun x -> x.Price)

    let getTotalYearDeposits (account: Account) (year: int) =
        account.Rows
        |> Seq.filter (fun x ->
            (x.Description.Equals "Deposit"
             || x.Description.Equals "flatex Deposit")
            && x.Date.Year = year)
        |> Seq.sumBy (fun x -> x.Price)

    let cleanCsv (csvContent: string) =
        let rows = csvContent.Split '\n'
        let malformed = Array.Exists (rows, (fun row -> (row.[0 .. 2].Equals ",,,")))
        if malformed then
            let sb = StringBuilder()
            rows
            |> Seq.iter (fun row ->
                            let strToAppend = 
                                if not (row.[0 .. 2].Equals ",,,") then 
                                    row
                                else 
                                    sb.Remove (sb.Length-1, 1) |> ignore
                                    Array.last (row.Split(","))
                            sb.Append strToAppend |> ignore
                            sb.AppendLine() |> ignore)
            sb.ToString(), malformed
        else
            csvContent, malformed



open DegiroAccount

[<EntryPoint>]
let main argv =

    if argv.Length < 3 then
        eprintfn "Error: missing parameter"
        eprintfn "Usage: stock-watcher <path/to/statement.csv> <year> [<1,2>]"
        Environment.Exit 1

    //let [<Literal>] csvFile = __SOURCE_DIRECTORY__ + "/account.csv"
    // let csvFile = __SOURCE_DIRECTORY__ + string (Path.DirectorySeparatorChar) + argv.[2]
    let originalCsvContent = System.IO.File.ReadAllText argv.[0]
    let cleanCsv, malformed = cleanCsv originalCsvContent

    if malformed then File.WriteAllText ((argv.[0] + "-clean.csv"), cleanCsv)    

    let account = Account.Parse(cleanCsv)
    let year = int argv.[1]
    let period = if argv.Length <= 2 then Period.All
                    else enum<Period>(int argv.[2])

    let txnsGrouped = getAllTxnRowsGrouped account
    let txns = Seq.map buildTxn txnsGrouped
    let sellsInPeriod = getSellTxnsInPeriod txns year period

    if Seq.isEmpty sellsInPeriod then
        printfn "No sells recorded in %d, period %A." year period
        Environment.Exit 0

    printfn "%-10s %-40s %7s %8s\n%s" "Date" "Product" "P/L (€)" "P/L %" (String.replicate 68 "-")
    let printEarning (e: Earning) =
        printfn "%s %-40s %7.2f %7.1f%%" (e.Date.ToString("yyyy-MM-dd")) e.Product e.Value e.Percent

    let periodEarnings = getSellsEarnings sellsInPeriod txns
    Seq.toList periodEarnings |> List.map printEarning |> ignore
    
    let periodTotalEarnings = periodEarnings |> Seq.sumBy (fun x -> x.Value)
    let periodAvgPercEarnings = periodEarnings |> Seq.averageBy (fun x -> x.Percent)
    printfn "\nTot. P/L (€): %.2f" periodTotalEarnings
    printfn "Avg %% P/L: %.2f%%" periodAvgPercEarnings

    let yearTotFees = getTotalYearFees account year
    printfn "\nTot. DeGiro fees in %d (€): %.2f" year yearTotFees

    let totDeposits = getTotalDeposits account
    let totYearDeposits = getTotalYearDeposits account year
    printfn "\nTot. deposits (€): %.2f" totDeposits
    printfn "Tot. deposits in %d (€): %.2f" year totYearDeposits
    0 