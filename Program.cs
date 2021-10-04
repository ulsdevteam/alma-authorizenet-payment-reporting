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
        public static IConfiguration Config { get; private set; }

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
            ApiOperationBase<ANetApiRequest, ANetApiResponse>.RunEnvironment = AuthorizeNet.Environment.SANDBOX;

            var almaClient = new FlurlClient("https://api-na.hosted.exlibrisgroup.com/almaws/v1");
            almaClient.BeforeCall(call => call.Request
                .WithHeader("Accept", "application/json")
                .SetQueryParam("apikey", Config["ALMA_API_KEY"]));

            // TODO: Get not-yet-settled transactions, make sure transactions are not duplicated
            var transactionsGroupedByUser = GetSettledTransactions(DateTime.Today.AddMonths(-4), DateTime.Today)
                .Where(t => t.customer?.id is not null)
                .GroupBy(t => t.customer.id);
            foreach (var (almaUserId, transactions) in transactionsGroupedByUser)
            {
                var almaUser = await almaClient
                    .Request("users", almaUserId)
                    .GetJsonAsync<AlmaUser>();
                var userActiveFees = await almaClient
                    .Request("users", almaUserId, "fees")
                    .SetQueryParam("status", "ACTIVE")
                    .GetJsonAsync<AlmaFees>();
                var userClosedFees = await almaClient
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
            while ((nextStart = currentStart.AddDays(intervalLengthInDays)) < end) {
                yield return (currentStart, nextStart);
                currentStart = nextStart;
            }
            yield return (currentStart, end);
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
                {
                    var transactionListRequest = new getTransactionListRequest { batchId = batch.batchId };
                    var transactionListController = new getTransactionListController(transactionListRequest);
                    var transactionListResponse = transactionListController.ExecuteAndCheckError();
                    if (transactionListResponse.transactions is null) continue;
                    foreach (var transaction in transactionListResponse.transactions)
                    {
                        var transactionDetailsRequest = new getTransactionDetailsRequest { transId = transaction.transId };
                        var transactionDetailsController = new getTransactionDetailsController(transactionDetailsRequest);
                        var transactionDetailsResponse = transactionDetailsController.ExecuteAndCheckError();
                        yield return transactionDetailsResponse.transaction;
                    }
                }
            }            
        }

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

        static void Deconstruct<TKey, TValue>(this IGrouping<TKey, TValue> grouping, out TKey key, out IEnumerable<TValue> values)
        {
            key = grouping.Key;
            values = grouping;
        }
    }
}
