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
        public string PatronName { get; }
        public string PaymentCategory { get; }
        public decimal PaymentAmount { get; }
        public string PatronUserId { get; }

        public FeePaymentRecord(AlmaUser user, Fee almaFee, Transaction almaTransaction, transactionDetailsType authorizeTransaction, lineItemType lineItem)
        {
            AlmaFeeId = almaFee.Id;
            AuthorizeTransactionId = authorizeTransaction.transId;
            TransactionSubmitTime = authorizeTransaction.submitTimeUTC;
            PatronUserId = user.PrimaryId;
            PatronName = user.FullName;
            PaymentCategory = almaFee.Type.Value;
            PaymentAmount = Convert.ToDecimal(almaTransaction.Amount);
        }
    }
}