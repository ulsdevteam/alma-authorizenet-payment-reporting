using System;
using alma_authorizenet_payment_reporting.AlmaTypes;
using AuthorizeNet.Api.Contracts.V1;

namespace alma_authorizenet_payment_reporting
{

    public class FeePaymentRecord
    {
        public DateTime TransactionSubmitTime { get; }
        public string PatronName { get; }
        public string PaymentCategory { get; }
        public decimal PaymentAmount { get; }
        public string PatronUserId { get; }
        public string AlmaFeeId { get; }
        public string AuthorizeTransactionId { get; }

        public FeePaymentRecord(AlmaUser user, Fee almaFee, Transaction almaTransaction, transactionDetailsType authorizeTransaction, lineItemType lineItem)
        {
            TransactionSubmitTime = authorizeTransaction.submitTimeUTC;
            // TODO: should this be the alma user name, or the name in the authorize.net billing details?
            PatronName = user.FullName;
            PaymentCategory = almaFee.Type.Value;
            PaymentAmount = Convert.ToDecimal(almaTransaction.Amount);
            PatronUserId = user.PrimaryId;
            AlmaFeeId = almaFee.Id;
            AuthorizeTransactionId = authorizeTransaction.transId;
        }
    }
}