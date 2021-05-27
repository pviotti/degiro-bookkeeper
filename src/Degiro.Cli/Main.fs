open System
open System.IO

open Degiro
open Degiro.Account

[<EntryPoint>]
let main argv =

    if argv.Length < 3 then
        eprintfn "Error: missing parameter"
        eprintfn "Usage: stock-watcher <path/to/statement.csv> <year> [<1,2>]"
        Environment.Exit 1

    //let [<Literal>] csvFile = __SOURCE_DIRECTORY__ + "/account.csv"
    // let csvFile = __SOURCE_DIRECTORY__ + string (Path.DirectorySeparatorChar) + argv.[2]
    let originalCsvContent = File.ReadAllText argv.[0]
    let cleanCsv, malformed = cleanCsv originalCsvContent

    if malformed then
        File.WriteAllText((argv.[0] + "-clean.csv"), cleanCsv)

    let account = AccountCsv.Parse(cleanCsv)
    let year = int argv.[1]

    let period =
        if argv.Length <= 2 then
            Period.All
        else
            enum<Period> (int argv.[2])

    let timer = new Diagnostics.Stopwatch()
    timer.Start()
    let txnsGrouped = getAllTxnRowsGrouped account
    let txns = Seq.map buildTxn txnsGrouped |> Seq.toList
    let sellsInPeriod = getSellTxnsInPeriod txns year period

    if List.isEmpty sellsInPeriod then
        printfn $"No sells recorded in %d{year}, period %A{period}."
        Environment.Exit 0

    printfn "%-10s %-40s %7s %8s\n%s" "Date" "Product" "P/L (€)" "P/L %" (String.replicate 68 "-")

    let printEarning (e: Earning) =
        printfn "%s %-40s %7.2f %7.1f%%" (e.Date.ToString("yyyy-MM-dd")) e.Product e.Value e.Percent

    let periodEarnings = getSellsEarnings sellsInPeriod txns

    periodEarnings
    |> List.iter printEarning

    let periodTotalEarnings =
        periodEarnings |> Seq.sumBy (fun x -> x.Value)

    let periodAvgPercEarnings =
        periodEarnings
        |> List.averageBy (fun x -> x.Percent)

    printfn $"\nTot. P/L (€): %.2f{periodTotalEarnings}"
    printfn $"Avg %% P/L: %.2f{periodAvgPercEarnings}%%"

    let yearTotFees = getTotalYearFees account year
    printfn $"\nTot. DeGiro fees in %d{year} (€): %.2f{yearTotFees}"

    let totDeposits = getTotalDeposits account
    let totYearDeposits = getTotalYearDeposits account year
    printfn $"\nTot. deposits (€): %.2f{totDeposits}"
    printfn $"Tot. deposits in %d{year} (€): %.2f{totYearDeposits}"
    printfn $"\nElapsed time: {timer.ElapsedMilliseconds} ms"
    0