using System;
using AuthorizeNet.Api.Contracts.V1;

namespace alma_authorizenet_payment_reporting
{

    public class AeonFeePaymentRecord
    {
        public string AuthorizeTransactionId { get; }
        public DateTime TransactionSubmitTime { get; }
        public DateTime? TransactionSettledTime { get; }
        public string TransactionStatus { get; }
        public string SettlementState { get; }
        public string AeonTransactionNumbers { get; }
        public decimal PaymentAmount { get; }

        public AeonFeePaymentRecord(transactionDetailsType authorizeTransaction, batchDetailsType batch)
        {
            AuthorizeTransactionId = authorizeTransaction.transId;
            TransactionSubmitTime = authorizeTransaction.submitTimeUTC;
            TransactionSettledTime = batch?.settlementTimeUTC;
            TransactionStatus = authorizeTransaction.transactionStatus;
            SettlementState = batch?.settlementState;
            AeonTransactionNumbers = authorizeTransaction.order.description.Replace("Payment for Aeon request(s) ", "");
            PaymentAmount = authorizeTransaction.authAmount;
        }
    }
}