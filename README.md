# Degiro Bookkeeper

[![.NET](https://github.com/pviotti/degiro-bookkeeper/actions/workflows/dotnet.yml/badge.svg?branch=master)](https://github.com/pviotti/degiro-bookkeeper/actions/workflows/dotnet.yml)
[![codecov](https://codecov.io/gh/pviotti/degiro-bookkeeper/branch/master/graph/badge.svg?token=rTsBxS9b8p)](https://codecov.io/gh/pviotti/degiro-bookkeeper)
[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/paypalme/paoloviotti/2)

This is a simple command line tool to work out earnings and dividends figures
for a [Degiro] account.
It takes as input a Degiro account CSV statement file in English language, and outputs to console
or to CSV files information about earnings from sells transactions and dividends
in a given period.

> **Disclaimer**: I am not a tax expert or a financial advisor,
> and this software (as any software) may be buggy, so use it at your own risk.

## Usage

 1. Download your Degiro account statement (`Activity` > `Account Statement`)
    for a certain time period as a CSV file.
 2. Supply the downloaded CSV file as input to Degiro Bookkeeper,
    using parameters as specified in its help message reported below.

```bash
$ ./dgbk --help
USAGE: dgbk [--help] [--version] --year <year> [--period <period>] [--outputpath <output path>] <input>

CSVFILEPATH:

    <input>               path of Degiro Account Statement CSV file

OPTIONS:

    --version             print dgbk version
    --year, -y <year>     year (in YYYY format)
    --period, -p <period> Irish CGT tax period (1: Jan-Nov; 2: Dec; default: whole year)
    --outputpath, -o <output path>
                          path for earnings and dividends CSVs output
    --help                display this list of options
```

## Features

 - compute earnings in € using exact exchange rate as per account statement
 - tell apart ETF and shares earnings
 - account for Degiro fees on transactions
 - account for taxes on dividends automatically deducted by Degiro
 - process malformed input CSV (i.e. with garbled rows) and output cleaned up CSV
   in the same folder as the input file (with `-clean.csv` suffix)
 - work out dividends in €, $ or Can$ in a given year
 - output summary of deposits and withdrawals both total and per-year
 - output total ADR fees for a given year in $
 - output and take into account stock splits
 - country-specific features:
   - Ireland: support for CGT tax periods (i.e. Initial and Later - see revenue.ie)

## Limitations

 - only supports Degiro account statements in English language
 - only outputs earnings in €
 - only supports transactions in € or USD

## FAQ

**Q:** *Why is my Degiro Annual Report showing numbers slightly different from the Account Statement?*

> In the Annual Report we use the exchange rates of the close price of the day the document is created.
> The Account Statement uses more accurate data, but the difference should be negligible.
> However, please remember that this report is not an official document as Degiro is not providing tax related services.
>
> -- *Degiro customer support (queried via mail, December 2021)*

 [degiro]: https://www.degiro.ie/member-get-member/start-trading?id=7883544C&utm_source=mgm
