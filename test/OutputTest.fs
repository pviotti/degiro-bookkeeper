namespace Degiro.Tests

open FsUnit
open NUnit.Framework

open Degiro
open Degiro.CsvOutput
open Degiro.CliOutput

open System


module OutputTests =

    [<TestCase>]
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
            """Date,Product,ISIN,Type,Value,Percent
2021-01-02,ACME Inc A,ABC1,Shares,123.0,20.0
2021-01-02,ACME Inc B,ABC2,ETF,500.0,10.0
"""

        earningsToCsvString [ earningOne
                              earningTwo ]
        |> should equal expectedStr

    [<TestCase>]
    let ``Empty earnings list to CSV string`` () =
        earningsToCsvString List.empty
        |> should
            equal
            ("Date,Product,ISIN,Type,Value,Percent"
             + Environment.NewLine)


    [<TestCase>]
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
            """Product,ISIN,Value,Value Tax,Currency
ACME Inc A,ABC1,42.0,0.50,USD
ACME Inc B,ABC2,142.0,0.52,EUR
"""

        dividendsToCsvString [ dividendOne
                               dividendTwo ]
        |> should equal expectedStr


    [<TestCase>]
    let ``Empty dividends list to CSV string`` () =
        dividendsToCsvString List.Empty
        |> should
            equal
            ("Product,ISIN,Value,Value Tax,Currency"
             + Environment.NewLine)

    [<TestCase>]
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

        let outStr =
            getEarningsCliString [ earningOne
                                   earningTwo ]

        outStr |> should contain expectedStr1
        outStr |> should contain expectedStr2

    [<TestCase>]
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

        let outStr =
            getDividendsCliString [ dividendOne
                                    dividendTwo ]

        outStr |> should contain expectedStr1
        outStr |> should contain expectedStr2

    [<TestCase>]
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