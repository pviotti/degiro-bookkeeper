namespace Degiro

open System
open System.Text
open FSharp.Data

module CsvOutput =

    type EarningsCsvType =
        CsvProvider<
            Sample="Date,Product,ISIN,Type,Value,Percent",
            Schema="Date (string), Product (string), ISIN (string), Type (string), Value (decimal), Percent (decimal)",
            HasHeaders=true
         >

    type DividendsCsvType =
        CsvProvider<
            Sample="Product,ISIN,Value,Value Tax,Currency",
            Schema="Product (string), ISIN (string), Value (decimal), Value Tax(decimal), Currency (string)",
            HasHeaders=true
         >

    type EtfBuysCsvType =
        CsvProvider<
            Sample="Date,Product,ISIN,Quantity,Value,Price,Fees",
            Schema="Date (string), Product (string), ISIN (string), Quantity (int), Value (decimal), Price (decimal), Fees (decimal)",
            HasHeaders=true
         >

    let earningsToCsvString (earnings: Earning list) =
        let earningToRow (earning: Earning) =
            EarningsCsvType.Row(
                earning.Date.ToString("yyy-MM-dd"),
                earning.Product,
                earning.ISIN,
                earning.ProdType.ToString(),
                earning.Value,
                earning.Percent
            )

        let rows = earnings |> List.map earningToRow
        let csv = new EarningsCsvType(rows)
        csv.SaveToString()

    let dividendsToCsvString (dividends: Dividend list) =
        let dividendToRow (dividend: Dividend) =
            DividendsCsvType.Row(
                dividend.Product,
                dividend.ISIN,
                dividend.Value,
                dividend.ValueTax,
                dividend.Currency.ToString()
            )

        let rows = dividends |> List.map dividendToRow
        let csv = new DividendsCsvType(rows)
        csv.SaveToString()

    let etfBuysToCsvString (etfBuys: Txn list) =
        let etfBuyToRow (etfBuy: Txn) =
            EtfBuysCsvType.Row(
                etfBuy.Date.ToString("yyy-MM-dd"),
                etfBuy.Product,
                etfBuy.ISIN,
                etfBuy.Quantity,
                etfBuy.Value,
                etfBuy.Price,
                etfBuy.Fees
            )

        let rows = etfBuys |> List.map etfBuyToRow
        let csv = new EtfBuysCsvType(rows)
        csv.SaveToString()


module CliOutput =

    let getEarningsCliString (earnings: Earning list) =
        let sb = StringBuilder()

        let headerLine = $"""%-10s{"Date"} %-40s{"Product"} %9s{"P/L (€)"} %8s{"P/L %"}"""
        sb.AppendLine headerLine |> ignore

        sb.AppendLine $"""%s{String.replicate headerLine.Length "─"}""" |> ignore

        let getEarningLine (e: Earning) =
            sb.AppendLine $"""%s{e.Date.ToString("yyyy-MM-dd")} %-40s{e.Product} %9.2f{e.Value} %7.1f{e.Percent}%%"""
            |> ignore

        earnings |> List.iter getEarningLine

        let periodTotalEarnings =
            match earnings.Length with
            | 0 -> 0.0m
            | _ -> earnings |> List.sumBy _.Value

        let periodAvgPercEarnings =
            match earnings.Length with
            | 0 -> 0.0m
            | _ -> earnings |> List.averageBy _.Percent

        sb.AppendLine() |> ignore

        sb.AppendLine $"""Tot. P/L (€): %.2f{periodTotalEarnings}""" |> ignore

        sb.AppendLine $"""Avg %% P/L: %.2f{periodAvgPercEarnings}%%""" |> ignore

        sb.ToString()

    let getEtfBuysCliString (etfBuys: Txn list) =
        let sb = StringBuilder()

        let headerLine =
            $"""%-10s{"Date"} %-40s{"Product"} %8s{"Quantity"} %9s{"Value [€]"} %9s{"Price [€]"} %9s{"Fees [€]"}"""

        sb.AppendLine headerLine |> ignore

        sb.AppendLine $"""%s{String.replicate headerLine.Length "─"}""" |> ignore

        let getEtfBuyLine (e: Txn) =
            sb.AppendLine
                $"""%s{e.Date.ToString("yyyy-MM-dd")} %-40s{e.Product} %8d{e.Quantity} %9.2f{e.Value} %9.2f{e.Price} %9.2f{e.Fees}"""
            |> ignore

        etfBuys |> List.iter getEtfBuyLine

        let totalEtfBuys = etfBuys |> List.sumBy _.Price

        sb.AppendLine() |> ignore

        sb.AppendLine $"""Tot. ETF buys in €: %.2f{totalEtfBuys}""" |> ignore

        sb.ToString()

    let getDividendsCliString (dividends: Dividend list) =
        let sb = StringBuilder()

        let headerLine = $"""%-40s{"Product"} %9s{"Tax"} %9s{"Value"} %8s{"Currency"}"""
        sb.AppendLine headerLine |> ignore

        sb.AppendLine $"""%s{String.replicate headerLine.Length "─"}""" |> ignore

        let getDividentLine (d: Dividend) =
            sb.AppendLine $"%-40s{d.Product} %9.2f{d.ValueTax} %9.2f{d.Value} %8A{d.Currency}"
            |> ignore

        dividends |> List.iter getDividentLine

        let getTotalNetDividends dividends currency =
            dividends
            |> List.filter (fun x -> x.Currency = currency)
            |> List.sumBy (fun x -> x.Value + x.ValueTax)

        sb.AppendLine() |> ignore

        sb.AppendLine $"""Tot. net dividends in €: {getTotalNetDividends dividends EUR}"""
        |> ignore

        sb.AppendLine $"""Tot. net dividends in $: {getTotalNetDividends dividends USD}"""
        |> ignore

        sb.AppendLine $"""Tot. net dividends in £: {getTotalNetDividends dividends GBP}"""
        |> ignore

        sb.AppendLine $"""Tot. net dividends in Can$: {getTotalNetDividends dividends CAD}"""
        |> ignore

        sb.ToString()

    let getStockChangesCliString (stockChanges: seq<StockChange>) =
        let sortedStockChanges = stockChanges |> Seq.sortByDescending _.Date

        let sb = StringBuilder()

        let headerLine =
            $"""%-10s{"Date"} %-13s{"ISIN Before"} %-13s{"ISIN After"} %-40s{"Product Name Before"} %-40s{"Product Name After"} %-10s{"Multiplier"}"""

        sb.AppendLine headerLine |> ignore

        sb.AppendLine $"""%s{String.replicate headerLine.Length "─"}""" |> ignore

        let getChangeLine (s: StockChange) =
            sb.AppendLine
                $"""%-10s{s.Date.ToString("yyyy-MM-dd")} %-13s{s.IsinBefore} %-13s{s.IsinAfter} %-40s{s.ProductBefore} %-40s{s.ProductAfter} %10d{s.Multiplier}"""
            |> ignore

        sortedStockChanges |> Seq.iter getChangeLine

        sb.ToString()