namespace Degiro.Tests

open FsUnit
open NUnit.Framework

open Degiro
open Degiro.CsvOutput
open Degiro.CliOutput

open System


module OutputTests =

    [<Test>]
    let ``Earnings to CSV string`` () =
        let earningOne =
            { Date = DateTime(2021, 1, 2)
              Product = "ACME Inc A"
              ISIN = "ABC1"
              ProdType = Shares
              Value = 123.0m
              Percent = 20.0m }

        let earningTwo =
            { earningOne with
                Product = "ACME Inc B"
                ISIN = "ABC2"
                ProdType = ETF
                Value = 500.0m
                Percent = 10.0m }

        let expectedStr =
            ("Date,Product,ISIN,Type,Value,Percent\r\n"
             + "2021-01-02,ACME Inc A,ABC1,Shares,123.0,20.0\r\n"
             + "2021-01-02,ACME Inc B,ABC2,ETF,500.0,10.0\r\n")

        earningsToCsvString [ earningOne; earningTwo ] |> should equal expectedStr

    [<Test>]
    let ``Empty earnings list to CSV string`` () =
        earningsToCsvString List.empty
        |> should equal "Date,Product,ISIN,Type,Value,Percent\r\n"

    [<Test>]
    let ``Dividends to CSV string`` () =
        let dividendOne =
            { Year = 2021
              Product = "ACME Inc A"
              ISIN = "ABC1"
              Value = 42.0m
              ValueTax = 0.50m
              Currency = USD }

        let dividendTwo =
            { dividendOne with
                Product = "ACME Inc B"
                ISIN = "ABC2"
                Value = 142.0m
                ValueTax = 0.52m
                Currency = EUR }

        let expectedStr =
            ("Product,ISIN,Value,Value Tax,Currency\r\n"
             + "ACME Inc A,ABC1,42.0,0.50,USD\r\n"
             + "ACME Inc B,ABC2,142.0,0.52,EUR\r\n")

        dividendsToCsvString [ dividendOne; dividendTwo ] |> should equal expectedStr

    [<Test>]
    let ``Empty dividends list to CSV string`` () =
        dividendsToCsvString List.Empty
        |> should equal "Product,ISIN,Value,Value Tax,Currency\r\n"

    [<Test>]
    let ``ETF buy transactions to CSV string`` () =
        let txnOne =
            { Date = DateTime(2021, 1, 2)
              Product = "ACME Inc A"
              Type = Buy
              ProdType = ETF
              ValueCurrency = EUR
              OrderId = Guid.NewGuid()
              ISIN = "ABC1"
              Quantity = 10
              Value = 123.0m
              Price = 12.3m
              Fees = 0.5m }

        let txnTwo =
            { txnOne with
                Product = "ACME Inc B"
                ISIN = "ABC2"
                Quantity = 5
                Value = 50.0m
                Price = 10.0m
                Fees = 0.2m }

        let expectedStr =
            ("Date,Product,ISIN,Quantity,Value,Price,Fees\r\n"
             + "2021-01-02,ACME Inc A,ABC1,10,123.0,12.3,0.5\r\n"
             + "2021-01-02,ACME Inc B,ABC2,5,50.0,10.0,0.2\r\n")

        etfBuysToCsvString [ txnOne; txnTwo ] |> should equal expectedStr

    [<Test>]
    let ``Empty ETF buy transactions list to CSV string`` () =
        etfBuysToCsvString List.Empty
        |> should equal "Date,Product,ISIN,Quantity,Value,Price,Fees\r\n"

    [<Test>]
    let ``Get earnings CLI string`` () =
        let earningOne =
            { Date = DateTime(2021, 1, 2)
              Product = "ACME Inc A"
              ISIN = "ABC1"
              ProdType = Shares
              Value = 100.0m
              Percent = 20m }

        let earningTwo =
            { Date = DateTime(2021, 2, 2)
              Product = "ACME Inc B"
              ISIN = "ABC2"
              ProdType = ETF
              Value = 50.0m
              Percent = 10m }

        let expectedStr1 = "Tot. P/L (€): 150.00"
        let expectedStr2 = "Avg % P/L: 15.00%"

        let outStr = getEarningsCliString [ earningOne; earningTwo ]

        outStr |> should contain expectedStr1
        outStr |> should contain expectedStr2

    [<Test>]
    let ``Get dividends CLI string`` () =
        let dividendOne =
            { Year = 2021
              Product = "ACME Inc A"
              ISIN = "ABC1"
              Value = 2.0m
              ValueTax = -0.50m
              Currency = EUR }

        let dividendTwo =
            { Year = 2021
              Product = "ACME Inc B"
              ISIN = "ABC2"
              Value = 5.0m
              ValueTax = -0.50m
              Currency = USD }

        let expectedStr1 = "Tot. net dividends in €: 1.50"
        let expectedStr2 = "Tot. net dividends in $: 4.50"
        let expectedStr3 = "Tot. net dividends in Can$: 0"
        let expectedStr4 = "Tot. net dividends in £: 0"

        let outStr = getDividendsCliString [ dividendOne; dividendTwo ]

        outStr |> should contain expectedStr1
        outStr |> should contain expectedStr2
        outStr |> should contain expectedStr3
        outStr |> should contain expectedStr4

    [<Test>]
    let ``Get ETF buy transactions CLI string`` () =
        let txnOne =
            { Date = DateTime(2021, 1, 2)
              Product = "ACME Inc A"
              Type = Buy
              ProdType = ETF
              ValueCurrency = EUR
              OrderId = Guid.NewGuid()
              ISIN = "ABC1"
              Quantity = 10
              Value = 123.0m
              Price = -12.3m
              Fees = 0.5m }

        let txnTwo =
            { txnOne with
                Product = "ACME Inc B"
                ISIN = "ABC2"
                Quantity = 5
                Value = 50.0m
                Price = -10.0m
                Fees = 0.2m }

        let outStr = getEtfBuysCliString [ txnOne; txnTwo ]

        outStr |> should contain "ACME Inc A"
        outStr |> should contain "ACME Inc B"
        outStr |> should contain $"Tot. ETF buys in €: -22.30"

    [<Test>]
    let ``Get stock change CLI string`` () =
        let stockChange: StockChange =
            { Date = DateTime(2022, 3, 4)
              IsinBefore = "ABC-Before"
              IsinAfter = "ABC-After"
              Multiplier = 3
              ProductAfter = "ABC"
              ProductBefore = "ABC Before" }

        let outStr = getStockChangesCliString [| stockChange |]

        outStr |> should contain "ABC-After"


    [<EntryPoint>]
    let main _ = 0