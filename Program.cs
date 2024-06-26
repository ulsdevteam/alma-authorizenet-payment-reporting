﻿using System;
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
using Newtonsoft.Json;

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
                        var schema = Schema.Get(options.SchemaVersion, Config);
                        Log($"Using schema version {schema.Version}.");
                        await EnsureTablesExist(connection, schema);
                        var transactions = GetSettledTransactions(
                            options.FromDate 
                                ?? (await GetMostRecentTransactionDate(connection, schema))?.AddDays(-2) 
                                ?? DateTime.Today.AddMonths(-1),
                            options.ToDate ?? DateTime.Today);
                        var records = await GetPaymentRecords(transactions);
                        await UpdateDatabase(connection, schema, records);
                        if (options.DryRun) {
                            foreach (var (table, tableRecords) in records)
                            {
                                Log($"{table}: Got {tableRecords.Count()} records.");
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Console.Error.WriteLine(e);
                    }
                },
                async (MigrateOptions migrateOptions) => { 
                    Log = migrateOptions.Log ? Console.WriteLine : (_) => { };
                    using var connection = new OracleConnection(Config["CONNECTION_STRING"]);
                    var currentSchema = Schema.Get(migrateOptions.CurrentSchema, Config);
                    var newSchema = Schema.Get(migrateOptions.NewSchema, Config);
                    await MigrateTable(connection, currentSchema, newSchema);
                },
                errors => Task.CompletedTask
                )
                ;
        }

        static bool IsAlmaTransaction(AuthorizeTransaction transaction) => transaction.transaction.order.invoiceNumber.StartsWith("ALMA");
        static bool IsAeonTransaction(AuthorizeTransaction transaction) => transaction.transaction.order.description.StartsWith("Payment for Aeon request(s)");

        static async Task EnsureTablesExist(IDbConnection connection, Schema schema)
        {
            if (connection is null) return;
            foreach (Table table in schema.GetTables()){
                var tableName = schema.GetName(table);
                Log($"Checking if table '{tableName}' exists.");
                var result = await connection.QueryAsync(@"
                    select table_name
                    from user_tables
                    where table_name = :tableName
                ", new { tableName });
                if (!result.Any())
                {
                    Log("Table does not exist, creating table.");
                    await connection.ExecuteAsync(schema.TableCreationSql(table));
                }
                else
                {
                    Log("Table exists.");
                }
            }
        }

        static async Task<DateTime?> GetMostRecentTransactionDate(IDbConnection connection, Schema schema)
        {
            if (connection is null) return null;
            Log("Getting most recent transaction date");
            return (await Task.WhenAll(schema.GetTables().Select(table => connection.QueryFirstOrDefaultAsync<DateTime?>($@"
                select TransactionSubmitTime from {schema.GetName(table)}
                order by TransactionSubmitTime desc
                fetch next 1 rows only")))).Max();
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

        static async Task<Dictionary<Table, IEnumerable<object>>> GetPaymentRecords(IEnumerable<AuthorizeTransaction> authorizeTransactions)
        {
            var almaPayments = new List<AlmaFeePaymentRecord>();
            var aeonPayments = new List<AeonFeePaymentRecord>();
            var (almaTransactions, nonAlmaTransactions) = authorizeTransactions
                .Where(t => t.transaction.transactionType == "authCaptureTransaction" 
                        && t.transaction.transactionStatus != "declined")
                .SplitBy(IsAlmaTransaction);
            var (aeonTransactions, unrecognizedTransactions) = nonAlmaTransactions.SplitBy(IsAeonTransaction);
            foreach (var transaction in unrecognizedTransactions)
            {
                LogUnrecognizedTransaction(transaction);
            }
            aeonPayments.AddRange(aeonTransactions.Select(transaction => new AeonFeePaymentRecord(transaction)));
            var transactionsGroupedByUser = almaTransactions.GroupBy(t => t.transaction.customer?.id);
            foreach (var (almaUserId, transactions) in transactionsGroupedByUser)
            {
                if (almaUserId is null)
                {
                    LogMissingUserIdTransactions(transactions);
                    continue;
                }
                var almaUser = await AlmaClient
                    .Request("users", almaUserId)
                    .GetJsonAsync<AlmaUser>();
                var feeLookup = (await GetAllFeesForUser(almaUserId)).ToDictionary(fee => fee.Id);

                foreach (var (transaction, batch) in transactions)
                {
                    if (transaction.lineItems is null)
                    {
                        LogMissingLineItemsError(almaUser, transaction);
                        continue;
                    }
                    foreach (var lineItem in transaction.lineItems)
                    {
                        if (feeLookup.TryGetValue(lineItem.itemId, out var fee))
                        {
                            var feeTransactions = fee.Transaction?
                                .Where(t => t.ExternalTransactionId == transaction.transId).ToList()
                                ?? Enumerable.Empty<Transaction>();
                            switch (feeTransactions.Count())
                            {
                                case 1:
                                    almaPayments.Add(new AlmaFeePaymentRecord(almaUser, fee, feeTransactions.Single(), transaction, lineItem, batch));
                                    break;
                                case 0:
                                    LogMissingFeeTransactionError(almaUser, transaction, fee);
                                    break;
                                default:
                                    LogTooManyFeeTransactionsError(almaUser, transaction, fee);
                                    break;
                            }
                        }
                        else
                        {
                            LogMissingFeeError(almaUser, transaction, lineItem);
                        }
                    }
                }
            }
            return new Dictionary<Table, IEnumerable<object>>{
                [Table.Alma] = almaPayments,
                [Table.Aeon] = aeonPayments
            };
        }

        static void LogUnrecognizedTransaction(AuthorizeTransaction transaction)
        {
            Console.Error.WriteLine($"NOTICE: Unrecognized Transaction:{Environment.NewLine}{JsonConvert.SerializeObject(transaction, Formatting.Indented)}");
        }

        static void LogMissingUserIdTransactions(IEnumerable<AuthorizeTransaction> transactions)
        {
            Console.Error.WriteLine($"ISSUE: Alma Transactions missing Alma User Id:{Environment.NewLine}{JsonConvert.SerializeObject(transactions.ToList(), Formatting.Indented)}");
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

        static void LogMissingLineItemsError(AlmaUser almaUser, transactionDetailsType transaction)
        {
            Console.Error.WriteLine(string.Join(Environment.NewLine,
                "ISSUE: Alma transaction with no line items (this shouldn't happen)",
                $"Transaction Id: {transaction.transId}",
                $"Transaction Status: {transaction.transactionStatus}",
                $"Transaction Submit Time: {transaction.submitTimeUTC.ToLocalTime()}",
                $"Alma User Id: {almaUser.PrimaryId}"));
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
                return fees.Fee ?? Array.Empty<Fee>();
            }));
            return allFees.SelectMany(fees => fees);
        }

        static async Task UpdateDatabase(IDbConnection connection, Schema schema, Dictionary<Table, IEnumerable<object>> records)
        {
            if (connection is null) return;
            foreach (var table in schema.GetTables())
            {
                 if (!records[table].Any())
                {
                    Log($"No {table} transactions in this date range.");
                    continue;
                }
                Log($"Updating table {schema.GetName(table)} with {records[table].Count()} records.");
                await connection.ExecuteAsync(schema.InsertDataSql(table), records[table]);
            }
           
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

        // Extension method to split a list into two based on a predicate
        static (List<T>, List<T>) SplitBy<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            var trueList = new List<T>();
            var falseList = new List<T>();
            foreach (var item in source) { (predicate(item) ? trueList : falseList).Add(item); }
            return (trueList, falseList);
        }
    }
}
