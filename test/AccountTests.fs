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
        16-02-2021,15:33,16-02-2021,ACME INC. COM,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-0.28,EUR,663.35,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:33,16-02-2021,ACME INC. COM,CODE123,Buy 85 ACME INC. COM@9.45 USD (CODE123),,USD,-803.25,USD,-803.25,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:32,16-02-2021,ACME INC. COM,CODE123,FX Credit,1.2092,USD,9.45,USD,0.00,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:32,16-02-2021,ACME INC. COM,CODE123,FX Debit,,EUR,-7.82,EUR,663.63,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:32,16-02-2021,ACME INC. COM,CODE123,Buy 1 ACME INC. COM@9.45 USD (CODE123),,USD,-9.45,USD,-9.45,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:30,16-02-2021,ACME INC. COM,CODE123,FX Credit,1.2092,USD,37.80,USD,0.00,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:30,16-02-2021,ACME INC. COM,CODE123,FX Debit,,EUR,-31.26,EUR,671.45,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:30,16-02-2021,ACME INC. COM,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-0.01,EUR,702.71,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:30,16-02-2021,ACME INC. COM,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,702.72,9f8a14c4-ad5c-4a92-99af-60a69e8b584e
        16-02-2021,15:30,16-02-2021,ACME INC. COM,CODE123,Buy 4 ACME INC. COM@9.45 USD (CODE123),,USD,-37.80,USD,-37.80,9f8a14c4-ad5c-4a92-99af-60a69e8b584e"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

        let txns = Seq.map buildTxn txnsGrouped |> Seq.toList

        let expectedTxn =
            { Date = DateTime(2021, 2, 16, 15, 30, 0)
              Type = Buy
              Product = "ACME INC. COM"
              ISIN = "CODE123"
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
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,DEGIRO Transaction and/or third party fees,,EUR,-0.01,EUR,65.43,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,DEGIRO Transaction and/or third party fees,,EUR,-2.00,EUR,65.44,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,DEGIRO Transaction and/or third party fees,,EUR,-0.01,EUR,67.44,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,Buy 2 PRODUCT IN EUR@9.598 EUR (CODE321),,EUR,-19.20,EUR,67.45,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,PRODUCT IN EUR,CODE321,Buy 3 PRODUCT IN EUR@9.598 EUR (CODE321),,EUR,-28.79,EUR,86.65,e30677c2-cf9e-4dac-8405-f2361f60e0fd"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

        let txns = Seq.map buildTxn txnsGrouped |> Seq.toList

        let expectedTxn =
            { Date = DateTime(2020, 6, 8, 9, 5, 0)
              Type = Buy
              Product = "PRODUCT IN EUR"
              ISIN = "CODE321"
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
    let ``BuildTxn with ETF`` () =
        let realEtfDescr =
            [ "VANGUARD FTSE AW"
              "SPDR S&P 500"
              "ISHARES S&P 500"
              "iSHR ESTX50 B A"
              "LYXOR ETF CAC 40" ]

        let rndEtfDesc = realEtfDescr |> List.randomChoice

        let testRows =
            header
            + $"""
        08-06-2020,09:05,08-06-2020,{rndEtfDesc},CODE321,DEGIRO Transaction and/or third party fees,,EUR,-2.00,EUR,65.44,e30677c2-cf9e-4dac-8405-f2361f60e0fd
        08-06-2020,09:05,08-06-2020,{rndEtfDesc},CODE321,Buy 2 {rndEtfDesc}@9.598 EUR (CODE321),,EUR,-19.20,EUR,67.45,e30677c2-cf9e-4dac-8405-f2361f60e0fd"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

        let txns = Seq.map buildTxn txnsGrouped |> Seq.toList

        let expectedTxn =
            { Date = DateTime(2020, 6, 8, 9, 5, 0)
              Type = Buy
              Product = rndEtfDesc
              ISIN = "CODE321"
              ProdType = ProductType.ETF
              Quantity = 2
              Price = -19.20m
              Value = 9.598m
              ValueCurrency = Currency.EUR
              Fees = -2.00m
              OrderId = Guid.Parse("e30677c2-cf9e-4dac-8405-f2361f60e0fd") }

        txns |> should haveLength 1
        txns[0] |> should equal expectedTxn


    [<Test>]
    let ``BuildTxn with thousands separator`` () =
        let testRows =
            header
            + $"""
        05-03-2021,15:30,05-03-2021,COSTOSO INC. CLASS A S,ABC123456,FX Credit,1.1929,USD,1154.97,USD,0.00,a7f91cfa-6058-4d77-a6b9-ebb0c61487b8
        05-03-2021,15:30,05-03-2021,COSTOSO INC. CLASS A S,ABC123456,FX Debit,,EUR,-968.20,EUR,-965.87,a7f91cfa-6058-4d77-a6b9-ebb0c61487b8
        05-03-2021,15:30,05-03-2021,COSTOSO INC. CLASS A S,ABC123456,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,2.33,a7f91cfa-6058-4d77-a6b9-ebb0c61487b8
        05-03-2021,15:30,05-03-2021,COSTOSO INC. CLASS A S,ABC123456,"Buy 1 COSTOSO INC. CLASS A S@1,154.97 USD (ABC123456)",,USD,-1154.97,USD,-1154.96,a7f91cfa-6058-4d77-a6b9-ebb0c61487b8"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

        let txns = Seq.map buildTxn txnsGrouped |> Seq.toList

        let expectedTxn =
            { Date = DateTime(2021, 3, 5, 15, 30, 0)
              Type = Buy
              Product = "COSTOSO INC. CLASS A S"
              ISIN = "ABC123456"
              ProdType = ProductType.Shares
              Quantity = 1
              Price = -968.20m
              Value = 1154.97m
              ValueCurrency = Currency.USD
              Fees = -0.50m
              OrderId = Guid.Parse("a7f91cfa-6058-4d77-a6b9-ebb0c61487b8") }

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
        30-01-2020,10:50,30-01-2020,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,2.00,EUR,242.41,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-01-2020,10:50,30-01-2020,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,0.02,EUR,240.41,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-01-2020,10:50,30-01-2020,ACME Inc,CODE123,Sell 10 ACME Inc@7.215 USD,,USD,72.15,USD,72.15,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-01-2020,09:06,30-01-2020,ACME Inc,CODE123,FX Credit,1.1004,USD,72.15,USD,0.00,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-01-2020,09:06,30-01-2020,ACME Inc,CODE123,FX Debit,,EUR,-65.57,EUR,240.39,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-01-2020,09:06,30-01-2020,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-2.00,EUR,305.96,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-01-2020,09:06,30-01-2020,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-0.02,EUR,307.96,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-01-2020,09:06,30-01-2020,ACME Inc,CODE123,Buy 10 ACME Inc@7.215 USD (CODE123),,USD,-72.15,USD,-72.15,c6aead59-29c2-40f4-8158-b92cc9b6867e"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

        let txns = Seq.map buildTxn txnsGrouped |> Seq.toList

        let expectedTxn =
            { Date = DateTime(2020, 1, 30, 9, 6, 0)
              Type = Buy
              Product = "ACME Inc"
              ISIN = "CODE123"
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
        30-01-2020,10:50,30-01-2020,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,2.00,EUR,242.41,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-06-2020,10:50,30-06-2020,,,flatex Deposit,,EUR,500.00,EUR,1051.56,
        30-07-2020,10:50,30-07-2020,ACME Inc,CODE123,Sell 10 ACME Inc@7.215 USD,,USD,72.15,USD,72.15,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-08-2020,09:06,30-08-2020,ACME Inc,CODE123,FX Credit,1.1004,USD,72.15,USD,0.00,c6aead59-29c2-40f4-8158-b92cc9b6867e
        30-12-2021,09:06,30-12-2021,,,Deposit,,EUR,700.00,EUR,1025.87,
        31-12-2021,09:06,31-12-2021,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-2.00,EUR,305.96,c6aead59-29c2-40f4-8158-b92cc9b6867e"""

        let rows = AccountCsv.Parse(testRows).Rows
        getTotalDeposits rows |> should equal 2200.0

        getTotalYearDeposits rows 2019 |> should equal 1000.0

        getTotalYearDeposits rows 2020 |> should equal 500.0

        getTotalYearDeposits rows 2021 |> should equal 700.0


    [<Test>]
    let ``Get total withdrawals`` () =
        let testRows =
            header
            + """
        12-05-2015,16:20,13-05-2015,,,Processed Flatex Withdrawal,,EUR,500.00,EUR,507.65,
        12-05-2015,16:20,13-05-2015,,,flatex Withdrawal,,EUR,-5000.00,EUR,-1492.35,
        12-05-2015,10:50,30-01-2020,ACME Inc,CODE123,FX Debit,,EUR,65.57,EUR,307.98,c6aead59-29c2-40f4-8158-b92cc9b6867e
        12-05-2015,10:50,30-01-2020,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,2.00,EUR,242.41,c6aead59-29c2-40f4-8158-b92cc9b6867e
        11-05-2015,17:08,11-05-2015,,,Processed Flatex Withdrawal,,EUR,-500.00,EUR,507.65,
        12-05-2016,16:20,13-05-2015,,,Processed Flatex Withdrawal,,EUR,1540.00,EUR,507.65,
        12-05-2016,16:20,13-05-2015,,,flatex Withdrawal,,EUR,-1540.00,EUR,-1492.35,
        11-05-2016,17:08,11-05-2015,,,Processed Flatex Withdrawal,,EUR,-1540.00,EUR,507.65,
        30-01-2019,10:50,30-01-2019,,,Deposit,,EUR,1000.00,EUR,1025.87,
        12-05-2019,16:20,13-05-2015,,,Processed Flatex Withdrawal,,EUR,1000.00,EUR,507.65,
        12-05-2019,16:20,13-05-2015,,,flatex Withdrawal,,EUR,-1000.00,EUR,-1492.35,
        11-05-2019,17:08,11-05-2015,,,Processed Flatex Withdrawal,,EUR,-1000.00,EUR,507.65,
        30-06-2020,10:50,30-06-2020,,,flatex Deposit,,EUR,500.00,EUR,1051.56,"""

        let rows = AccountCsv.Parse(testRows).Rows

        getTotalWithdrawals rows |> should equal (500.0 + 1540.0 + 1000.0)

        getTotalYearWithdrawals rows 2015 |> should equal 500.0

        getTotalYearWithdrawals rows 2016 |> should equal 1540.0

        getTotalYearWithdrawals rows 2019 |> should equal 1000.0


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
        23-09-2019,15:31,23-09-2019,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,1061.36,838de62f-2b72-428a-8c80-590ec5a7e94d
        23-09-2019,15:31,23-09-2019,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-0.01,EUR,109961.86,838de62f-2b72-428a-8c80-590ec5a7e94d
        03-09-2019,15:32,03-09-2019,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,12448.30,924288dd-057a-431b-86ea-af3a54ead0ca
        03-09-2019,15:32,03-09-2019,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-0.01,EUR,12468.80,924288dd-057a-431b-86ea-af3a54ead0ca
        03-09-2019,15:31,03-09-2019,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,1863.68,7d6141bb-e349-453f-b30d-560bffcde3e2
        03-09-2019,15:31,03-09-2019,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-0.01,EUR,18694.18,7d6141bb-e349-453f-b30d-560bffcde3e2
        03-09-2020,15:30,03-09-2020,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,22498.35,dd33e62e-f313-45b4-8ed6-3b8a0849dd44
        03-09-2020,15:30,03-09-2020,ACME Inc,CODE123,DEGIRO Transaction and/or third party fees,,EUR,-0.01,EUR,2248.85,dd33e62e-f313-45b4-8ed6-3b8a0849dd44"""

        let rows = AccountCsv.Parse(testRows).Rows
        getTotalYearFees rows 2018 |> should equal -2.5
        getTotalYearFees rows 2019 |> should equal -6.53
        getTotalYearFees rows 2020 |> should equal -0.51


    [<Test>]
    let ``Get total Spanish FTT for a year`` () =
        let testRows =
            header
            + """
        12-07-2023,15:14,12-07-2023,INT.AIRL.GRP,ES0177542018,Spanish Transaction Tax,,EUR,-18.00,EUR,92.46,asdfghjk-asdf-asdf-asdf-asdfghlkjhgf
        12-07-2023,15:14,12-07-2023,INT.AIRL.GRP,ES0177542018,Spanish Transaction Tax,,EUR,-2.00,EUR,110.46,asdfghjk-asdf-asdf-asdf-asdfghlkjhgf
        12-07-2023,15:14,12-07-2023,INT.AIRL.GRP,ES0177542018,DEGIRO Transaction and/or third party fees,,EUR,-4.90,EUR,112.46,asdfghjk-asdf-asdf-asdf-asdfghlkjhgf
        12-07-2023,15:14,12-07-2023,INT.AIRL.GRP,ES0177542018,"Buy 4,928 INT.AIRL.GRP@1.826 EUR (ES0177542018)",,EUR,-8998.53,EUR,117.36,asdfghjk-asdf-asdf-asdf-asdfghlkjhgf
        12-07-2023,15:14,12-07-2023,INT.AIRL.GRP,ES0177542018,Buy 548 INT.AIRL.GRP@1.826 EUR (ES0177542018),,EUR,-1000.65,EUR,9115.89,asdfghjk-asdf-asdf-asdf-asdfghlkjhgf"""

        let rows = AccountCsv.Parse(testRows).Rows
        getTotalYearSpanishFTT rows 2023 |> should equal -20.0


    [<Test>]
    let ``Get earnings in period`` () =
        let txnBuyA1 =
            { Product = "Acme Inc A"
              Type = Buy
              Price = -100.0m
              Quantity = 5
              Date = DateTime(2019, 4, 4)
              ISIN = "ABC"
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
              ISIN = "ABC2"
              ProdType = ProductType.ETF
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

        let allTxns = [ txnBuyA1; txnBuyA2; txnBuyB1; txnSellA1 ]

        getSellTxnsInPeriod allTxns 2019 Period.Initial |> should be Empty

        getSellTxnsInPeriod allTxns 2020 Period.Initial |> should be Empty

        getSellTxnsInPeriod allTxns 2020 Period.Later |> should equal [ txnSellA1 ]

        let expectedEarning =
            { Date = txnSellA1.Date
              Product = txnSellA1.Product
              ISIN = "ABC"
              ProdType = Shares
              Value = 105.0m
              Percent = Math.Round((105.0m / 95.0m) * 100.0m, 2) }

        getSellsEarnings [ txnSellA1 ] allTxns Map.empty
        |> should equal [ expectedEarning ]


    [<Test>]
    let ``Get earnings of stock that changed name between buy and sell transactions`` () =
        let testRows =
            header
            + """
19-11-2020,10:18,18-11-2020,FLATEX EURO BANKACCOUNT,NLFLATEXACNT,Degiro Cash Sweep Transfer,,EUR,-1777.22,EUR,4007.15,
18-11-2020,15:30,18-11-2020,BRAND NEW NAME,US12008C1234,FX Debit,1.1352,USD,-2018.24,USD,0.00,cd5560a8-c903-4ddd-aacf-ad1319b6c588
18-11-2020,15:30,18-11-2020,BRAND NEW NAME,US12008C1234,FX Credit,,EUR,1777.82,EUR,5784.37,cd5560a8-c903-4ddd-aacf-ad1319b6c588
18-11-2020,15:30,18-11-2020,BRAND NEW NAME,US12008C1234,DEGIRO Transaction and/or third party fees,,EUR,-0.10,EUR,4006.55,cd5560a8-c903-4ddd-aacf-ad1319b6c588
18-11-2020,15:30,18-11-2020,BRAND NEW NAME,US12008C1234,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,4006.65,cd5560a8-c903-4ddd-aacf-ad1319b6c588
18-11-2020,15:30,18-11-2020,BRAND NEW NAME,US12008C1234,Sell 28 BRAND NEW NAME@72.08 USD (US12008C1234),,USD,2018.24,USD,2018.24,cd5560a8-c903-4ddd-aacf-ad1319b6c588
20-07-2020,16:51,20-07-2020,BRAND NEW NAME,US12008C1234,ISIN CHANGE: Buy 28 BRAND NEW NAME@40.47 USD (US12008C1234),,USD,-1133.16,USD,0.00,
20-07-2020,16:51,20-07-2020,ORIGINAL NAME,US12008C1234,ISIN CHANGE: Sell 28 ORIGINAL NAME@40.47 USD (US12008C1234),,USD,1133.16,USD,1133.16,
03-06-2020,15:30,03-06-2020,ORIGINAL NAME,US12008C1234,FX Credit,1.2144,USD,1190.56,USD,0.00,d08f886f-c142-4f97-96fe-73ebcec1e180
03-06-2020,15:30,03-06-2020,ORIGINAL NAME,US12008C1234,FX Debit,,EUR,-980.38,EUR,23.25,d08f886f-c142-4f97-96fe-73ebcec1e180
03-06-2020,15:30,03-06-2020,ORIGINAL NAME,US12008C1234,DEGIRO Transaction and/or third party fees,,EUR,-0.09,EUR,1003.63,d08f886f-c142-4f97-96fe-73ebcec1e180
03-06-2020,15:30,03-06-2020,ORIGINAL NAME,US12008C1234,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,1003.72,d08f886f-c142-4f97-96fe-73ebcec1e180
03-06-2020,15:30,03-06-2020,ORIGINAL NAME,US12008C1234,Buy 28 ORIGINAL NAME@42.52 USD (US12008C1234),,USD,-1190.56,USD,-1190.56,d08f886f-c142-4f97-96fe-73ebcec1e180"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

        let allTnxs = Seq.map buildTxn txnsGrouped |> Seq.toList

        let expectedEarning =
            { Date = DateTime(2020, 11, 18, 15, 30, 0)
              Product = "BRAND NEW NAME"
              ISIN = "US12008C1234"
              ProdType = Shares
              Value = (1777.82m - 980.38m)
              Percent = Math.Round(((1777.82m - 980.38m) / 980.38m) * 100.0m, 2) }

        let sellTxns = getSellTxnsInPeriod allTnxs 2020 Period.All

        getSellsEarnings sellTxns allTnxs Map.empty |> should equal [ expectedEarning ]


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
              ISIN = "CODE123"
              Value = 10.5m
              ValueTax = -1.58m
              Currency = USD }

        let allDividends = getAllDividends rows 2020
        allDividends |> should haveLength 1

        allDividends[0] |> should equal expectedDividend


    [<Test>]
    let ``Split rows are not considered transactions`` () =
        let testRows =
            header
            + """
        06-06-2022,14:38,06-06-2022,ACME Inc NEW,CODENEW123456,STOCK SPLIT: Buy 5 ACME Inc NEW@17.5 USD (CODENEW123456),,USD,-87.50,USD,-0.00,
        06-06-2022,14:38,06-06-2022,ACME Inc OLD,CODEOLD123456,STOCK SPLIT: Sell 50 ACME Inc OLD@1.75 USD (CODEOLD123456),,USD,87.50,USD,87.50,"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows
        let txns = Seq.map buildTxn txnsGrouped
        txns |> should be Empty


    [<Test>]
    let ``ISIN change rows are not considered transactions`` () =
        let testRows =
            header
            + """
        03-10-2022,07:22,03-10-2022,ACME NEW NAME,CODENEW123456,ISIN CHANGE: Buy 7 ACME NEW NAME@29.52 USD (CODENEW123456),,USD,-206.64,USD,3.00,
        03-10-2022,07:22,03-10-2022,ACME OLD NAME,CODEOLD123456,ISIN CHANGE: Sell 7 ACME OLD NAME@29.52 USD (CODEOLD123456),,USD,206.64,USD,209.64,"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows
        let txns = Seq.map buildTxn txnsGrouped
        txns |> should be Empty


    [<Test>]
    let ``Merger rows are not considered transactions`` () =
        let testRows =
            header
            + """
        02-06-2023,14:25,02-06-2023,BIG CORP COMMON,US4042511123,MERGER: Buy 7 BIG CORP COMMON@25.3651 USD (US4042511123),,USD,-177.56,USD,10.54,
        02-06-2023,14:25,02-06-2023,SMALL CORP ACME,US494271234,MERGER: Sell 57 SMALL CORP ACME@3.3 USD (US494271234),,USD,188.10,USD,188.10,"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows
        let txns = Seq.map buildTxn txnsGrouped
        txns |> should be Empty


    [<Test>]
    let ``Get earnings of a stock that had a split`` () =
        let testRows =
            header
            + """
        01-08-2022,16:57,01-08-2022,ACME Inc NEW,CODENEW123456,FX Debit,1.0298,USD,-89.00,USD,6.08,ab2dea5e-1459-425c-96bb-f3124b754b75
        01-08-2022,16:57,01-08-2022,ACME Inc NEW,CODENEW123456,FX Credit,,EUR,86.43,EUR,1325.18,ab2dea5e-1459-425c-96bb-f3124b754b75
        01-08-2022,16:57,01-08-2022,ACME Inc NEW,CODENEW123456,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,1238.75,ab2dea5e-1459-425c-96bb-f3124b754b75
        01-08-2022,16:57,01-08-2022,ACME Inc NEW,CODENEW123456,Sell 5 ACME Inc NEW@17.8 USD (CODENEW123456),,USD,89.00,USD,95.08,ab2dea5e-1459-425c-96bb-f3124b754b75
        28-07-2022,07:27,27-07-2022,ACME Inc NEW,CODENEW123456,Dividend,,USD,4.50,USD,6.56,
        28-07-2022,07:27,27-07-2022,ACME Inc NEW,CODENEW123456,Dividend Tax,,USD,-0.68,USD,2.06,
        06-06-2022,14:38,06-06-2022,ACME Inc NEW,CODENEW123456,STOCK SPLIT: Buy 5 ACME Inc NEW@17.5 USD (CODENEW123456),,USD,-87.50,USD,-0.00,
        06-06-2022,14:38,06-06-2022,ACME Inc OLD,CODEOLD123456,STOCK SPLIT: Sell 50 ACME Inc OLD@1.75 USD (CODEOLD123456),,USD,87.50,USD,87.50,
        22-06-2020,15:30,22-06-2020,ACME Inc OLD,CODEOLD123456,FX Credit,1.1215,USD,207.00,USD,0.00,7e68c77d-7441-46f0-aac6-627ad59e253b
        22-06-2020,15:30,22-06-2020,ACME Inc OLD,CODEOLD123456,FX Debit,,EUR,-184.58,EUR,835.49,7e68c77d-7441-46f0-aac6-627ad59e253b
        22-06-2020,15:30,22-06-2020,ACME Inc OLD,CODEOLD123456,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,1020.07,7e68c77d-7441-46f0-aac6-627ad59e253b
        22-06-2020,15:30,22-06-2020,ACME Inc OLD,CODEOLD123456,DEGIRO Transaction and/or third party fees,,EUR,-0.18,EUR,1020.57,7e68c77d-7441-46f0-aac6-627ad59e253b
        22-06-2020,15:30,22-06-2020,ACME Inc OLD,CODEOLD123456,Buy 50 ACME Inc OLD@4.14 USD (CODEOLD123456),,USD,-207.00,USD,-207.00,7e68c77d-7441-46f0-aac6-627ad59e253b"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

        let txns = Seq.map buildTxn txnsGrouped |> Seq.toList
        txns |> should haveLength 2

        let sellTxns = getSellTxnsInPeriod txns 2022 Period.All
        let splits = getStockChanges rows
        splits |> should haveCount 1

        let expectedEarning =
            { Date = DateTime(2022, 8, 1, 16, 57, 0)
              Product = "ACME Inc NEW"
              ISIN = "CODENEW123456"
              ProdType = Shares
              Value = (86.43m - 184.58m)
              Percent = Math.Round(((86.43m - 184.58m) / 184.58m) * 100.0m, 2) }

        getSellsEarnings sellTxns txns splits |> should equal [ expectedEarning ]


    [<Test>]
    let ``Get earnings of a stock that had a split with ISIN change and multiplier rounding error`` () =
        let testRows =
            header
            + """
        29-07-2024,15:30,29-07-2024,ACME INC,NEWISIN12345,FX Debit,1.0842,USD,-9.02,USD,-0.01,96908937-369f-46fe-be23-d9807ad1550d
        29-07-2024,15:30,29-07-2024,ACME INC,NEWISIN12345,FX Credit,,EUR,8.31,EUR,4364.27,96908937-369f-46fe-be23-d9807ad1550d
        29-07-2024,15:30,29-07-2024,ACME INC,NEWISIN12345,DEGIRO Transaction and/or third party fees,,EUR,-2.00,EUR,4355.96,96908937-369f-46fe-be23-d9807ad1550d
        29-07-2024,15:30,29-07-2024,ACME INC,NEWISIN12345,Sell 25 ACME Inc@0.3606 USD (NEWISIN12345),,USD,9.02,USD,9.01,96908937-369f-46fe-be23-d9807ad1550d
        04-04-2023,14:34,04-04-2023,ACME INC,NEWISIN12345,STOCK SPLIT: Buy 25 ACME Inc@4.8765 USD (NEWISIN12345),,USD,-121.91,USD,4.84,
        04-04-2023,14:34,04-04-2023,ACME INC,OLDISIN12345,STOCK SPLIT: Sell 375 ACME Inc@0.3251 USD (OLDISIN12345),,USD,121.91,USD,126.75,
        14-05-2021,15:54,14-05-2021,ACME INC,OLDISIN12345,FX Credit,1.2133,USD,460.70,USD,0.00,019fe8b6-772d-4fe4-89fd-1031045a3679
        14-05-2021,15:54,14-05-2021,ACME INC,OLDISIN12345,FX Debit,,EUR,-379.71,EUR,1016.46,019fe8b6-772d-4fe4-89fd-1031045a3679
        14-05-2021,15:54,14-05-2021,ACME INC,OLDISIN12345,DEGIRO Transaction and/or third party fees,,EUR,-0.56,EUR,1396.17,019fe8b6-772d-4fe4-89fd-1031045a3679
        14-05-2021,15:54,14-05-2021,ACME INC,OLDISIN12345,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,1396.73,019fe8b6-772d-4fe4-89fd-1031045a3679
        14-05-2021,15:54,14-05-2021,ACME INC,OLDISIN12345,Buy 170 ACME Inc@2.71 USD (OLDISIN12345),,USD,-460.70,USD,-460.70,019fe8b6-772d-4fe4-89fd-1031045a3679
        25-03-2021,18:37,25-03-2021,ACME INC,OLDISIN12345,FX Credit,1.1757,USD,165.25,USD,0.60,e4ec296d-8bb9-47ee-80d8-c230139a7f34
        25-03-2021,18:37,25-03-2021,ACME INC,OLDISIN12345,FX Debit,,EUR,-140.55,EUR,1258.17,e4ec296d-8bb9-47ee-80d8-c230139a7f34
        25-03-2021,18:37,25-03-2021,ACME INC,OLDISIN12345,DEGIRO Transaction and/or third party fees,,EUR,-0.17,EUR,1398.72,e4ec296d-8bb9-47ee-80d8-c230139a7f34
        25-03-2021,18:37,25-03-2021,ACME INC,OLDISIN12345,FX Credit,1.1757,USD,99.15,USD,-164.65,e4ec296d-8bb9-47ee-80d8-c230139a7f34
        25-03-2021,18:37,25-03-2021,ACME INC,OLDISIN12345,FX Debit,,EUR,-84.33,EUR,1398.89,e4ec296d-8bb9-47ee-80d8-c230139a7f34
        25-03-2021,18:37,25-03-2021,ACME INC,OLDISIN12345,DEGIRO Transaction and/or third party fees,,EUR,-0.10,EUR,1483.22,e4ec296d-8bb9-47ee-80d8-c230139a7f34
        25-03-2021,18:37,25-03-2021,ACME INC,OLDISIN12345,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,1483.32,e4ec296d-8bb9-47ee-80d8-c230139a7f34
        25-03-2021,18:37,25-03-2021,ACME INC,OLDISIN12345,Buy 50 ACME Inc@3.305 USD (OLDISIN12345),,USD,-165.25,USD,-263.80,e4ec296d-8bb9-47ee-80d8-c230139a7f34
        25-03-2021,18:37,25-03-2021,ACME INC,OLDISIN12345,Buy 30 ACME Inc@3.305 USD (OLDISIN12345),,USD,-99.15,USD,-98.55,e4ec296d-8bb9-47ee-80d8-c230139a7f34
        21-01-2021,15:35,21-01-2021,ACME INC,OLDISIN12345,FX Credit,1.2136,USD,612.50,USD,-0.51,d16a014f-cdb4-4a5d-b8ed-5dcdd0362c31
        21-01-2021,15:35,21-01-2021,ACME INC,OLDISIN12345,FX Debit,,EUR,-504.70,EUR,217.95,d16a014f-cdb4-4a5d-b8ed-5dcdd0362c31
        21-01-2021,15:35,21-01-2021,ACME INC,OLDISIN12345,DEGIRO Transaction and/or third party fees,,EUR,-0.41,EUR,722.65,d16a014f-cdb4-4a5d-b8ed-5dcdd0362c31
        21-01-2021,15:35,21-01-2021,ACME INC,OLDISIN12345,DEGIRO Transaction and/or third party fees,,EUR,-0.50,EUR,723.06,d16a014f-cdb4-4a5d-b8ed-5dcdd0362c31
        21-01-2021,15:35,21-01-2021,ACME INC,OLDISIN12345,Buy 125 ACME Inc@4.9 USD (OLDISIN12345),,USD,-612.50,USD,-613.01,d16a014f-cdb4-4a5d-b8ed-5dcdd0362c31"""

        // Here we have:
        // 125 + 30 + 50 + 170 = 375 shares bought
        // then a stock split 15:1
        // then 25 shares sold.

        // If we use the multiplier of the stock split to compute the quantity of buys, we get:
        // (125/15) + (30/15) + (50/15) + (170/15) = 8.33333 + 2 + 3.33333 + 11.33333 = 24.99999 shares bought
        // which is different from the 25 shares sold.
        // Hence we need to round up to the next unit the quantity of one of the buy transactions.

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

        let txns = Seq.map buildTxn txnsGrouped |> Seq.toList
        txns |> should haveLength 4

        let sellTxns = getSellTxnsInPeriod txns 2024 Period.All
        let splits = getStockChanges rows
        splits |> should haveCount 1

        let totCostBuys = 504.7m + 84.33m + 140.55m + 379.71m

        let expectedEarning =
            { Date = DateTime(2024, 7, 29, 15, 30, 0)
              Product = "ACME INC"
              ISIN = "NEWISIN12345"
              ProdType = Shares
              Value = 8.31m - totCostBuys
              Percent = Math.Round(((8.31m - totCostBuys) / totCostBuys) * 100.0m, 2) }

        getSellsEarnings sellTxns txns splits |> should equal [ expectedEarning ]


    [<Test>]
    let ``Get earnings of a stock that had an ISIN change`` () =
        let testRows =
            header
            + """
        17-10-2022,17:01,17-10-2022,ACME NEW NAME,CODENEW123456,FX Debit,0.9842,USD,-225.54,USD,0.00,73afdd08-1cb2-44df-9eeb-76fa0bea7389
        17-10-2022,17:01,17-10-2022,ACME NEW NAME,CODENEW123456,FX Credit,,EUR,229.17,EUR,1908.62,73afdd08-1cb2-44df-9eeb-76fa0bea7389
        17-10-2022,17:01,17-10-2022,ACME NEW NAME,CODENEW123456,DEGIRO Transaction and/or third party fees,,EUR,-1.00,EUR,1679.45,73afdd08-1cb2-44df-9eeb-76fa0bea7389
        17-10-2022,17:01,17-10-2022,ACME NEW NAME,CODENEW123456,Sell 7 ACME NEW NAME@32.22 USD (CODENEW123456),,USD,225.54,USD,225.54,73afdd08-1cb2-44df-9eeb-76fa0bea7389
        03-10-2022,07:22,03-10-2022,ACME NEW NAME,CODENEW123456,ISIN CHANGE: Buy 7 ACME NEW NAME@29.52 USD (CODENEW123456),,USD,-206.64,USD,3.00,
        03-10-2022,07:22,03-10-2022,ACME OLD NAME,CODEOLD123456,ISIN CHANGE: Sell 7 ACME OLD NAME@29.52 USD (CODEOLD123456),,USD,206.64,USD,209.64,
        08-09-2022,21:31,08-09-2022,ACME OLD NAME,CODEOLD123456,FX Credit,0.9970,USD,222.39,USD,-0.00,b5e65ac1-b3ae-4237-902b-d60932cd7cb9
        08-09-2022,21:31,08-09-2022,ACME OLD NAME,CODEOLD123456,FX Debit,,EUR,-223.06,EUR,1416.23,b5e65ac1-b3ae-4237-902b-d60932cd7cb9
        08-09-2022,21:31,08-09-2022,ACME OLD NAME,CODEOLD123456,DEGIRO Transaction and/or third party fees,,EUR,-1.00,EUR,1639.29,b5e65ac1-b3ae-4237-902b-d60932cd7cb9
        08-09-2022,21:31,08-09-2022,ACME OLD NAME,CODEOLD123456,Buy 7 ACME OLD NAME@31.77 USD (CODEOLD123456),,USD,-222.39,USD,-222.39,b5e65ac1-b3ae-4237-902b-d60932cd7cb9"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

        let txns = Seq.map buildTxn txnsGrouped |> Seq.toList
        txns |> should haveLength 2

        let sellTxns = getSellTxnsInPeriod txns 2022 Period.All
        let isinChanges = getStockChanges rows
        isinChanges |> should haveCount 1

        let expectedEarning =
            { Date = DateTime(2022, 10, 17, 17, 1, 0)
              Product = "ACME NEW NAME"
              ISIN = "CODENEW123456"
              ProdType = Shares
              Value = (229.17m - 223.06m)
              Percent = Math.Round(((229.17m - 223.06m) / 223.06m) * 100.0m, 2) }

        getSellsEarnings sellTxns txns isinChanges |> should equal [ expectedEarning ]

    [<Test>]
    let ``Get earnings of a stock that had a merger`` () =
        let testRows =
            header
            + """
        16-06-2023,18:25,16-06-2023,NEWCORP NAME,ISINNEW12345,FX Debit,1.0955,USD,-185.15,USD,6.86,7caa97fa-b0f0-4fde-89ef-408a6ff9849e
        16-06-2023,18:25,16-06-2023,NEWCORP NAME,ISINNEW12345,FX Credit,,EUR,169.00,EUR,157.62,7caa97fa-b0f0-4fde-89ef-408a6ff9849e
        16-06-2023,18:25,16-06-2023,NEWCORP NAME,ISINNEW12345,DEGIRO Transaction and/or third party fees,,EUR,-2.00,EUR,-11.38,7caa97fa-b0f0-4fde-89ef-408a6ff9849e
        16-06-2023,18:25,16-06-2023,NEWCORP NAME,ISINNEW12345,Sell 7 NEWCORP NAME@26.45 USD (ISINNEW12345),,USD,185.15,USD,192.01,7caa97fa-b0f0-4fde-89ef-408a6ff9849e
        06-06-2023,11:57,02-06-2023,NEWCORP NAME,ISINNEW12345,Corporate Action Cash Settlement Stock,,USD,513.00,USD,513.00,
        02-06-2023,14:25,02-06-2023,NEWCORP NAME,ISINNEW12345,MERGER: Buy 7 NEWCORP NAME@25.3651 USD (ISINNEW12345),,USD,-177.56,USD,10.54,
        02-06-2023,14:25,02-06-2023,OLDCORP NAME,ISINOLD123345,MERGER: Sell 57 OLDCORP NAME@3.3 USD (ISINOLD123345),,USD,188.10,USD,188.10,
        11-05-2023,20:21,11-05-2023,OLDCORP NAME,ISINOLD123345,FX Debit,1.0941,USD,-75.18,USD,0.00,41b95965-ea3d-4668-83cb-7f31bcc91f27
        11-05-2023,20:21,11-05-2023,OLDCORP NAME,ISINOLD123345,FX Credit,,EUR,68.71,EUR,3654.99,41b95965-ea3d-4668-83cb-7f31bcc91f27
        11-05-2023,20:21,11-05-2023,OLDCORP NAME,ISINOLD123345,DEGIRO Transaction and/or third party fees,,EUR,-1.00,EUR,3586.28,41b95965-ea3d-4668-83cb-7f31bcc91f27
        11-05-2023,20:21,11-05-2023,OLDCORP NAME,ISINOLD123345,Sell 6 OLDCORP NAME@12.53 USD (ISINOLD123345),,USD,75.18,USD,75.18,41b95965-ea3d-4668-83cb-7f31bcc91f27
        11-04-2023,17:38,11-04-2023,OLDCORP NAME,ISINOLD123345,FX Credit,1.0884,USD,773.64,USD,0.01,bcb7c5f7-41f8-431e-ab0e-eaf52d72f21b
        11-04-2023,17:38,11-04-2023,OLDCORP NAME,ISINOLD123345,FX Debit,,EUR,-710.82,EUR,3657.48,bcb7c5f7-41f8-431e-ab0e-eaf52d72f21b
        11-04-2023,17:38,11-04-2023,OLDCORP NAME,ISINOLD123345,DEGIRO Transaction and/or third party fees,,EUR,-1.00,EUR,4368.30,bcb7c5f7-41f8-431e-ab0e-eaf52d72f21b
        11-04-2023,17:38,11-04-2023,OLDCORP NAME,ISINOLD123345,Buy 63 OLDCORP NAME@12.28 USD (ISINOLD123345),,USD,-773.64,USD,-773.63,bcb7c5f7-41f8-431e-ab0e-eaf52d72f21b"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows

        let txns = Seq.map buildTxn txnsGrouped |> Seq.toList
        txns |> should haveLength 3

        let sellTxns = getSellTxnsInPeriod txns 2023 Period.All
        let mergersChanges = getStockChanges rows
        mergersChanges |> should haveCount 1

        let partialBuyCost = (710.82m / 63m) * 6m

        // XXX: in case of a merger (or ISIN change event) with "Corporate Action Cash Settlement" it's impossible to determine the
        // precise earning of the transaction, since the "Cash Settlement" creates a disparity which
        // is not possible to sort out as it cannot be related to the transaction in question (doesn't have a transaction GUID).

        // let multiplier = 57 / 7
        // let mergedBuyCost = (710.82m / 63m) * 7m * (decimal multiplier)
        let mergedBuyCost = 710.82m

        let expectedEarning =
            [ { Date = DateTime(2023, 6, 16, 18, 25, 0)
                Product = "NEWCORP NAME"
                ISIN = "ISINNEW12345"
                ProdType = Shares
                Value = (169.0m - mergedBuyCost)
                Percent = Math.Round(((169.0m - mergedBuyCost) / mergedBuyCost) * 100.0m, 2) }
              { Date = DateTime(2023, 5, 11, 20, 21, 0)
                Product = "OLDCORP NAME"
                ISIN = "ISINOLD123345"
                ProdType = Shares
                Value = 68.71m - partialBuyCost
                Percent = Math.Round(((68.71m - partialBuyCost) / partialBuyCost) * 100.0m, 2) } ]

        getSellsEarnings sellTxns txns mergersChanges |> should equal expectedEarning


    [<Test>]
    let ``Create StockChange objects`` () =
        let testRows =
            header
            + """
        06-06-2022,14:38,06-06-2022,ACME Inc NEW,CODENEW123456,STOCK SPLIT: Buy 5 ACME Inc NEW@17.5 USD (CODENEW123456),,USD,-87.50,USD,-0.00,
        06-06-2022,14:38,06-06-2022,ACME Inc OLD,CODEOLD123456,STOCK SPLIT: Sell 50 ACME Inc OLD@1.75 USD (CODEOLD123456),,USD,87.50,USD,87.50,"""

        let rows = AccountCsv.Parse(testRows).Rows
        let stockChanges = getStockChanges rows

        let expectedStockChangeMap =
            Map[("CODENEW123456",
                 { Date = DateTime(2022, 6, 6, 14, 38, 0)
                   IsinBefore = "CODEOLD123456"
                   IsinAfter = "CODENEW123456"
                   ProductBefore = "ACME Inc OLD"
                   ProductAfter = "ACME Inc NEW"
                   Multiplier = 10 })]

        stockChanges |> should equal expectedStockChangeMap


    [<Test>]
    let ``Get total ADR fees for a year`` () =
        let testRows =
            header
            + """
        22-08-2022,10:17,03-06-2021,Contoso Inc.,US012344A1088,ADR/GDR Pass-Through Fee,,USD,0.60,USD,0.60,
        16-08-2022,14:12,09-08-2022,ACME Inc,US6541232043,ADR/GDR Pass-Through Fee,,USD,-0.10,USD,3.02,
        16-08-2022,14:10,09-08-2022,ACME Inc,US6541232043,ADR/GDR Pass-Through Fee,,USD,0.19,USD,4.18,
        15-08-2022,08:33,09-08-2022,ACME Inc,US6541232043,ADR/GDR Pass-Through Fee,,USD,0.10,USD,0.97,
        15-08-2022,08:33,09-08-2022,ACME Inc,US6541232043,ADR/GDR Pass-Through Fee,,USD,-0.19,USD,-0.19,"""

        let rows = AccountCsv.Parse(testRows).Rows
        let totAdrFees = getTotalYearAdrFees rows 2022

        Math.Round(totAdrFees, 2) |> should equal 0.6


    [<Test>]
    let ``Get total Stamp Duty fees for a year`` () =
        let testRows =
            header
            + """
        16-06-2023,15:43,16-06-2023,ACME LTD,GB0012345H64,FX Debit,,EUR,-2387.76,EUR,-9.38,f8a5113b-2bd7-46fd-97d1-cbf00c37dd53
        16-06-2023,15:43,16-06-2023,ACME LTD,GB0012345H64,London/Dublin Stamp Duty,,EUR,-11.94,EUR,2378.38,f8a5113b-2bd7-46fd-97d1-cbf00c37dd53
        16-06-2023,15:43,16-06-2023,ACME LTD,GB0012345H64,FX Credit,85.1470,GBP,225.90,GBP,-2033.10,f8a5113b-2bd7-46fd-97d1-cbf00c37dd53
        16-06-2023,15:43,16-06-2023,ACME LTD,GB0012345H64,FX Debit,,EUR,-265.31,EUR,2390.32,f8a5113b-2bd7-46fd-97d1-cbf00c37dd53
        16-06-2023,15:43,16-06-2023,ACME LTD,GB0012345H64,DEGIRO Transaction and/or third party fees,,EUR,-4.90,EUR,2655.63,f8a5113b-2bd7-46fd-97d1-cbf00c37dd53
        16-06-2023,15:43,16-06-2023,ACME LTD,GB0012345H64,London/Dublin Stamp Duty,,EUR,-1.33,EUR,2660.53,f8a5113b-2bd7-46fd-97d1-cbf00c37dd53
        16-06-2023,15:43,16-06-2023,ACME LTD,GB0012345H64,Buy 270 ACME LTD@753 GBX (GB0012345),,GBP,-2033.10,GBP,-2259.00,f8a5113b-2bd7-46fd-97d1-cbf00c37dd53
        16-06-2023,15:43,16-06-2023,ACME LTD,GB0012345H64,Buy 30 ACME LTD@753 GBX (GB0012345),,GBP,-225.90,GBP,-225.90,f8a5113b-2bd7-46fd-97d1-cbf00c37dd53
        16-06-2022,15:37,16-06-2023,FOO BAR LTD,GB123543Y695,FX Credit,85.1673,GBP,1363.64,GBP,0.00,64fc3347-e204-4894-bd03-da3cddbb7fcb
        16-06-2022,15:37,16-06-2023,FOO BAR LTD,GB123543Y695,FX Debit,,EUR,-1601.15,EUR,2661.86,64fc3347-e204-4894-bd03-da3cddbb7fcb
        16-06-2022,15:37,16-06-2023,FOO BAR LTD,GB123543Y695,London/Dublin Stamp Duty,,EUR,-8.01,EUR,4263.01,64fc3347-e204-4894-bd03-da3cddbb7fcb
        16-06-2022,15:37,16-06-2023,FOO BAR LTD,GB123543Y695,FX Credit,85.1673,GBP,74.72,GBP,-1363.64,64fc3347-e204-4894-bd03-da3cddbb7fcb
        16-06-2022,15:37,16-06-2023,FOO BAR LTD,GB123543Y695,FX Debit,,EUR,-87.73,EUR,4271.02,64fc3347-e204-4894-bd03-da3cddbb7fcb
        16-06-2022,15:37,16-06-2023,FOO BAR LTD,GB123543Y695,London/Dublin Stamp Duty,,EUR,-0.44,EUR,4358.75,64fc3347-e204-4894-bd03-da3cddbb7fcb
        16-06-2022,15:37,16-06-2023,FOO BAR LTD,GB123543Y695,Buy 292 FOO BAR@467 GBX (GB00123543),,GBP,-1363.64,GBP,-1438.36,64fc3347-e204-4894-bd03-da3cddbb7fcb"""

        let rows = AccountCsv.Parse(testRows).Rows

        getTotalYearStampDuty rows 2022 |> should equal (-0.44 - 8.01)

        getTotalYearStampDuty rows 2023 |> should equal (-11.94 - 1.33)


    [<Test>]
    let ``Get ETF buy transactions in a given year`` () =
        let testRows =
            header
            + """
03-05-2024,21:59,03-05-2024,ACME LTD VERY GOOD,US1233456,FX Debit,1.0791,USD,-2335.50,USD,0.00,305a5867-3282-4ac5-9e09-be49ac2f1f02
03-05-2024,21:59,03-05-2024,ACME LTD VERY GOOD,US1233456,FX Credit,,EUR,2164.32,EUR,2309.28,305a5867-3282-4ac5-9e09-be49ac2f1f02
03-05-2024,21:59,03-05-2024,ACME LTD VERY GOOD,US1233456,DEGIRO Transaction and/or third party fees,,EUR,-2.00,EUR,144.96,305a5867-3282-4ac5-9e09-be49ac2f1f02
03-05-2024,21:59,03-05-2024,ACME LTD VERY GOOD,US1233456,Sell 173 ACME LTD VERY GOOD@13.5 USD (US1233456),,USD,2335.50,USD,2335.50,305a5867-3282-4ac5-9e09-be49ac2f1f02
18-04-2024,10:06,18-04-2024,AMUNDI STOXX EUROPE 600 - UCITS ETF,LU0123456,DEGIRO Transaction and/or third party fees,,EUR,-1.00,EUR,82.90,39ff7a39-4a82-4e50-b49b-3c731be6202a
18-04-2024,10:06,18-04-2024,AMUNDI STOXX EUROPE 600 - UCITS ETF,LU0123456,Buy 13 AMUNDI STOXX EUROPE 600 - UCITS ETF ACC@228.95 EUR (LU0123456),,EUR,-2976.35,EUR,83.90,39ff7a39-4a82-4e50-b49b-3c731be6202a
04-04-2024,21:40,04-04-2024,BERKSHIRE HATHAWAY INC,US0846707026,FX Credit,1.0809,USD,1660.00,USD,0.00,f64bdbd7-6864-44d1-8897-c99c9f8af142
04-04-2024,21:40,04-04-2024,BERKSHIRE HATHAWAY INC,US0846707026,FX Debit,,EUR,-1535.77,EUR,169.81,f64bdbd7-6864-44d1-8897-c99c9f8af142
04-04-2024,21:40,04-04-2024,BERKSHIRE HATHAWAY INC,US0846707026,DEGIRO Transaction and/or third party fees,,EUR,-2.00,EUR,1705.58,f64bdbd7-6864-44d1-8897-c99c9f8af142
04-04-2024,21:40,04-04-2024,BERKSHIRE HATHAWAY INC,US0846707026,Buy 4 BERKSHIRE HATHAWAY INC@415 USD (US0846707026),,USD,-1660.00,USD,-1660.00,f64bdbd7-6864-44d1-8897-c99c9f8af142
01-03-2024,12:08,01-03-2024,ISHARES CORE S&P 500 UCITS ETF USD,IE00123456,DEGIRO Transaction and/or third party fees,,EUR,-1.00,EUR,2122.85,449f7dce-814d-4174-aaec-caf3dc6c1862
01-03-2024,12:08,01-03-2024,ISHARES CORE S&P 500 UCITS ETF USD,IE00123456,Buy 6 iShares Core S&P 500 UCITS ETF USD (Acc)@494.31 EUR (IE00123456),,EUR,-2965.86,EUR,2123.85,449f7dce-814d-4174-aaec-caf3dc6c1862
08-12-2023,17:07,08-12-2023,ISHARES EMIM,IE98765,DEGIRO Transaction and/or third party fees,,EUR,-1.00,EUR,207.54,2ad5b7e3-414d-497b-9e74-807a95892125
08-12-2023,17:07,08-12-2023,ISHARES EMIM,IE98765,Buy 14 ISHARES EMIM@28.151 EUR (IE98765),,EUR,-394.11,EUR,208.54,2ad5b7e3-414d-497b-9e74-807a95892125
08-12-2023,17:06,08-12-2023,ISHARES CORE S&P 500 UCITS ETF USD,IE00123456,DEGIRO Transaction and/or third party fees,,EUR,-1.00,EUR,602.65,28d568a3-b27d-497d-8974-12651af7b10d
08-12-2023,17:06,08-12-2023,ISHARES CORE S&P 500 UCITS ETF USD,IE00123456,Buy 3 iShares Core S&P 500 UCITS ETF USD (Acc)@447.32 EUR (IE00123456),,EUR,-1341.96,EUR,603.65,28d568a3-b27d-497d-8974-12651af7b10d"""

        let rows = AccountCsv.Parse(testRows).Rows
        let txnsGrouped = getRowsGroupedByOrderId rows
        let txns = Seq.map buildTxn txnsGrouped |> Seq.toList
        let etfBuys = getEtfBuyTxnsInYear txns 2024

        let expectedTxns =
            [ { Date = DateTime(2024, 4, 18, 10, 6, 0)
                Type = Buy
                Product = "AMUNDI STOXX EUROPE 600 - UCITS ETF"
                ISIN = "LU0123456"
                ProdType = ProductType.ETF
                Quantity = 13
                Price = -228.95m * 13m
                Value = 228.95m
                ValueCurrency = Currency.EUR
                Fees = -1.0m
                OrderId = Guid.Parse("39ff7a39-4a82-4e50-b49b-3c731be6202a") }
              { Date = DateTime(2024, 3, 1, 12, 8, 0)
                Type = Buy
                Product = "ISHARES CORE S&P 500 UCITS ETF USD"
                ISIN = "IE00123456"
                ProdType = ProductType.ETF
                Quantity = 6
                Price = -494.31m * 6m
                Value = 494.31m
                ValueCurrency = Currency.EUR
                Fees = -1.0m
                OrderId = Guid.Parse("449f7dce-814d-4174-aaec-caf3dc6c1862") } ]

        etfBuys |> should equal expectedTxns