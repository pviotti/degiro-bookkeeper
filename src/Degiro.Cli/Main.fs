open System
open System.IO
open System.Reflection

open Argu

open Degiro
open Degiro.Account

let VERSION =
    Assembly
        .GetExecutingAssembly()
        .GetName()
        .Version.ToString()

let PROGRAM_NAME = "degiro"

type CliArguments =
    | [<NoAppSettings>] Version
    | [<Mandatory; MainCommand>] CsvFilePath of file: string
    | [<AltCommandLine("-y")>] Year of year: int
    | [<AltCommandLine("-p")>] Period of period: int

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Version _ -> $"print {PROGRAM_NAME} version"
            | CsvFilePath _ -> "path of Degiro account csv file"
            | Year _ -> "year"
            | Period _ -> "Irish tax period (1: Jan-Nov; 2: Dec)"


let printVersion () = printfn $"{PROGRAM_NAME} v{VERSION}"

let printEarnings earnings =
    printfn $"""%-10s{"Date"} %-40s{"Product"} %7s{"P/L (€)"} %8s{"P/L %"}"""
    printfn $"""%s{String.replicate 68 "-"}"""

    let printEarning (e: Earning) =
        printfn "%s %-40s %7.2f %7.1f%%" (e.Date.ToString("yyyy-MM-dd")) e.Product e.Value e.Percent

    earnings |> List.iter printEarning

let printDividends dividends =
    printfn $"""%-40s{"Product"} %7s{"Tax"} %7s{"Value"} %8s{"Currency"}"""
    printfn $"""%s{String.replicate 65 "-"}"""

    let printDividend (d: Dividend) =
        printfn $"%-40s{d.Product} %7.2f{d.ValueTax} %7.2f{d.Value} %8A{d.Currency}"

    dividends |> List.iter printDividend

[<EntryPoint>]
let main argv =
    let parser =
        ArgumentParser.Create<CliArguments>(
            programName = PROGRAM_NAME,
            errorHandler = ProcessExiter(),
            checkStructure = false
        )

    let args = parser.ParseCommandLine(argv)

    if args.Contains Version then
        printVersion ()
        Environment.Exit 0

    let csvFilePath = args.GetResult CsvFilePath
    let year = args.GetResult Year

    let period =
        if args.Contains Period then
            enum<Period> (args.GetResult Period)
        else
            Period.All

    // CSV cleaning
    let originalCsvContent = File.ReadAllText csvFilePath
    let cleanCsv, isMalformed = cleanCsv originalCsvContent

    if isMalformed then
        let newFilePath =
            csvFilePath.[..csvFilePath.Length - 5]
            + "-clean.csv"

        File.WriteAllText(newFilePath, cleanCsv)
        printfn $"Cleaned csv file has been written to {newFilePath}.\n"

    let rows = AccountCsv.Parse(cleanCsv).Rows

#if DEBUG
    let timer = Diagnostics.Stopwatch()
    timer.Start()
#endif

    let txnsGrouped = getRowsGroupedByOrderId rows

    let txns =
        Seq.map buildTxn txnsGrouped |> Seq.toList

    let sellsInPeriod = getSellTxnsInPeriod txns year period

    // Earnings
    if List.isEmpty sellsInPeriod then
        printfn $"No sells recorded in %d{year}, period: %A{period}."
    else
        let periodEarnings = getSellsEarnings sellsInPeriod txns
        printfn $"Earnings in {year}, period %A{period}:\n"
        printEarnings periodEarnings

        let periodTotalEarnings =
            periodEarnings |> List.sumBy (fun x -> x.Value)

        let periodAvgPercEarnings =
            periodEarnings
            |> List.averageBy (fun x -> x.Percent)

        printfn
            $"""
Tot. P/L (€): %.2f{periodTotalEarnings}
Avg %% P/L: %.2f{periodAvgPercEarnings}%%"""

    // Dividends
    let dividends = getAllDividends rows year
    printfn $"\nDividends in {year}:\n"
    printDividends dividends

    let getTotalNetDividends dividends currency =
        dividends
        |> List.filter (fun x -> x.Currency = currency)
        |> List.sumBy (fun x -> x.Value + x.ValueTax)

    printfn
        $"""
Tot. net dividends in €: {getTotalNetDividends dividends EUR}
Tot. net dividends in $: {getTotalNetDividends dividends USD}"""

    // Total deposits and fees
    let yearTotFees = getTotalYearFees rows year
    let totDeposits = getTotalDeposits rows
    let totYearDeposits = getTotalYearDeposits rows year

    printfn
        $"""
Tot. Degiro fees in %d{year} (€): %.2f{yearTotFees}
Tot. deposits in %d{year} (€): %.2f{totYearDeposits}
Tot. deposits (€): %.2f{totDeposits}"""

#if DEBUG
    printfn $"\nElapsed time: {timer.ElapsedMilliseconds} ms"
#endif
    0
