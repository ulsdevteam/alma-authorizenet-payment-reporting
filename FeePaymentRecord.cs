using System;
using alma_authorizenet_payment_reporting.AlmaTypes;
using AuthorizeNet.Api.Contracts.V1;

namespace alma_authorizenet_payment_reporting
{

    public class FeePaymentRecord
    {
        public string AlmaFeeId { get; }
        public string AuthorizeTransactionId { get; }
        public DateTime TransactionSubmitTime { get; }
        public DateTime? TransactionSettledTime { get; }
        public string TransactionStatus { get; }
        public string SettlementState { get; }
        public string PatronName { get; }
        public string PaymentCategory { get; }
        public decimal PaymentAmount { get; }
        public string PatronUserId { get; }

        public FeePaymentRecord(AlmaUser user, Fee almaFee, Transaction almaTransaction, transactionDetailsType authorizeTransaction, lineItemType lineItem, batchDetailsType batch)
        {
            AlmaFeeId = almaFee?.Id ?? lineItem.itemId;
            AuthorizeTransactionId = authorizeTransaction.transId;
            TransactionSubmitTime = authorizeTransaction.submitTimeUTC;
            TransactionSettledTime = batch?.settlementTimeUTC;
            TransactionStatus = authorizeTransaction.transactionStatus;
            SettlementState = batch?.settlementState;
            PatronUserId = user.PrimaryId;
            PatronName = user.FullName;
            PaymentCategory = almaFee?.Type.Value ?? lineItem.name;
            PaymentAmount = almaTransaction is not null ? Convert.ToDecimal(almaTransaction.Amount) : lineItem.unitPrice;
        }
    }
}