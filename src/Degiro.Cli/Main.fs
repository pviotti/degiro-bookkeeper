open System
open System.IO
open System.Reflection

open Argu

open Degiro
open Degiro.Account
open Degiro.CliOutput
open Degiro.CsvOutput

let VERSION =
    Assembly
        .GetExecutingAssembly()
        .GetName()
        .Version.ToString()

let PROGRAM_NAME = "degiro"

type CliArguments =
    | [<NoAppSettings>] Version
    | [<Mandatory; MainCommand>] CsvFilePath of input: string
    | [<AltCommandLine("-y")>] Year of year: int
    | [<AltCommandLine("-p")>] Period of period: int
    | [<AltCommandLine("-o")>] OutputPath of output_path: string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Version _ -> $"print {PROGRAM_NAME} version"
            | CsvFilePath _ -> "path of Degiro account CSV file"
            | Year _ -> "year"
            | Period _ -> "Irish CGT tax period (1: Jan-Nov; 2: Dec)"
            | OutputPath _ -> "output path for earnings and dividends CSVs"


let printVersion () = printfn $"{PROGRAM_NAME} v{VERSION}"


[<EntryPoint>]
let main argv =
    try
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
                match args.GetResult Period with
                | 1 -> Period.Initial
                | 2 -> Period.Later
                | _ -> failwith "Irish CGT tax period not supported (1: Jan-Nov; 2: Dec)"
            else
                Period.All

        let outputPath =
            if args.Contains OutputPath then
                Some(args.GetResult OutputPath)
            else
                None

        // CSV cleaning
        let originalCsvContent = File.ReadAllText csvFilePath
        let cleanCsv, isMalformed = cleanCsv originalCsvContent

        if isMalformed then
            let newFilePath =
                csvFilePath.[..csvFilePath.Length - 5]
                + "-clean.csv"

            File.WriteAllText(newFilePath, cleanCsv)
            printfn $"Cleaned CSV file has been written to {newFilePath}.\n"

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
            printfn $"%s{getEarningsCliString periodEarnings}"

            if outputPath.IsSome then
                let csvEarnings = earningsToCsvString periodEarnings

                let outputFilePath =
                    Path.Combine(outputPath.Value, $"{year}-{period}-degiro-earnings.csv")

                File.WriteAllText(outputFilePath, csvEarnings)
                printfn $"Earning CSV file written to {outputFilePath}\n"

        // Dividends
        let dividends = getAllDividends rows year
        printfn $"\nDividends in {year}:\n"
        printfn $"%s{getDividendsCliString dividends}"

        if outputPath.IsSome then
            let csvDividends = dividendsToCsvString dividends

            let outputFilePath =
                Path.Combine(outputPath.Value, $"{year}-degiro-dividends.csv")

            File.WriteAllText(outputFilePath, csvDividends)
            printfn $"Dividends CSV file written to {outputFilePath}\n"

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
    with ex ->
        eprintfn $"Error: %s{ex.Message}"
        Environment.Exit 1

    0
