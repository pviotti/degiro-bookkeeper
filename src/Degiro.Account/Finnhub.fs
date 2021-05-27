open System
open ThreeFourteen.Finnhub.Client

(*
    Finnhub API (free):
     - symbol lookup - https://finnhub.io/api/v1/search?q=apple&token=c0p7kp748v6rvej4etcg
     - company profile - https://finnhub.io/api/v1/stock/profile2?symbol=AAPL&token=c0p7kp748v6rvej4etcg
     - company news - https://finnhub.io/api/v1/company-news?symbol=AAPL&from=2020-04-30&to=2020-05-01&token=c0p7kp748v6rvej4etcg
     - news sentiment - https://finnhub.io/api/v1/news-sentiment?symbol=AAPL&token=c0p7kp748v6rvej4etcg
     - basic finantial - https://finnhub.io/api/v1/stock/metric?symbol=AAPL&metric=all&token=c0p7kp748v6rvej4etcg
     - IPOs calendar - https://finnhub.io/api/v1/calendar/ipo?from=2020-01-01&to=2020-04-30&token=c0p7kp748v6rvej4etcg
     - recommendations trends - https://finnhub.io/api/v1/stock/recommendation?symbol=AAPL&token=c0p7kp748v6rvej4etcg
     - earnings calendar - https://finnhub.io/api/v1/calendar/earnings?from=2021-01-12&to=2021-03-15&token=c0p7kp748v6rvej4etcg
     - quote - https://finnhub.io/api/v1/quote?symbol=AAPL&token=c0p7kp748v6rvej4etcg
*)

//let API_KEY = "sandbox_c0p7kp748v6rvej4etd0"
let API_KEY = "c0p7kp748v6rvej4etcg"

let callApi () =
    async {
        let client = FinnhubClient(API_KEY)
        let! company = client.Stock.GetCompany "AAPL" |> Async.AwaitTask
        let! recommendations = client.Stock.GetRecommendationTrends "AAPL" |> Async.AwaitTask
        printfn "%s - %s" company.Ticker company.Exchange
        printfn "%A" company
        printfn "%d %A" recommendations.Rank recommendations
    }


// [<EntryPoint>]
// let main argv =
//     callApi() |> Async.RunSynchronously
//     0 