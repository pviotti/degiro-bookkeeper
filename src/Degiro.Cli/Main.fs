open System
open System.IO
open System.Reflection

open Argu

open Degiro
open Degiro.Account
open Degiro.CliOutput
open Degiro.CsvOutput

let VERSION = Assembly.GetExecutingAssembly().GetName().Version.ToString()

let PROGRAM_NAME = AppDomain.CurrentDomain.FriendlyName

type CliArguments =
    | [<NoAppSettings>] Version
    | [<MainCommand>] CsvFilePath of input: string
    | [<AltCommandLine("-y")>] Year of year: int
    | [<AltCommandLine("-p")>] Period of period: int
    | [<AltCommandLine("-o")>] OutputPath of output_path: string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Version -> $"print {PROGRAM_NAME} version"
            | CsvFilePath _ -> "path of Degiro Account Statement CSV file"
            | Year _ -> "year (in YYYY format)"
            | Period _ -> "Irish CGT tax period (1: Jan-Nov; 2: Dec; default: whole year)"
            | OutputPath _ -> "path for earnings and dividends CSVs output"


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
                | _ -> failwith "Irish CGT tax period not supported (1: Jan-Nov; 2: Dec; <omitted>: all year)"
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
            let newFilePath = csvFilePath[.. csvFilePath.Length - 5] + "-clean.csv"

            File.WriteAllText(newFilePath, cleanCsv)
            printfn $"Cleaned CSV file has been written to {newFilePath}.\n"

        let rows = AccountCsv.Parse(cleanCsv).Rows

#if DEBUG
        let timer = Diagnostics.Stopwatch()
        timer.Start()
#endif

        let txnsGrouped = getRowsGroupedByOrderId rows

        let txns = Seq.map buildTxn txnsGrouped |> Seq.toList

        let stockChanges = getStockChanges rows

        if not (Map.isEmpty stockChanges) then
            printfn "🖋  The Account Statement reports the following stock splits, ISIN or product name changes:\n"
            printfn $"%s{getStockChangesCliString stockChanges.Values}\n"

        let sellsInPeriod = getSellTxnsInPeriod txns year period

        // Earnings
        if List.isEmpty sellsInPeriod then
            printfn $"No sells recorded in %d{year}, period: %A{period}."
        else
            let sellsSharesInPeriod =
                sellsInPeriod |> List.filter (fun x -> x.ProdType = Shares)

            let sellsETFInPeriod = sellsInPeriod |> List.filter (fun x -> x.ProdType = ETF)

            let earningsSharesInPeriod = getSellsEarnings sellsSharesInPeriod txns stockChanges

            printfn $"💰 Earnings from shares in {year}, period %A{period}:\n"
            printfn $"%s{getEarningsCliString earningsSharesInPeriod}"

            let earningsETFInPeriod = getSellsEarnings sellsETFInPeriod txns stockChanges
            printfn $"\n💰 Earnings from ETF in {year}, period %A{period}:\n"
            printfn $"%s{getEarningsCliString earningsETFInPeriod}"

            if outputPath.IsSome then
                let csvEarningsShares = earningsToCsvString earningsSharesInPeriod

                let csvEarningsETF = earningsToCsvString earningsETFInPeriod

                let csvEarningsSharesPath =
                    Path.Combine(outputPath.Value, $"{year}-{period.ToString().ToLower()}-degiro-shares-earnings.csv")

                let csvEarningsETFPath =
                    Path.Combine(outputPath.Value, $"{year}-{period.ToString().ToLower()}-degiro-etf-earnings.csv")

                File.WriteAllText(csvEarningsSharesPath, csvEarningsShares)
                File.WriteAllText(csvEarningsETFPath, csvEarningsETF)
                printfn $"\nShares earnings CSV file written to: {csvEarningsSharesPath}"
                printfn $"ETF earnings CSV file written to: {csvEarningsETFPath}\n"

        // Dividends
        let dividends = getAllDividends rows year
        printfn $"\n💰 Dividends in {year}:\n"
        printfn $"%s{getDividendsCliString dividends}"

        if outputPath.IsSome then
            let csvDividends = dividendsToCsvString dividends

            let outputFilePath = Path.Combine(outputPath.Value, $"{year}-degiro-dividends.csv")

            File.WriteAllText(outputFilePath, csvDividends)
            printfn $"Dividends CSV file written to {outputFilePath}\n"

        // Total deposits and fees
        let yearTotAdrFees = getTotalYearAdrFees rows year
        let yearTotStampDuty = getTotalYearStampDuty rows year
        let yearTotFees = getTotalYearFees rows year
        let totDeposits = getTotalDeposits rows
        let totYearDeposits = getTotalYearDeposits rows year
        let totWithdrawals = getTotalWithdrawals rows
        let totYearWithdrawals = getTotalYearWithdrawals rows year

        printfn
            $"""
💸 Tot. ADR fees in %d{year} ($): %.2f{yearTotAdrFees}
💸 Tot. Stamp Duty in %d{year} (€): %.2f{yearTotStampDuty}
💸 Tot. Degiro fees in %d{year} (€): %.2f{yearTotFees}

🏧 Tot. deposits in %d{year} (€): %.2f{totYearDeposits}
🏧 Tot. withdrawals in %d{year} (€): %.2f{totYearWithdrawals}

🏧 Tot. deposits (€): %.2f{totDeposits}
🏧 Tot. withdrawals (€): %.2f{totWithdrawals}"""

#if DEBUG
        printfn $"\nElapsed time: {timer.ElapsedMilliseconds} ms"
#endif
    with ex ->
        eprintfn $"Error: %s{ex.Message}\n%s{ex.StackTrace}"
        Environment.Exit 1

    0