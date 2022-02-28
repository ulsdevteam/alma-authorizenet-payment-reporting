# Alma Authorize.net Payment Reporting

This is a C# console app that pulls transaction data from Authorize.net and fee payment data from Alma, matches them together, and inputs the data into a database.

This app has 2 modes: the default `run` mode, and the `migrate` mode which can be used to update a pre-existing table to an new schema.

## Configuration
Configuration values are set via environment variables, or using a `.env` file. See [the example .env file](example.env) for what values are expected to be present.

## `run`

    alma-authorizenet-payment-reporting 0.2.0
    Copyright (C) 2022 University of Pittsburgh

    -f, --from              Get transactions starting from this date. If not supplied, defaults to the most recent transaction date in the database. If the table is empty, defaults to one
                            month before today.

    -t, --to                Get transactions up to this date. Defaults to today.

    -l, --log               Log messages to stdout when set.

    -d, --dryrun            Will not connect to database when set.

    -s, --schema-version    (Default: V2) Which version of the reporting table schema to use.

    --help                  Display this help screen.

    --version               Display version information.

When the program runs, it will pull all Authorize.net transaction batches that have been settled within the dates provided. For each transaction in those batches, it will match it to its corresponding fee payment in Alma, and then write the results to the table. If the `dryrun` option is set, it will still do this matching and will report any errors, but will not read from or update the database.

By default, the most recent schema will be used. 
## `migrate`

    alma-authorizenet-payment-reporting 0.2.0
    Copyright (C) 2022 University of Pittsburgh

    -l, --log       Log messages to stdout when set.

    --help          Display this help screen.

    --version       Display version information.

    value pos. 0    Required.

    value pos. 1    (Default: V2)

The migrate command can be used to update existing tables if their structure changes between updates to this program. Table definitions for these schemas as well as the SQL commands for performing the migrations can be found [here](Schema.cs).

## Building

To build this as a self-contained Linux executable: `dotnet publish -r linux-x64 -p:PublishSingleFile=true --self-contained true`

## License

Copyright University of Pittsburgh.

Freely licensed for reuse under the MIT License.