#r "nuget: FSharp.Data"

open System
open System.Threading
open System.Globalization

// change printed format of specific types in fsi
fsi.AddPrinter<DateTime>(fun d -> d.ToShortDateString())
fsi.AddPrinter<TimeSpan>(fun time -> time.ToString("c"))

//type Transaction = {
//    Date: DateTime
//    Product: string
//    Fees: float
//    Price: float
//    OrderId: string
//}

Thread.CurrentThread.CurrentCulture = CultureInfo("en-IRL")

let [<Literal>] CsvFilePath = __SOURCE_DIRECTORY__ + "/account.csv"

// Culture is used to automatically parse dates in the dd-mm-YYY format as `Option<DateTime>` type
type Account = CsvProvider<CsvFilePath, Schema=",,,,,,,,Price,,,OrderId", Culture="en-IRL">
let account : Account = Account.Load(CsvFilePath)

account.Rows
let firstRow : Account.Row = account.Rows |> Seq.head
firstRow.Date

let dateToString (date: Option<DateTime>) =
    match date with
    | Some(x) -> x.ToString "yyyy-MM-dd"
    | None -> "None"
    
let isSellToString isSell = if isSell then "SELL" else "BUY"

let buildTxn (txn: string * seq<Account.Row>) =
    let records = snd txn
    try
        let descRow: Account.Row = records |> Seq.find (fun x -> (x.Description.StartsWith "Sell" || x.Description.StartsWith "Buy"))
        let isSell = descRow.Description.StartsWith "Sell"
        let degiroFees = records
                         |> Seq.filter (fun x -> x.Description.Equals "DEGIRO Transaction Fee")
                         |> Seq.sumBy (fun x -> x.Price)
        let price = match descRow.Description.Contains "EUR" with
                    | true -> descRow.Price
                    | false -> let fxRow = match isSell with
                                           | true -> records |> Seq.find (fun x -> x.Description.Equals "FX Credit")
                                           | false -> records |> Seq.find (fun x -> x.Description.Equals "FX Debit")
                               fxRow.Price
        printfn "%s %s\t%s\t%f\t%f" (dateToString descRow.Date) descRow.Product (isSellToString isSell) price degiroFees
    with ex ->
        printfn "Error: %A" (Seq.head records)


let txns : seq<string * seq<Account.Row>> = account.Rows
                                            |> Seq.filter (fun x -> not (String.IsNullOrEmpty x.OrderId))
                                            |> Seq.groupBy (fun x -> x.OrderId.[0 .. 18])
txns
txns |> Seq.iter buildTxn

let depositTot = account.Rows
                 |> Seq.filter (fun x -> x.Description.Equals "Deposit")
                 |> Seq.sumBy (fun x -> x.Price)

let filtered = account.Rows
                |> Seq.filter (fun x -> not (System.String.IsNullOrEmpty x.Product))
                |> Seq.map (fun x -> (x.Date, x.Product, x.Description))
                |> Seq.groupBy (fun (date: Option<DateTime>, product: string, desc: string) -> product)
//    |> Seq.filter (fun x -> !x.Product.Contains("Flatex") )
filtered