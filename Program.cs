using System;
using System.Collections.Generic;
using System.Linq;
using alma_authorizenet_payment_reporting.AlmaTypes;
using dotenv.net;
using Microsoft.Extensions.Configuration;
using AuthorizeNet.Api.Controllers;
using AuthorizeNet.Api.Contracts.V1;
using AuthorizeNet.Api.Controllers.Bases;
using CommandLine;
using Dapper;
using Flurl.Http;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace alma_authorizenet_payment_reporting
{
    static class Program
    {
        static IConfiguration Config { get; set; }
        static IFlurlClient AlmaClient { get; set; }
        static Action<string> Log { get; set; }

        static async Task Main(string[] args)
        {
            DotEnv.Load();
            Config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            ApiOperationBase<ANetApiRequest, ANetApiResponse>.MerchantAuthentication = new merchantAuthenticationType
            {
                name = Config["AUTHORIZE_API_LOGIN"],
                ItemElementName = ItemChoiceType.transactionKey,
                Item = Config["AUTHORIZE_API_KEY"]
            };
            // TODO: support for custom authorize.net environments?
            ApiOperationBase<ANetApiRequest, ANetApiResponse>.RunEnvironment = Config["AUTHORIZE_ENVIRONMENT"]?.ToUpper() switch
            {
                "SANDBOX" => AuthorizeNet.Environment.SANDBOX,
                "PRODUCTION" => AuthorizeNet.Environment.PRODUCTION,
                "LOCAL_VM" => AuthorizeNet.Environment.LOCAL_VM,
                "HOSTED_VM" => AuthorizeNet.Environment.HOSTED_VM,
                null => throw new Exception("Missing Authorize.net environment"),
                _ => throw new Exception("Unrecognized Authorize.net environment")
            };

            AlmaClient = new FlurlClient($"https://api-{Config["ALMA_REGION"]}.hosted.exlibrisgroup.com/almaws/v1")
                .BeforeCall(call => call.Request
                    .WithHeader("Accept", "application/json")
                    .SetQueryParam("apikey", Config["ALMA_API_KEY"]));

            var commandLineParser = new Parser(config => {
                config.AutoHelp = true;
                config.CaseInsensitiveEnumValues = true;
                config.HelpWriter = Console.Error;
            });

            await commandLineParser.ParseArguments<Options, MigrateOptions>(args).MapResult(
                async (Options options) =>
                {
                    try
                    {
                        Log = options.Log ? Console.WriteLine : (_) => { };
                        using var connection = options.DryRun ? null : new OracleConnection(Config["CONNECTION_STRING"]);
                        var schema = Schema.Get(options.SchemaVersion, Config["TABLE_NAME"]);
                        Log($"Using schema version {schema.Version}.");
                        await EnsureTableExists(connection, schema);
                        var transactions = GetSettledTransactions(
                            options.FromDate ?? await GetMostRecentTransactionDate(connection) ?? DateTime.Today.AddMonths(-1),
                            options.ToDate ?? DateTime.Today);
                        var records = await GetPaymentRecords(transactions);
                        await UpdateDatabase(connection, schema, records);
                        if (options.DryRun) Log($"Got {records.Count} records.");
                    }
                    catch (System.Exception e)
                    {
                        Console.Error.WriteLine(e);
                    }
                },
                async (MigrateOptions migrateOptions) => { 
                    Log = migrateOptions.Log ? Console.WriteLine : (_) => { };
                    using var connection = new OracleConnection(Config["CONNECTION_STRING"]);
                    var currentSchema = Schema.Get(migrateOptions.CurrentSchema, Config["TABLE_NAME"]);
                    var newSchema = Schema.Get(migrateOptions.NewSchema, Config["TABLE_NAME"]);
                    await MigrateTable(connection, currentSchema, newSchema);
                },
                errors => Task.CompletedTask
                )
                ;
        }

        static async Task EnsureTableExists(IDbConnection connection, Schema schema)
        {
            if (connection is null) return;
            Log($"Checking if table '{Config["TABLE_NAME"]}' exists.");
            var result = await connection.QueryAsync(@"
                select table_name
                from user_tables
                where table_name = :TableName
            ", new { TableName = Config["TABLE_NAME"] });
            if (!result.Any())
            {
                Log("Table does not exist, creating table.");
                await connection.ExecuteAsync(schema.TableCreationSql());
            }
            else
            {
                Log("Table exists.");
            }
        }

        static async Task<DateTime?> GetMostRecentTransactionDate(IDbConnection connection)
        {
            if (connection is null) return null;
            Log("Getting most recent transaction date");
            return await connection.QueryFirstOrDefaultAsync<DateTime?>($@"
                select TransactionSubmitTime from {Config["TABLE_NAME"]}
                order by TransactionSubmitTime desc
                fetch next 1 rows only");
        }

        [Obsolete("This function is not currently in use, as we are only concerned with settled transactions.")]
        static IEnumerable<AuthorizeTransaction> GetTransactionsInDateRange(DateTime start, DateTime end)
        {
            start = start.ToUniversalTime();
            end = end.ToUniversalTime();
            Log($"Getting Authorize.net transactions between {start.ToLocalTime()} and {end.ToLocalTime()}");
            return GetSettledTransactions(start, end)
                .Concat(GetUnsettledTransactions().Select(t => new AuthorizeTransaction(t, null)))
                .Where(t => t.transaction.submitTimeUTC >= start && t.transaction.submitTimeUTC <= end);
        }

        [Obsolete("This function is not currently in use, as we are only concerned with settled transactions.")]
        static IEnumerable<transactionDetailsType> GetUnsettledTransactions()
        {
            Log("Getting transactions that are not yet settled");
            // Page offset starts at 1
            var pageOffset = 0;
            while (true)
            {
                ++pageOffset;
                var listRequest = new getUnsettledTransactionListRequest
                {
                    paging = new Paging
                    {
                        limit = 1000,
                        offset = pageOffset,
                    }
                };
                var listController = new getUnsettledTransactionListController(listRequest);
                var listResponse = listController.ExecuteAndCheckError();
                if (listResponse.transactions is null) yield break;
                foreach (var transaction in listResponse.transactions)
                {
                    var transactionDetailsRequest = new getTransactionDetailsRequest { transId = transaction.transId };
                    var transactionDetailsController = new getTransactionDetailsController(transactionDetailsRequest);
                    var transactionDetailsResponse = transactionDetailsController.ExecuteAndCheckError();
                    yield return transactionDetailsResponse.transaction;
                }
                // If the number of results is less than the limit, this means there should be no more
                // The next call would return no transactions and break anyway, but this prevents the extra call
                if (listResponse.totalNumInResultSet < 1000) yield break;
            }
        }

        static IEnumerable<(DateTime, DateTime)> DateIntervals(DateTime start, DateTime end, int intervalLengthInDays)
        {
            var currentStart = start;
            DateTime nextStart;
            while ((nextStart = currentStart.AddDays(intervalLengthInDays)) < end)
            {
                yield return (currentStart, nextStart);
                currentStart = nextStart;
            }
            yield return (currentStart, end);
        }

        static IEnumerable<AuthorizeTransaction> GetSettledTransactions(DateTime fromDate, DateTime toDate)
        {
            foreach (var (batchStartDate, batchEndDate) in DateIntervals(fromDate.ToUniversalTime(), toDate.ToUniversalTime(), 31))
            {
                var batchRequest = new getSettledBatchListRequest
                {
                    firstSettlementDate = batchStartDate,
                    lastSettlementDate = batchEndDate,
                    includeStatistics = false
                };
                var batchController = new getSettledBatchListController(batchRequest);
                var batchResponse = batchController.ExecuteAndCheckError();
                if (batchResponse.batchList is null) continue;
                foreach (var batch in batchResponse.batchList)
                {
                    foreach (var transaction in GetBatchTransactions(batch.batchId))
                    {                        
                        yield return new AuthorizeTransaction(transaction, batch);
                    }
                }
            }
        }

        static IEnumerable<transactionDetailsType> GetBatchTransactions(string batchId)
        {
            Log($"Getting settled transactions in batch {batchId}");
            // Page offset starts at 1
            var pageOffset = 0;
            while (true)
            {
                ++pageOffset;
                var transactionListRequest = new getTransactionListRequest
                {
                    batchId = batchId,
                    paging = new Paging
                    {
                        limit = 1000,
                        offset = pageOffset,
                    }
                };
                var transactionListController = new getTransactionListController(transactionListRequest);
                var transactionListResponse = transactionListController.ExecuteAndCheckError();
                if (transactionListResponse.transactions is null) yield break;
                foreach (var transaction in transactionListResponse.transactions)
                {
                    var transactionDetailsRequest = new getTransactionDetailsRequest { transId = transaction.transId };
                    var transactionDetailsController = new getTransactionDetailsController(transactionDetailsRequest);
                    var transactionDetailsResponse = transactionDetailsController.ExecuteAndCheckError();
                    yield return transactionDetailsResponse.transaction;
                }
                // If the number of results is less than the limit, this means there should be no more
                // The next call would return no transactions and break anyway, but this prevents the extra call
                if (transactionListResponse.totalNumInResultSet < 1000) yield break;
            }
        }

        static async Task<List<FeePaymentRecord>> GetPaymentRecords(IEnumerable<AuthorizeTransaction> authorizeTransactions)
        {
            var records = new List<FeePaymentRecord>();
            var transactionsGroupedByUser = authorizeTransactions
                .Where(t => t.transaction.customer?.id is not null)
                .GroupBy(t => t.transaction.customer.id);
            foreach (var (almaUserId, transactions) in transactionsGroupedByUser)
            {
                var almaUser = await AlmaClient
                    .Request("users", almaUserId)
                    .GetJsonAsync<AlmaUser>();
                var feeLookup = (await GetAllFeesForUser(almaUserId)).ToDictionary(fee => fee.Id);

                foreach (var (transaction, batch) in transactions)
                {
                    foreach (var lineItem in transaction.lineItems)
                    {
                        if (feeLookup.TryGetValue(lineItem.itemId, out var fee))
                        {
                            var feeTransactions = fee.Transaction.Where(t => t.ExternalTransactionId == transaction.transId).ToList();
                            switch (feeTransactions.Count)
                            {
                                case 1:
                                    records.Add(new FeePaymentRecord(almaUser, fee, feeTransactions[0], transaction, lineItem, batch));
                                    break;
                                case 0:
                                    LogMissingFeeTransactionError(almaUser, transaction, fee);
                                    break;
                                default:
                                    LogTooManyFeeTransactionsError(almaUser, transaction, fee);
                                    break;
                            }
                        }
                        else if (transaction.transactionStatus != "declined")
                        {
                            // A record can be added here if we are considering Authorize.net to be the source of truth rather than Alma
                            // records.Add(new FeePaymentRecord(almaUser, null, null, transaction, lineItem, batch));
                            LogMissingFeeError(almaUser, transaction, lineItem);
                        }
                    }
                }
            }
            return records;
        }

        static void LogMissingFeeTransactionError(AlmaUser almaUser, transactionDetailsType transaction, Fee fee)
        {
            Console.Error.WriteLine(string.Join(Environment.NewLine,
                "ISSUE: Failed to match an Authorize.net transaction with an Alma transaction.",
                $"Transaction Id: {transaction.transId}",
                $"Transaction Status: {transaction.transactionStatus}",
                $"Transaction Submit Time: {transaction.submitTimeUTC.ToLocalTime()}",
                $"Alma User Id: {almaUser.PrimaryId}",
                $"Alma Fee Id: {fee.Id}"));
        }

        static void LogTooManyFeeTransactionsError(AlmaUser almaUser, transactionDetailsType transaction, Fee fee)
        {
            Console.Error.WriteLine(string.Join(Environment.NewLine,
                "ISSUE: Multiple Alma transactions associated with one Authorize.net transaction.",
                $"Transaction Id: {transaction.transId}",
                $"Transaction Status: {transaction.transactionStatus}",
                $"Transaction Submit Time: {transaction.submitTimeUTC.ToLocalTime()}",
                $"Alma User Id: {almaUser.PrimaryId}",
                $"Alma Fee Id: {fee.Id}"));
        }

        static void LogMissingFeeError(AlmaUser almaUser, transactionDetailsType transaction, lineItemType lineItem)
        {
            Console.Error.WriteLine(string.Join(Environment.NewLine,
                "ISSUE: Failed to match an Authorize.net transaction with an Alma fee.",
                $"Transaction Id: {transaction.transId}",
                $"Transaction Status: {transaction.transactionStatus}",
                $"Transaction Submit Time: {transaction.submitTimeUTC.ToLocalTime()}",
                $"Alma User Id: {almaUser.PrimaryId}",
                $"Line Item Id (Expected Alma Fee Id): {lineItem.itemId}"));
        }

        static async Task<IEnumerable<Fee>> GetAllFeesForUser(string almaUserId)
        {
            var feeStatuses = new[] { "ACTIVE", "CLOSED", "EXPORTED", "INDISPUTE" };
            var allFees = await Task.WhenAll(feeStatuses.Select(async status =>
            {
                var fees = await AlmaClient
                    .Request("users", almaUserId, "fees")
                    .SetQueryParam("status", status)
                    .GetJsonAsync<AlmaFees>();
                return fees.Fee ?? new Fee[0];
            }));
            return allFees.SelectMany(fees => fees);
        }

        static async Task UpdateDatabase(IDbConnection connection, Schema schema, List<FeePaymentRecord> records)
        {
            if (connection is null) return;
            if (records.Count == 0)
            {
                Log("No transactions in this date range.");
                return;
            }
            Log($"Updating table with {records.Count} records.");
            await connection.ExecuteAsync(schema.InsertDataSql(), records);
        }

        static async Task MigrateTable(IDbConnection connection, Schema currentSchema, Schema newSchema) {
            if (currentSchema.Version == newSchema.Version) return;
            Log($"Migrating table from version {currentSchema.Version} to {newSchema.Version}.");
            await connection.ExecuteAsync(newSchema.MigrationSql(currentSchema));
        }

        // The Authorize.net API has a handful of different ways it signals an error condition
        // This extension method is meant to try and encapsulate all of them
        static TResponse ExecuteAndCheckError<TRequest, TResponse>(this ApiOperationBase<TRequest, TResponse> controller)
        where TRequest : ANetApiRequest
        where TResponse : ANetApiResponse
        {
            var response = controller.ExecuteWithApiResponse();
            if (response is null)
            {
                var errorResponse = controller.GetErrorResponse();
                if (errorResponse is null)
                {
                    throw new Exception("Authorize.net API Error: no response.");
                }
                else
                {
                    throw new Exception($"Authorize.net API Error: {string.Join(Environment.NewLine, errorResponse.messages.message.Select(m => $"{m.code}: {m.text}"))}");
                }
            }
            else if (response.messages.resultCode == messageTypeEnum.Error)
            {
                throw new Exception($"Authorize.net API Error: {string.Join(Environment.NewLine, response.messages.message.Select(m => $"{m.code}: {m.text}"))}");
            }
            return response;
        }

        // Extension method providing syntactic sugar for extracting the key from an IGrouping
        static void Deconstruct<TKey, TValue>(this IGrouping<TKey, TValue> grouping, out TKey key, out IEnumerable<TValue> values)
        {
            key = grouping.Key;
            values = grouping;
        }
    }
}
