# Degiro Bookkeeper

[![.NET](https://github.com/pviotti/degiro-bookkeeper/actions/workflows/dotnet.yml/badge.svg?branch=master)](https://github.com/pviotti/degiro-bookkeeper/actions/workflows/dotnet.yml)
[![codecov](https://codecov.io/gh/pviotti/degiro-bookkeeper/branch/master/graph/badge.svg?token=rTsBxS9b8p)](https://codecov.io/gh/pviotti/degiro-bookkeper)

This is a simple command line tool to work out earnings and dividends figures
for a [Degiro] account.
It takes as input a Degiro account CSV statement file, and outputs to console
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
USAGE: dgbk [--help] [--version] [--year <year>] [--period <period>] [--outputpath <output path>] <input>

CSVFILEPATH:

    <input>               path of Degiro account CSV file

OPTIONS:

    --version             print dgbk version
    --year, -y <year>     year
    --period, -p <period> Irish CGT tax period (1: Jan-Nov; 2: Dec)
    --outputpath, -o <output path>
                          output path for earnings and dividends CSVs
    --help                display this list of options.
```

## Features

 - computes earnings (or losses) in € using exact exchange rate as per account statement
   in case of transactions in non-€ currencies
 - accounts for Degiro fees on transactions
 - accounts for taxes on dividends automatically deducted by Degiro
 - only outputs earnings in €. Transactions are supported in the following currencies: €, USD
 - processes malformed input CSV (i.e. with garbled rows) and output cleaned up CSV
   in the same folder as the input file (with `-clean.csv` suffix)
 - computes dividends in € or USD in a given year
 - supports Irish CGT tax periods (i.e. Initial and Later - see revenue.ie)


 [degiro]: https://www.degiro.ie/
