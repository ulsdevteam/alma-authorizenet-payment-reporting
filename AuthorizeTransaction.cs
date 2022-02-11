using AuthorizeNet.Api.Contracts.V1;

namespace alma_authorizenet_payment_reporting
{
    public record AuthorizeTransaction(transactionDetailsType transaction, batchDetailsType batch);
}