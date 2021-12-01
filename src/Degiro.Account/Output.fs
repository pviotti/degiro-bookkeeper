namespace Degiro

open System
open System.Text
open FSharp.Data

module CsvOutput =

    type EarningsCsvType =
        CsvProvider<Sample="Date,Product,ISIN,Type,Value,Percent", Schema="Date (string), Product (string), ISIN (string), Type (string), Value (decimal), Percent (decimal)", HasHeaders=true>

    type DividendsCsvType =
        CsvProvider<Sample="Product,ISIN,Value,Value Tax,Currency", Schema="Product (string), ISIN (string), Value (decimal), Value Tax(decimal), Currency (string)", HasHeaders=true>

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


module CliOutput =

    let getEarningsCliString (earnings: Earning list) =
        let sb = StringBuilder()

        sb.AppendLine $"""%-10s{"Date"} %-40s{"Product"} %7s{"P/L (€)"} %8s{"P/L %"}"""
        |> ignore

        sb.AppendLine $"""%s{String.replicate 68 "─"}"""
        |> ignore

        let getEarningLine (e: Earning) =
            sb.AppendLine $"""%s{e.Date.ToString("yyyy-MM-dd")} %-40s{e.Product} %7.2f{e.Value} %7.1f{e.Percent}%%"""
            |> ignore

        earnings |> List.iter getEarningLine

        let periodTotalEarnings =
            match earnings.Length with
            | 0 -> 0.0m
            | _ -> earnings |> List.sumBy (fun x -> x.Value)

        let periodAvgPercEarnings =
            match earnings.Length with
            | 0 -> 0.0m
            | _ -> earnings |> List.averageBy (fun x -> x.Percent)

        sb.AppendLine() |> ignore

        sb.AppendLine $"""Tot. P/L (€): %.2f{periodTotalEarnings}"""
        |> ignore

        sb.AppendLine $"""Avg %% P/L: %.2f{periodAvgPercEarnings}%%"""
        |> ignore

        sb.ToString()

    let getDividendsCliString (dividends: Dividend list) =
        let sb = StringBuilder()

        sb.AppendLine $"""%-40s{"Product"} %7s{"Tax"} %7s{"Value"} %8s{"Currency"}"""
        |> ignore

        sb.AppendLine $"""%s{String.replicate 65 "─"}"""
        |> ignore

        let getDividentLine (d: Dividend) =
            sb.AppendLine $"%-40s{d.Product} %7.2f{d.ValueTax} %7.2f{d.Value} %8A{d.Currency}"
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

        sb.ToString()
