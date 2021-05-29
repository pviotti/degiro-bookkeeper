module DegiroTests

open NUnit.Framework
open FsUnit

open Degiro
open Degiro.Account

open System

let header =
    """Date,Time,Value date,Product,ISIN,Description,FX,Change,,Balance,,Order ID"""

[<Test>]
let ``BuildTxn with many description rows in USD`` () =
    let testRows =
        header
        + """
        16-02-2021,15:33,16-02-2021,ACME INC. COM,CODE123,FX Credit,1.2094,USD,803.25,USD,0.00,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:33,16-02-2021,ACME INC. COM,CODE123,FX Debit,,EUR,-664.18,EUR,-0.83,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:33,16-02-2021,ACME INC. COM,CODE123,DEGIRO Transaction Fee,,EUR,-0.28,EUR,663.35,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:33,16-02-2021,ACME INC. COM,CODE123,Buy 85 ACME INC. COM@9.45 USD (CODE123),,USD,-803.25,USD,-803.25,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:32,16-02-2021,ACME INC. COM,CODE123,FX Credit,1.2092,USD,9.45,USD,0.00,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:32,16-02-2021,ACME INC. COM,CODE123,FX Debit,,EUR,-7.82,EUR,663.63,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:32,16-02-2021,ACME INC. COM,CODE123,Buy 1 ACME INC. COM@9.45 USD (CODE123),,USD,-9.45,USD,-9.45,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:30,16-02-2021,ACME INC. COM,CODE123,FX Credit,1.2092,USD,37.80,USD,0.00,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:30,16-02-2021,ACME INC. COM,CODE123,FX Debit,,EUR,-31.26,EUR,671.45,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:30,16-02-2021,ACME INC. COM,CODE123,DEGIRO Transaction Fee,,EUR,-0.01,EUR,702.71,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:30,16-02-2021,ACME INC. COM,CODE123,DEGIRO Transaction Fee,,EUR,-0.50,EUR,702.72,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:30,16-02-2021,ACME INC. COM,CODE123,Buy 4 ACME INC. COM@9.45 USD (CODE123),,USD,-37.80,USD,-37.80,9f8a14c4-ad5c-4a92-99af-60a69e8b584e"""

    let account = AccountCsv.Parse(testRows)
    let txnsGrouped = getAllTxnRowsGrouped account

    let txns =
        Seq.map buildTxn txnsGrouped |> Seq.toList

    let expectedTxn =
        { Date = DateTime(2021, 2, 16, 15, 30, 0)
          Type = Buy
          Product = "ACME INC. COM"
          ProductId = "CODE123"
          ProdType = ProductType.Shares
          Quantity = 90
          Price = -703.26m
          Value = 9.45m
          ValueCurrency = Currency.USD
          Fees = -0.79m
          OrderId = Guid.Parse("9f8a14c4-ad5c-4a92-99af-60a69e8b584e") }

    txns |> should haveLength 1
    txns.[0] |> should equal expectedTxn

[<Test>]
let ``BuildTxn with many description rows in EUR`` () =
    let testRows =
        header
        + """
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,DEGIRO Transaction Fee,,EUR,-0.01,EUR,65.43,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,DEGIRO Transaction Fee,,EUR,-2.00,EUR,65.44,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,DEGIRO Transaction Fee,,EUR,-0.01,EUR,67.44,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,Buy 2 PRODUCT IN EUR@9.598 EUR (CODE321),,EUR,-19.20,EUR,67.45,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,Buy 3 PRODUCT IN EUR@9.598 EUR (CODE321),,EUR,-28.79,EUR,86.65,e30677c2-cf9e-4dac-8405-f2361f60e0fd"""

    let account = AccountCsv.Parse(testRows)
    let txnsGrouped = getAllTxnRowsGrouped account

    let txns =
        Seq.map buildTxn txnsGrouped |> Seq.toList

    let expectedTxn =
        { Date = DateTime(2020, 6, 8, 9, 5, 0)
          Type = Buy
          Product = "PRODUCT IN EUR"
          ProductId = "CODE321"
          ProdType = ProductType.Shares
          Quantity = 5
          Price = -19.20m + -28.79m
          Value = 9.598m
          ValueCurrency = Currency.EUR
          Fees = -2.02m
          OrderId = Guid.Parse("e30677c2-cf9e-4dac-8405-f2361f60e0fd") }

    txns |> should haveLength 1
    txns.[0] |> should equal expectedTxn

[<Test>]
let ``BuiltTxn with cancelled and compensated transactions`` () =
    // Sometimes, Degiro executes a Buy order and then cancel it by
    // issuing a Sell transaction of the same exact amount with the same OrderId
    let testRows =
        header
        + """
30-01-2020,10:50,30-01-2020,ACME Inc,CODE123,FX Credit,1.1031,USD,-72.15,USD,0.00,c6aead59-29c2-40f4-8158-b92cc9b6867e
30-01-2020,10:50,30-01-2020,ACME Inc,CODE123,FX Debit,,EUR,65.57,EUR,307.98,c6aead59-29c2-40f4-8158-b92cc9b6867e
30-01-2020,10:50,30-01-2020,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,2.00,EUR,242.41,c6aead59-29c2-40f4-8158-b92cc9b6867e
30-01-2020,10:50,30-01-2020,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,0.02,EUR,240.41,c6aead59-29c2-40f4-8158-b92cc9b6867e
30-01-2020,10:50,30-01-2020,ACME Inc,CODE123,Sell 10 ACME Inc@7.215 USD,,USD,72.15,USD,72.15,c6aead59-29c2-40f4-8158-b92cc9b6867e
30-01-2020,09:06,30-01-2020,ACME Inc,CODE123,FX Credit,1.1004,USD,72.15,USD,0.00,c6aead59-29c2-40f4-8158-b92cc9b6867e
30-01-2020,09:06,30-01-2020,ACME Inc,CODE123,FX Debit,,EUR,-65.57,EUR,240.39,c6aead59-29c2-40f4-8158-b92cc9b6867e
30-01-2020,09:06,30-01-2020,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,-2.00,EUR,305.96,c6aead59-29c2-40f4-8158-b92cc9b6867e
30-01-2020,09:06,30-01-2020,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,-0.02,EUR,307.96,c6aead59-29c2-40f4-8158-b92cc9b6867e
30-01-2020,09:06,30-01-2020,ACME Inc,CODE123,Buy 10 ACME Inc@7.215 USD (CODE123),,USD,-72.15,USD,-72.15,c6aead59-29c2-40f4-8158-b92cc9b6867e
"""

    let account = AccountCsv.Parse(testRows)
    let txnsGrouped = getAllTxnRowsGrouped account

    let txns =
        Seq.map buildTxn txnsGrouped |> Seq.toList

    let expectedTxn =
        { Date = DateTime(2020, 1, 30, 9, 6, 0)
          Type = Buy
          Product = "ACME Inc"
          ProductId = "CODE123"
          ProdType = ProductType.Shares
          Quantity = 0
          Price = 0.0m
          Value = 0.0m
          ValueCurrency = Currency.USD
          Fees = 0.0m
          OrderId = Guid.Parse("c6aead59-29c2-40f4-8158-b92cc9b6867e") }

    txns |> should haveLength 1
    txns.[0] |> should equal expectedTxn

[<Test>]
let ``Parse row without price value`` () =
    let testRows =
        header
        + """
11-09-2020,15:56,11-09-2020,ACME Inc,CODE123,"Money Market fund conversion: Sell 0.009917 at 9,923.3034 EUR",,,,EUR,663.86,
"""

    let rows = AccountCsv.Parse(testRows).Rows
    Seq.length rows |> should equal 1

[<EntryPoint>]
let main _ = 0
