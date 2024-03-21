using System;

namespace alma_authorizenet_payment_reporting
{

    public class AeonFeePaymentRecord
    {
        public string AuthorizeTransactionId { get; }
        public DateTime TransactionSubmitTime { get; }
        public DateTime? TransactionSettledTime { get; }
        public string TransactionStatus { get; }
        public string SettlementState { get; }
        public string PatronName { get; }
        public string AeonTransactionNumbers { get; }
        public decimal PaymentAmount { get; }

        public AeonFeePaymentRecord(AuthorizeTransaction transaction)
        {
            AuthorizeTransactionId = transaction.transaction.transId;
            TransactionSubmitTime = transaction.transaction.submitTimeUTC;
            TransactionSettledTime = transaction.batch?.settlementTimeUTC;
            TransactionStatus = transaction.transaction.transactionStatus;
            SettlementState = transaction.batch?.settlementState;
            PatronName = transaction.transaction.billTo.firstName + ' ' + transaction.transaction.billTo.lastName;
            AeonTransactionNumbers = transaction.transaction.order.description.Replace("Payment for Aeon request(s) ", "");
            PaymentAmount = transaction.transaction.authAmount;
        }
    }
}