using System;
using System.Collections.Generic;
using System.Linq;
using alma_authorizenet_payment_reporting.AlmaTypes;
using dotenv.net;
using Microsoft.Extensions.Configuration;
using AuthorizeNet.Api.Controllers;
using AuthorizeNet.Api.Contracts.V1;
using AuthorizeNet.Api.Controllers.Bases;
using Flurl.Http;
using System.Threading.Tasks;

namespace alma_authorizenet_payment_reporting
{
    static class Program
    {
        private static IConfiguration Config { get; set; }
        private static IFlurlClient AlmaClient { get; set; }

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
            ApiOperationBase<ANetApiRequest, ANetApiResponse>.RunEnvironment = Config["AUTHORIZE_ENVIRONMENT"]?.ToUpper() switch {
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

            // TODO: Get not-yet-settled transactions, make sure transactions are not duplicated
            var transactionsGroupedByUser = GetSettledTransactions(DateTime.Today.AddMonths(-4), DateTime.Today)
                .Where(t => t.customer?.id is not null)
                .GroupBy(t => t.customer.id);
            foreach (var (almaUserId, transactions) in transactionsGroupedByUser)
            {
                var almaUser = await AlmaClient
                    .Request("users", almaUserId)
                    .GetJsonAsync<AlmaUser>();
                var userActiveFees = await AlmaClient
                    .Request("users", almaUserId, "fees")
                    .SetQueryParam("status", "ACTIVE")
                    .GetJsonAsync<AlmaFees>();
                var userClosedFees = await AlmaClient
                    .Request("users", almaUserId, "fees")
                    .SetQueryParam("status", "CLOSED")
                    .GetJsonAsync<AlmaFees>();

                // TODO: this ought to be more legible, also seems to not be producing enough records
                var feeTransactions =
                    from aNetTransaction in transactions
                    from lineItem in aNetTransaction.lineItems
                    from fee in userActiveFees.Fee.Concat(userClosedFees.Fee)
                    from almaTransaction in fee.Transaction
                    where lineItem.itemId == fee.Id
                    where aNetTransaction.transId == almaTransaction.ExternalTransactionId
                    select new FeePaymentRecord(almaUser, fee, almaTransaction, aNetTransaction, lineItem);
                feeTransactions = feeTransactions.ToList();

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

        static IEnumerable<transactionDetailsType> GetUnsettledTransactions()
        {
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

        static IEnumerable<transactionDetailsType> GetSettledTransactions(DateTime fromDate, DateTime toDate)
        {
            foreach (var (batchStartDate, batchEndDate) in DateIntervals(fromDate, toDate, 31))
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
                foreach (var transaction in GetBatchTransactions(batch.batchId))
                {
                    yield return transaction;
                }
            }
        }

        static IEnumerable<transactionDetailsType> GetBatchTransactions(string batchId)
        {
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
