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
    type AccountCsv =
        CsvProvider<accountStatementSampleCsv, Schema=",,,,,,,,Price (decimal?),,,OrderId", Culture="en-IE">

    type Row = AccountCsv.Row


    /// Get all rows corresponding to some Degiro order, grouped by their OrderId.
    let getRowsGroupedByOrderId (rows: seq<Row>) : seq<string * seq<Row>> =
        rows
        |> Seq.filter (fun row -> Option.isSome row.OrderId)
        |> Seq.groupBy
            (fun row ->
                match row.OrderId with
                | Some (x) -> x.ToString()[0..18] // XXX because some Guids can be malformed in input csv
                | None -> "")


    /// Build a transaction object (Txn).
    /// A transaction object (Txn) summarizes a set of rows in the Account Statement that correspond to a Degiro order.
    let buildTxn (txn: string * seq<Row>) =
        let allRows = snd txn

        try
            // get all rows containing a description of the transaction
            let descRows =
                allRows
                |> Seq.filter (fun x -> Regex.IsMatch(x.Description, txnDescriptionRegExp))

            assert (Seq.length descRows >= 1)

            let getTxnTypeAndCurrency (row: Row) =
                let matches =
                    Regex.Match(row.Description, txnDescriptionRegExp)

                let txnType =
                    TxnType.FromString matches.Groups[1].Value

                let valueCurrency =
                    Currency.FromString matches.Groups[4].Value

                txnType, valueCurrency

            let getFractionalQuantity (row: Row) =
                let matches =
                    Regex.Match(row.Description, txnDescriptionRegExp)

                int matches.Groups[2].Value

            let getFractionalPrice (row: Row) =
                let matches =
                    Regex.Match(row.Description, txnDescriptionRegExp)

                (decimal matches.Groups[2].Value)
                * (decimal matches.Groups[3].Value)

            let getTotQuantity (rows: seq<Row>) (filter: Row -> bool) =
                rows
                |> Seq.filter filter
                |> Seq.map getFractionalQuantity
                |> Seq.sum

            let firstDescRow = Seq.last descRows
            let txnType, valueCurrency = getTxnTypeAndCurrency firstDescRow

            let degiroFees =
                allRows
                |> Seq.filter (fun (x: Row) -> x.Description.Equals "DEGIRO Transaction Fee")
                |> Seq.sumBy (fun (x: Row) -> x.Price.Value)

            let price, totValue, totQuantity =
                if not (Seq.forall (fun x -> (txnType, valueCurrency) = (getTxnTypeAndCurrency x)) descRows) then
                    // if the order contains Buy and Sell transactions, then
                    // it's a case of cancelled and compensated transaction
                    // and the resulting Tnx should be a Buy with 0 quantity
                    let isSell =
                        fun (x: Row) -> x.Description.StartsWith "Sell"

                    let isBuy =
                        fun (x: Row) -> x.Description.StartsWith "Buy"

                    let totSellQuantity = getTotQuantity descRows isSell
                    let totBuyQuantity = getTotQuantity descRows isBuy

                    let totalPriceSells =
                        descRows
                        |> Seq.filter isSell
                        |> Seq.map getFractionalPrice
                        |> Seq.sum

                    let totalPriceBuys =
                        descRows
                        |> Seq.filter isBuy
                        |> Seq.map getFractionalPrice
                        |> Seq.sum

                    assert (totBuyQuantity = totSellQuantity)
                    assert (totalPriceBuys = totalPriceSells)
                    assert (degiroFees = 0.0m)
                    0.0m, 0.0m, 0
                else
                    let totQuantity = getTotQuantity descRows (fun _ -> true)

                    let totValue =
                        let totalPrice =
                            descRows |> Seq.map getFractionalPrice |> Seq.sum

                        totalPrice / (decimal totQuantity)

                    let price =
                        match valueCurrency with
                        | EUR -> descRows |> Seq.sumBy (fun x -> x.Price.Value)
                        | USD ->
                            match txnType with
                            | Sell ->
                                allRows
                                |> Seq.filter (fun x -> x.Description.Equals "FX Credit")
                                |> Seq.sumBy (fun x -> x.Price.Value)
                            | Buy ->
                                allRows
                                |> Seq.filter (fun x -> x.Description.Equals "FX Debit")
                                |> Seq.sumBy (fun x -> x.Price.Value)

                    price, totValue, totQuantity

            { Date = firstDescRow.Date + firstDescRow.Time
              Type = txnType
              Product = firstDescRow.Product
              ISIN = firstDescRow.ISIN
              ProdType = Shares // FIXME: tell apart ETF from Shares
              Quantity = totQuantity
              Fees = degiroFees
              Price = price
              Value = totValue
              ValueCurrency = valueCurrency
              OrderId = (Option.defaultValue Guid.Empty firstDescRow.OrderId) }
        with ex -> failwithf $"Error: %A{Seq.last allRows} - %s{ex.Message} \n%A{ex}"


    /// Get all sell transactions for the given year and Irish tax period
    let getSellTxnsInPeriod (txns: list<Txn>) (year: int) (period: Period) =
        txns
        |> List.filter (fun x -> x.Type = Sell)
        |> List.filter
            (fun x ->
                match period with
                | Period.Initial -> x.Date.Month < 12 && x.Date.Year = year
                | Period.Later -> x.Date.Month = 12 && x.Date.Year = year
                | _ -> x.Date.Year = year)
        |> List.sortByDescending (fun x -> x.Date)


    /// For a given Sell transaction, compute its earning by
    /// going back in time to as many Buy transactions as required to match the quantity sold
    // FIXME: make it comply with Irish CGT FIFO rule
    let computeEarning (txns: list<Txn>) (sellTxn: Txn) =
        let buysPrecedingSell =
            txns
            |> List.filter
                (fun x ->
                    x.Type = Buy
                    && x.ISIN = sellTxn.ISIN
                    && x.Date < sellTxn.Date)
            |> List.sortByDescending (fun x -> x.Date)

        let rec getTotBuyPrice (buys: list<Txn>) (quantityToSell: int) (totBuyPrice: decimal) =
            if quantityToSell = 0 then
                totBuyPrice
            elif List.isEmpty buys && quantityToSell <> 0 then // Should not happen
                failwithf $"can't find buy transactions for remaining {quantityToSell} sells of %A{sellTxn}"
            else
                let currBuy = List.head buys

                let quantityRemaining, newTotalBuyPrice =
                    if currBuy.Quantity <= quantityToSell then
                        quantityToSell - currBuy.Quantity, totBuyPrice + currBuy.Price
                    else
                        0,
                        (totBuyPrice
                         + (currBuy.Price / decimal currBuy.Quantity)
                           * decimal quantityToSell)

                getTotBuyPrice (List.tail buys) quantityRemaining newTotalBuyPrice

        let totBuyPrice =
            getTotBuyPrice buysPrecedingSell sellTxn.Quantity 0.0m

        let earning = sellTxn.Price + totBuyPrice
        earning, Math.Round(earning / (-totBuyPrice) * 100.0m, 2)


    /// Return the Earning objects for a given sequence of sells
    let getSellsEarnings (sells: list<Txn>) (allTxns: list<Txn>) : list<Earning> =
        sells
        |> List.map
            (fun sell ->
                let earning, earningPercentage = computeEarning allTxns sell

                { Date = sell.Date
                  Product = sell.Product
                  ISIN = sell.ISIN
                  ProdType = sell.ProdType
                  Value = earning
                  Percent = earningPercentage })


    /// Compute total Degiro Fees (transactions fees and stock exchange fees)
    let getTotalYearFees (rows: seq<Row>) (year: int) =
        rows
        |> Seq.filter
            (fun x ->
                x.Date.Year = year
                && (x.Description.Equals "DEGIRO Transaction Fee"
                    || x.Description.StartsWith "DEGIRO Exchange Connection Fee"))
        |> Seq.sumBy (fun x -> x.Price.Value)


    /// Get the total sum of deposits recorded in the Account Statement
    let getTotalDeposits (rows: seq<Row>) =
        rows
        |> Seq.filter
            (fun x ->
                x.Description.Equals "Deposit"
                || x.Description.Equals "flatex Deposit")
        |> Seq.sumBy (fun x -> x.Price.Value)


    /// Get the total sum of deposits for a given year
    let getTotalYearDeposits (rows: seq<Row>) (year: int) =
        rows
        |> Seq.filter
            (fun x ->
                (x.Description.Equals "Deposit"
                 || x.Description.Equals "flatex Deposit")
                && x.Date.Year = year)
        |> Seq.sumBy (fun x -> x.Price.Value)


    /// Clean a CSV string from malformed rows.
    /// Returns the clean string and
    /// a boolean stating if the input string was malformed.
    let cleanCsv (csvContent: string) =
        let rows = csvContent.Split '\n'

        let isMalformed =
            Array.Exists(rows, (fun row -> (row[0..2].Equals ",,,")))

        if isMalformed then
            let sb = StringBuilder()

            rows
            |> Array.iter
                (fun row ->
                    let strToAppend =
                        if row[0..2].Equals ",,," then
                            (Array.last (row.Split(","))).TrimEnd()
                        else
                            Environment.NewLine + row.TrimEnd()

                    sb.Append(strToAppend) |> ignore)

            sb.ToString().Trim(), isMalformed
        else
            csvContent, isMalformed


    /// Return a list of Dividend objects for a given year
    /// sorted in decreasing order by total dividend value.
    /// Note: they can be in different currencies, so sorting is not accurate.
    let getAllDividends (rows: seq<Row>) (year: int) =
        let rowsDividendsInYear =
            rows
            |> Seq.filter
                (fun x ->
                    x.Date.Year = year
                    && (x.Description.Equals "Dividend"
                        || x.Description.Equals "Dividend Tax"))
            |> Seq.toList

        let productsWithDividendsInYear =
            rowsDividendsInYear
            |> List.map (fun x -> x.Product)
            |> List.distinct

        let getAllDividendsForProductInYear (rowsDividends: Row list) (product: string) =
            let totDividends =
                rowsDividends
                |> List.filter
                    (fun x ->
                        x.Description.Equals "Dividend"
                        && x.Product = product)
                |> List.sumBy (fun x -> x.Price.Value)

            let totTaxDividends =
                rowsDividends
                |> List.filter
                    (fun x ->
                        x.Description.Equals "Dividend Tax"
                        && x.Product = product)
                |> List.sumBy (fun x -> x.Price.Value)

            let dividendRow = rowsDividends
                                 |> List.find (fun x -> x.Product = product)

            { Year = year
              Product = product
              ISIN = dividendRow.ISIN
              Value = totDividends
              ValueTax = totTaxDividends
              Currency = Currency.FromString dividendRow.Change}

        productsWithDividendsInYear
        |> List.map (getAllDividendsForProductInYear rowsDividendsInYear)
        |> List.sortByDescending (fun x -> x.Value)
