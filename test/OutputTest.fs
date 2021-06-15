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
              Value = 123.0m
              Percent = 20.0m }

        let earningTwo =
            { earningOne with
                  Product = "ACME Inc B"
                  Value = 500.0m
                  Percent = 10.0m }

        let expectedStr = """Date,Product,Value,Percent
2021-01-02,ACME Inc A,123.0,20.0
2021-01-02,ACME Inc B,500.0,10.0
"""

        earningsToCsvString [ earningOne
                              earningTwo ]
        |> should equal expectedStr

    [<TestCase>]
    let ``Empty earnings list to CSV string`` () =
        earningsToCsvString List.empty
        |> should equal ("Date,Product,Value,Percent" + Environment.NewLine)


    [<TestCase>]
    let ``Dividends to CSV string`` () =
        let dividendOne =
            { Year = 2021
              Product = "ACME Inc A"
              Value = 42.0m
              ValueTax = 0.50m
              Currency = USD }

        let dividendTwo =
            { dividendOne with
                  Product = "ACME Inc B"
                  Value = 142.0m
                  ValueTax = 0.52m
                  Currency = EUR }

        let expectedStr = """Year,Product,Value,Value Tax,Currency
2021,ACME Inc A,42.0,0.50,USD
2021,ACME Inc B,142.0,0.52,EUR
"""

        dividendsToCsvString [ dividendOne
                               dividendTwo ]
        |> should equal expectedStr


    [<TestCase>]
    let ``Empty dividends list to CSV string`` () =
        dividendsToCsvString List.Empty
        |> should
            equal
            ("Year,Product,Value,Value Tax,Currency"
             + Environment.NewLine)

    [<TestCase>]
    let ``Get earnings CLI string`` () =
        let earningOne =
            { Date = DateTime(2021, 1, 2)
              Product = "ACME Inc A"
              Value = 100.0m
              Percent = 20m }

        let earningTwo =
            { Date = DateTime(2021, 2, 2)
              Product = "ACME Inc B"
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
              Value = 2.0m
              ValueTax = -0.50m
              Currency = EUR }

        let dividendTwo =
            { Year = 2021
              Product = "ACME Inc B"
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

    [<EntryPoint>]
    let main _ = 0
