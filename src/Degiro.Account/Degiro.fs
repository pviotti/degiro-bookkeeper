namespace Degiro

open System
open System.Text
open System.Text.RegularExpressions

open FSharp.Data

module Account =

    let txnDescriptionRegExp =
        "^(Buy|Sell) (\d+) .+?(?=@)@([\d\.\d]+) (EUR|USD)"

    [<Literal>]
    let accountStatementSampleCsv = """
        Date,Time,Value date,Product,ISIN,Description,FX,Change,,Balance,,Order ID
        28-10-2020,14:30,28-10-2020,ASSET DESCR,US4780000,FX Credit,1.1719,USD,563.60,USD,0.00,3aca1fc3-d622-46de-8c8b-1bec568feac5
        12-09-2020,07:42,21-09-2020,,,FX Debit,,EUR,1.15,EUR,1018.47,
        31-12-2020,01:31,31-12-2020,,,Flatex Interest,,EUR,-0.79,EUR,470.07,"""

    // Culture is set to parse dates in the dd-mm-YYY format as `Option<DateTime>` type
    type AccountCsv = CsvProvider<accountStatementSampleCsv, Schema=",,,,,,,,Price (float),,,OrderId", Culture="en-IRL">

    // Get all rows corresponding to some order, grouped by their OrderId
    let getAllTxnRowsGrouped (account: AccountCsv) : seq<string * seq<AccountCsv.Row>> =
        account.Rows
        |> Seq.filter (fun row -> Option.isSome row.OrderId)
        |> Seq.groupBy
            (fun row ->
                match row.OrderId with
                | Some (x) -> x.ToString().[0..18] // XXX because some Guids can be malformed in input csv
                | None -> "")

    // Build a transaction object (Txn) (i.e. rows corresponding to DeGiro orders)
    // by parsing multiple rows in the account corresponding to the transaction in object
    let buildTxn (txn: string * seq<AccountCsv.Row>) =
        let records = snd txn

        try
            // get all rows containing a description of the transaction
            let descRows =
                records
                |> Seq.filter (fun x -> Regex.IsMatch(x.Description, txnDescriptionRegExp))

            assert (Seq.length descRows >= 1)

            let firstDescRow = Seq.head descRows

            let matches =
                Regex.Match(firstDescRow.Description, txnDescriptionRegExp)

            let txnType =
                match matches.Groups.[1].Value with
                | "Sell" -> Sell
                | "Buy" -> Buy
                | _ -> failwith $"Error: unsupported transaction type for {firstDescRow}"

            let valueCurrency =
                match matches.Groups.[4].Value with
                | "EUR" -> EUR
                | "USD" -> USD
                | _ -> failwith $"Error: unsupported currency for {firstDescRow}"

            let getFractionalQuantity (row: AccountCsv.Row) =
                let matches =
                    Regex.Match(row.Description, txnDescriptionRegExp)

                int matches.Groups.[2].Value

            let getFractionalPrice (row: AccountCsv.Row) =
                let matches =
                    Regex.Match(row.Description, txnDescriptionRegExp)

                (float matches.Groups.[2].Value)
                * (float matches.Groups.[3].Value)

            let totQuantity =
                descRows
                |> Seq.map getFractionalQuantity
                |> Seq.sum

            let totValue =
                let totalPrice =
                    descRows |> Seq.map getFractionalPrice |> Seq.sum

                totalPrice / (float totQuantity)

            let price =
                match valueCurrency with
                | EUR -> descRows |> Seq.sumBy (fun x -> x.Price)
                | USD ->
                    match txnType with
                    | Sell ->
                        records
                        |> Seq.filter (fun x -> x.Description.Equals "FX Credit")
                        |> Seq.sumBy (fun x -> x.Price)
                    | Buy ->
                        records
                        |> Seq.filter (fun x -> x.Description.Equals "FX Debit")
                        |> Seq.sumBy (fun x -> x.Price)

            let degiroFees =
                records
                |> Seq.filter (fun x -> x.Description.Equals "DEGIRO Transaction Fee")
                |> Seq.sumBy (fun x -> x.Price)

            { Date = firstDescRow.Date + firstDescRow.Time
              Type = txnType
              Product = firstDescRow.Product
              ProductId = firstDescRow.ISIN
              ProdType = Shares // FIXME: tell apart ETF from Shares
              Quantity = totQuantity
              Fees = degiroFees
              Price = price
              Value = totValue
              ValueCurrency = valueCurrency
              OrderId = (Option.defaultValue Guid.Empty firstDescRow.OrderId) }
        with ex -> failwithf $"Error: %A{Seq.last records} - %s{ex.Message} \n%A{ex}"

    // Get all sell transactions for the given period
    let getSellTxnsInPeriod (txns: list<Txn>) (year: int) (period: Period) =
        txns
        |> List.sortByDescending (fun x -> x.Date)
        |> List.filter
            (fun x ->
                match period with
                | Period.Initial ->
                    x.Date.Month < 12
                    && x.Date.Year = year
                    && x.Type = Sell
                | Period.Later ->
                    x.Date.Month = 12
                    && x.Date.Year = year
                    && x.Type = Sell
                | _ -> x.Date.Year = year && x.Type = Sell)


    // For a given Sell transaction, compute its earning by
    // going back in time to as many Buy transactions as required to match the quantity sold
    // FIXME: make it comply with Irish CGT FIFO rule
    let computeEarning (txns: list<Txn>) (sellTxn: Txn) =
        let buysPrecedingSell =
            txns
            |> List.filter
                (fun x ->
                    x.Type = Buy
                    && x.Product = sellTxn.Product
                    && x.Date < sellTxn.Date)
            |> List.sortByDescending (fun x -> x.Date)

        let rec getTotBuyPrice (buys: list<Txn>) (quantityToSell: int) (totBuyPrice: float) =
            if quantityToSell = 0 then
                totBuyPrice
            elif List.isEmpty buys && quantityToSell <> 0 then // Should not happen
                failwithf $"Error: can't find buy txns for remaining {quantityToSell} sells of {sellTxn.Product}"
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

                getTotBuyPrice (List.tail buys) quantityRemaining newTotalBuyPrice

        let totBuyPrice =
            getTotBuyPrice buysPrecedingSell sellTxn.Quantity 0.0

        let earning = sellTxn.Price + totBuyPrice
        earning, earning / (-totBuyPrice) * 100.0

    // Return the Earning objects for a given sequence of sells
    let getSellsEarnings (sells: list<Txn>) (allTxns: list<Txn>) : list<Earning> =
        sells
        |> List.map
            (fun sell ->
                let earning, earningPercentage = computeEarning allTxns sell

                { Date = sell.Date
                  Product = sell.Product
                  Value = earning
                  Percent = earningPercentage })

    // TODO Compute CGT to pay
    // let yearCgt = 0.33 * yearTotalEarning

    // Compute total DeGiro Fees (txn fees and stock exchange fees)
    let getTotalYearFees (account: AccountCsv) (year: int) =
        account.Rows
        |> Seq.filter
            (fun x ->
                x.Date.Year = year
                && (x.Description.Equals "DEGIRO Transaction Fee"
                    || x.Description.StartsWith "DEGIRO Exchange Connection Fee"))
        |> Seq.sumBy (fun x -> x.Price)

    // Get deposits amounts
    let getTotalDeposits (account: AccountCsv) =
        account.Rows
        |> Seq.filter
            (fun x ->
                x.Description.Equals "Deposit"
                || x.Description.Equals "flatex Deposit")
        |> Seq.sumBy (fun x -> x.Price)

    let getTotalYearDeposits (account: AccountCsv) (year: int) =
        account.Rows
        |> Seq.filter
            (fun x ->
                (x.Description.Equals "Deposit"
                 || x.Description.Equals "flatex Deposit")
                && x.Date.Year = year)
        |> Seq.sumBy (fun x -> x.Price)

    let cleanCsv (csvContent: string) =
        let rows = csvContent.Split '\n'

        let malformed =
            Array.Exists(rows, (fun row -> (row.[0..2].Equals ",,,")))

        if malformed then
            let sb = StringBuilder()

            rows
            |> Array.iter
                (fun row ->
                    let strToAppend =
                        if not (row.[0..2].Equals ",,,") then
                            row
                        else
                            sb.Remove(sb.Length - 1, 1) |> ignore
                            Array.last (row.Split(","))

                    sb.Append strToAppend |> ignore
                    sb.AppendLine() |> ignore)

            sb.ToString(), malformed
        else
            csvContent, malformed