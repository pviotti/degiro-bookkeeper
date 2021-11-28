namespace Degiro.Tests

open NUnit.Framework
open FsUnit

open Degiro.Account
open AccountTests

module CsvParsingTests =

    [<Test>]
    let ``Clean a malformed CSV string`` () =
        // Degiro account CSV files may have malformed rows, as the following

        let malformedRows =
            """10-09-2020,21:37,10-09-2020,SHARE NAME,CODE123,FX Debit,,EUR,-659995.30,EUR,663.86,df793c8a-2033-43c1-
,,,,,,,,,,,abd3-6d415682f99f
26-06-2020,21:15,26-06-2020,SHARE NAME A,CODE123,Sell 5 ACME Inc@213 USD,,USD,1065.00,USD,1065.64,5092d5ff-ae52-4620-
,,,,,(IE00B4BNMY34),,,,,,8bd1-d715b7897812
26-06-2020,16:01,26-06-2020,SHARE NAME,CODE123,"Money Market fund conversion: Buy 0.000229 at 9,934.3851 EUR",,,,EUR,164.81,
26-06-2020,09:24,25-06-2020,SHARE NAME 123,CODE123,Dividend,,USD,0.64,USD,0.64,
30-01-2020,10:50,30-01-2020,SHARE NAME B,CODE123,Sell 10 ACME Inc 2@7.215 USD,,USD,72.15,USD,72.15,c6aead59-29c2-40f4-8158-b92cc9b6867e
,,,,,(IE00B1XNHC34),,,,,,
30-01-2020,09:06,30-01-2020,SHARE NAME,CODE123,FX Credit,1.1004,USD,72.15,USD,0.00,c6aead59-29c2-40f4-8158-b92cc9b6867e
28-01-2020,15:30,28-01-2020,SHARE NAME,CODE123,FX Debit,,EUR,-136.56,EUR,1187.59,00c31d47-e414-460c-
,,,,,,,,,,,8905-9c33d4cc41a0
28-01-2020,15:30,28-01-2020,SHARE NAME,CODE123,DEGIRO Transaction Fee,,EUR,-0.50,EUR,1324.15,00c31d47-e414-460c-8905-9c33d4cc41a
    """

        let expectedCleanRows =
            """10-09-2020,21:37,10-09-2020,SHARE NAME,CODE123,FX Debit,,EUR,-659995.30,EUR,663.86,df793c8a-2033-43c1-abd3-6d415682f99f
26-06-2020,21:15,26-06-2020,SHARE NAME A,CODE123,Sell 5 ACME Inc@213 USD,,USD,1065.00,USD,1065.64,5092d5ff-ae52-4620-8bd1-d715b7897812
26-06-2020,16:01,26-06-2020,SHARE NAME,CODE123,"Money Market fund conversion: Buy 0.000229 at 9,934.3851 EUR",,,,EUR,164.81,
26-06-2020,09:24,25-06-2020,SHARE NAME 123,CODE123,Dividend,,USD,0.64,USD,0.64,
30-01-2020,10:50,30-01-2020,SHARE NAME B,CODE123,Sell 10 ACME Inc 2@7.215 USD,,USD,72.15,USD,72.15,c6aead59-29c2-40f4-8158-b92cc9b6867e
30-01-2020,09:06,30-01-2020,SHARE NAME,CODE123,FX Credit,1.1004,USD,72.15,USD,0.00,c6aead59-29c2-40f4-8158-b92cc9b6867e
28-01-2020,15:30,28-01-2020,SHARE NAME,CODE123,FX Debit,,EUR,-136.56,EUR,1187.59,00c31d47-e414-460c-8905-9c33d4cc41a0
28-01-2020,15:30,28-01-2020,SHARE NAME,CODE123,DEGIRO Transaction Fee,,EUR,-0.50,EUR,1324.15,00c31d47-e414-460c-8905-9c33d4cc41a"""

        let cleanRows, isMalformed = cleanCsv malformedRows
        isMalformed |> should be True
        cleanRows |> should equal expectedCleanRows


    [<Test>]
    let ``Parse row without price value`` () =
        let testRows =
            header
            + """
11-09-2020,15:56,11-09-2020,ACME Inc,CODE123,"Money Market fund conversion: Sell 0.009917 at 9,923.3034 EUR",,,,EUR,663.86,
"""

        let rows = AccountCsv.Parse(testRows).Rows
        Seq.length rows |> should equal 1
