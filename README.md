# Alma Authorize.net Payment Reporting

This is a C# console app that pulls transaction data from Authorize.net and fee payment data from Alma, matches them together, and inputs the data into a database.

To build this as a self-contained Linux executable: `dotnet publish -r linux-x64 -p:PublishSingleFile=true --self-contained true`

## License

Copyright University of Pittsburgh.

Freely licensed for reuse under the MIT License.