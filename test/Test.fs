module StockWatchTest

open NUnit.Framework
open FsUnit

open Degiro
open Degiro.Account

open System

let header = """Date,Time,Value date,Product,ISIN,Description,FX,Change,,Balance,,Order ID"""

[<Test>]
let ``BuildTxn with many description rows in USD`` () =
    let testRows = header + """
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
    let txns = Seq.map buildTxn txnsGrouped |> Seq.toList

    let expectedTxn =  { Date = DateTime(2021, 2, 16, 15, 33, 00);
                       Type = Buy;
                       Product = "ACME INC. COM";
                       ProductId = "CODE123";
                       ProdType = ProductType.Shares;
                       Quantity = 90;
                       Price = -703.26;
                       Value = 9.45;
                       ValueCurrency = Currency.USD;
                       Fees = -0.79;
                       OrderId = Guid.Parse("9f8a14c4-ad5c-4a92-99af-60a69e8b584e");}

    txns |> should haveLength 1
    txns.[0] |> should equal expectedTxn

[<Test>]
let ``BuildTxn with many description rows in EUR`` () =
    let testRows = header + """
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,DEGIRO Transaction Fee,,EUR,-0.01,EUR,65.43,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,DEGIRO Transaction Fee,,EUR,-2.00,EUR,65.44,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,DEGIRO Transaction Fee,,EUR,-0.01,EUR,67.44,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,Buy 2 PRODUCT IN EUR@9.598 EUR (CODE321),,EUR,-19.20,EUR,67.45,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,Buy 3 PRODUCT IN EUR@9.598 EUR (CODE321),,EUR,-28.79,EUR,86.65,e30677c2-cf9e-4dac-8405-f2361f60e0fd"""

    let account = AccountCsv.Parse(testRows)
    let txnsGrouped = getAllTxnRowsGrouped account
    let txns = Seq.map buildTxn txnsGrouped |> Seq.toList

    let expectedTxn =  { Date = DateTime(2020, 6, 8, 9, 05, 00);
                       Type = Buy;
                       Product = "PRODUCT IN EUR";
                       ProductId = "CODE321";
                       ProdType = ProductType.Shares;
                       Quantity = 5;
                       Price = -19.20 + -28.79;
                       Value = ((3.0 * 9.598) + (2.0 * 9.598)) / 5.0;
                       ValueCurrency = Currency.EUR;
                       Fees = -2.0 + -0.01 + -0.01;
                       OrderId = Guid.Parse("e30677c2-cf9e-4dac-8405-f2361f60e0fd")}

    txns |> should haveLength 1
    txns.[0] |> should equal expectedTxn




[<EntryPoint>]
let main _ = 0