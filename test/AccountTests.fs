namespace Degiro.Tests

open NUnit.Framework
open FsUnit

open Degiro
open Degiro.Account

open System

module AccountTests =

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

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

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
        txns[0] |> should equal expectedTxn

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

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

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
        txns[0] |> should equal expectedTxn

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
        30-01-2020,09:06,30-01-2020,ACME Inc,CODE123,Buy 10 ACME Inc@7.215 USD (CODE123),,USD,-72.15,USD,-72.15,c6aead59-29c2-40f4-8158-b92cc9b6867e"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

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
        txns[0] |> should equal expectedTxn

    [<Test>]
    let ``Get total deposits`` () =
        let testRows =
            header
            + """
        30-01-2019,10:50,30-01-2019,,,Deposit,,EUR,1000.00,EUR,1025.87,
        30-01-2020,10:50,30-01-2020,ACME Inc,CODE123,FX Debit,,EUR,65.57,EUR,307.98,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-01-2020,10:50,30-01-2020,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,2.00,EUR,242.41,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-06-2020,10:50,30-06-2020,,,flatex Deposit,,EUR,500.00,EUR,1051.56,
        30-07-2020,10:50,30-07-2020,ACME Inc,CODE123,Sell 10 ACME Inc@7.215 USD,,USD,72.15,USD,72.15,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-08-2020,09:06,30-08-2020,ACME Inc,CODE123,FX Credit,1.1004,USD,72.15,USD,0.00,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-12-2021,09:06,30-12-2021,,,Deposit,,EUR,700.00,EUR,1025.87,
        31-12-2021,09:06,31-12-2021,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,-2.00,EUR,305.96,c6aead59-29c2-40f4-8158-b92cc9b6867e"""

        let rows = AccountCsv.Parse(testRows).Rows
        getTotalDeposits rows |> should equal 2200.0

        getTotalYearDeposits rows 2019
        |> should equal 1000.0

        getTotalYearDeposits rows 2020
        |> should equal 500.0

        getTotalYearDeposits rows 2021
        |> should equal 700.0

    [<Test>]
    let ``Get total fees for a year`` () =
        let testRows =
            header
            + """
        01-11-2018,09:35,31-10-2018,,,DEGIRO Exchange Connection Fee 2018 (Euronext Amsterdam - EAM),,EUR,-2.50,EUR,317.04,
        01-10-2019,10:50,30-09-2019,,,DEGIRO Exchange Connection Fee 2019 (Euronext Paris - EPA),,EUR,-2.50,EUR,705.69,
        01-10-2019,10:50,30-09-2019,,,DEGIRO Exchange Connection Fee 2019 (Borsa Italiana S.p.A. - MIL),,EUR,-2.50,EUR,708.19,
        30-07-2019,10:50,30-07-2019,ACME Inc,CODE123,Sell 10 ACME Inc@7.215 USD,,USD,72.15,USD,72.15,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-08-2019,09:06,30-08-2019,ACME Inc,CODE123,FX Credit,1.1004,USD,72.15,USD,0.00,c6aead59-29c2-40f4-8158-b92cc9b6867e
        23-09-2019,15:31,23-09-2019,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,-0.50,EUR,1061.36,838de62f-2b72-428a-8c80-590ec5a7e94d
        23-09-2019,15:31,23-09-2019,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,-0.01,EUR,109961.86,838de62f-2b72-428a-8c80-590ec5a7e94d
        03-09-2019,15:32,03-09-2019,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,-0.50,EUR,12448.30,924288dd-057a-431b-86ea-af3a54ead0ca
        03-09-2019,15:32,03-09-2019,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,-0.01,EUR,12468.80,924288dd-057a-431b-86ea-af3a54ead0ca
        03-09-2019,15:31,03-09-2019,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,-0.50,EUR,1863.68,7d6141bb-e349-453f-b30d-560bffcde3e2
        03-09-2019,15:31,03-09-2019,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,-0.01,EUR,18694.18,7d6141bb-e349-453f-b30d-560bffcde3e2
        03-09-2020,15:30,03-09-2020,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,-0.50,EUR,22498.35,dd33e62e-f313-45b4-8ed6-3b8a0849dd44
        03-09-2020,15:30,03-09-2020,ACME Inc,CODE123,DEGIRO Transaction Fee,,EUR,-0.01,EUR,2248.85,dd33e62e-f313-45b4-8ed6-3b8a0849dd44"""

        let rows = AccountCsv.Parse(testRows).Rows
        getTotalYearFees rows 2018 |> should equal -2.5
        getTotalYearFees rows 2019 |> should equal -6.53
        getTotalYearFees rows 2020 |> should equal -0.51

    [<Test>]
    let ``Get earnings in period`` () =
        let txnBuyA1 =
            { Product = "Acme Inc A"
              Type = Buy
              Price = -100.0m
              Quantity = 5
              Date = DateTime(2019, 4, 4)
              ProductId = "ABC"
              ProdType = ProductType.Shares
              Fees = 2.5m
              Value = 20m
              ValueCurrency = Currency.EUR
              OrderId = Guid.NewGuid() }

        let txnBuyA2 =
            { txnBuyA1 with
                  Price = -55.0m
                  Quantity = 2
                  Date = DateTime(2019, 5, 5) }

        let txnBuyB1 =
            { Product = "Acme Inc B"
              Type = Buy
              Price = 100.0m
              Quantity = 5
              Date = DateTime(2019, 6, 6)
              ProductId = "ABC2"
              ProdType = ProductType.Shares
              Fees = 2.5m
              Value = 123.0m
              ValueCurrency = Currency.EUR
              OrderId = Guid.NewGuid() }

        let txnSellA1 =
            { txnBuyA1 with
                  Type = Sell
                  Price = 200.0m
                  Quantity = 4
                  Date = DateTime(2020, 12, 1) }

        let allTxns =
            [ txnBuyA1
              txnBuyA2
              txnBuyB1
              txnSellA1 ]

        getSellTxnsInPeriod allTxns 2019 Period.Initial
        |> should be Empty

        getSellTxnsInPeriod allTxns 2020 Period.Initial
        |> should be Empty

        getSellTxnsInPeriod allTxns 2020 Period.Later
        |> should equal [ txnSellA1 ]

        let expectedEarning =
            { Date = txnSellA1.Date
              Product = txnSellA1.Product
              ProductId = "ABC"
              Value = 105.0m
              Percent = Math.Round((105.0m / 95.0m) * 100.0m, 2) }

        getSellsEarnings [ txnSellA1 ] allTxns
        |> should equal [ expectedEarning ]

    [<Test>]
    let ``Get dividends`` () =
        let testRows =
            header
            + """
        01-09-2019,09:13,01-09-2019,ACME Inc,CODE123,Dividend,,USD,10.50,USD,8.92,
        01-04-2020,09:13,01-04-2020,ACME Inc,CODE123,Dividend,,USD,10.50,USD,8.92,
        30-06-2020,10:50,30-06-2020,,,flatex Deposit,,EUR,500.00,EUR,1051.56,
        30-07-2020,10:50,30-07-2020,ACME Inc,CODE123,Sell 10 ACME Inc@7.215 USD,,USD,72.15,USD,72.15,c6aead59-29c2-40f4-8158-b92cc9b6867e
        01-04-2020,09:13,31-03-2020,ACME Inc,CODE123,Dividend Tax,,USD,-1.58,USD,-1.58,"""

        let rows = AccountCsv.Parse(testRows).Rows

        let expectedDividend =
            { Year = 2020
              Product = "ACME Inc"
              ProductId = "CODE123"
              Value = 10.5m
              ValueTax = -1.58m
              Currency = USD }

        let allDividends = getAllDividends rows 2020
        allDividends |> should haveLength 1
        allDividends[0] |> should equal expectedDividend
