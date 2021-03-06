// Generated by https://quicktype.io

namespace alma_authorizenet_payment_reporting.AlmaTypes
{
    using System;
    using Newtonsoft.Json;

    public partial class AlmaFees
    {
        [JsonProperty("fee")]
        public Fee[] Fee { get; set; }

        [JsonProperty("total_record_count")]
        public long TotalRecordCount { get; set; }

        [JsonProperty("total_sum")]
        public double TotalSum { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }
    }

    public partial class Fee
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public ValueDesc Type { get; set; }

        [JsonProperty("status")]
        public ValueDesc Status { get; set; }

        [JsonProperty("user_primary_id")]
        public ValueLink UserPrimaryId { get; set; }

        [JsonProperty("balance")]
        public double Balance { get; set; }

        [JsonProperty("remaining_vat_amount")]
        public double RemainingVatAmount { get; set; }

        [JsonProperty("original_amount")]
        public double OriginalAmount { get; set; }

        [JsonProperty("original_vat_amount")]
        public double OriginalVatAmount { get; set; }

        [JsonProperty("creation_time")]
        public DateTimeOffset CreationTime { get; set; }

        [JsonProperty("status_time")]
        public DateTimeOffset StatusTime { get; set; }

        [JsonProperty("comment")]
        public object Comment { get; set; }

        [JsonProperty("owner")]
        public ValueDesc Owner { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("barcode")]
        public ValueLink Barcode { get; set; }

        [JsonProperty("transaction")]
        public Transaction[] Transaction { get; set; }

        [JsonProperty("link")]
        public Uri Link { get; set; }
    }

    public partial class ValueLink
    {
        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("link")]
        public Uri Link { get; set; }
    }

    public partial class ValueDesc
    {
        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("desc")]
        public string Desc { get; set; }
    }

    public partial class Transaction
    {
        [JsonProperty("type")]
        public ValueDesc Type { get; set; }

        [JsonProperty("amount")]
        public double Amount { get; set; }

        [JsonProperty("vat_amount")]
        public double VatAmount { get; set; }

        [JsonProperty("created_by")]
        public string CreatedBy { get; set; }

        [JsonProperty("external_transaction_id")]
        public string ExternalTransactionId { get; set; }

        [JsonProperty("transaction_time")]
        public DateTimeOffset TransactionTime { get; set; }

        [JsonProperty("received_by")]
        public ValueDesc ReceivedBy { get; set; }

        [JsonProperty("payment_method")]
        public ValueDesc PaymentMethod { get; set; }
    }

    public partial class AlmaUser
    {
        [JsonProperty("record_type")]
        public ValueDesc RecordType { get; set; }

        [JsonProperty("primary_id")]
        public string PrimaryId { get; set; }

        [JsonProperty("first_name")]
        public string FirstName { get; set; }

        [JsonProperty("middle_name")]
        public string MiddleName { get; set; }

        [JsonProperty("last_name")]
        public string LastName { get; set; }

        [JsonProperty("full_name")]
        public string FullName { get; set; }

        [JsonProperty("user_title")]
        public ValueDesc UserTitle { get; set; }

        [JsonProperty("job_category")]
        public ValueDesc JobCategory { get; set; }

        [JsonProperty("job_description")]
        public string JobDescription { get; set; }

        [JsonProperty("gender")]
        public ValueDesc Gender { get; set; }

        [JsonProperty("user_group")]
        public ValueDesc UserGroup { get; set; }

        [JsonProperty("campus_code")]
        public ValueDesc CampusCode { get; set; }

        [JsonProperty("web_site_url")]
        public string WebSiteUrl { get; set; }

        [JsonProperty("cataloger_level")]
        public ValueDesc CatalogerLevel { get; set; }

        [JsonProperty("preferred_language")]
        public ValueDesc PreferredLanguage { get; set; }

        [JsonProperty("expiry_date")]
        public string ExpiryDate { get; set; }

        [JsonProperty("purge_date")]
        public string PurgeDate { get; set; }

        [JsonProperty("account_type")]
        public ValueDesc AccountType { get; set; }

        [JsonProperty("external_id")]
        public string ExternalId { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("force_password_change")]
        public string ForcePasswordChange { get; set; }

        [JsonProperty("status")]
        public ValueDesc Status { get; set; }

        [JsonProperty("status_date")]
        public string StatusDate { get; set; }

        [JsonProperty("requests")]
        public object Requests { get; set; }

        [JsonProperty("loans")]
        public object Loans { get; set; }

        [JsonProperty("fees")]
        public object Fees { get; set; }

        [JsonProperty("contact_info")]
        public ContactInfo ContactInfo { get; set; }

        [JsonProperty("pref_first_name")]
        public string PrefFirstName { get; set; }

        [JsonProperty("pref_middle_name")]
        public string PrefMiddleName { get; set; }

        [JsonProperty("pref_last_name")]
        public string PrefLastName { get; set; }

        [JsonProperty("pref_name_suffix")]
        public string PrefNameSuffix { get; set; }

        [JsonProperty("is_researcher")]
        public bool IsResearcher { get; set; }

        [JsonProperty("researcher")]
        public object Researcher { get; set; }

        [JsonProperty("link")]
        public object Link { get; set; }

        [JsonProperty("user_identifier")]
        public UserIdentifier[] UserIdentifier { get; set; }

        [JsonProperty("user_role")]
        public UserRole[] UserRole { get; set; }

        [JsonProperty("user_block")]
        public object[] UserBlock { get; set; }

        [JsonProperty("user_note")]
        public object[] UserNote { get; set; }

        [JsonProperty("user_statistic")]
        public UserStatistic[] UserStatistic { get; set; }

        [JsonProperty("proxy_for_user")]
        public object[] ProxyForUser { get; set; }
    }

    public partial class ContactInfo
    {
        [JsonProperty("address")]
        public Address[] Address { get; set; }

        [JsonProperty("email")]
        public Email[] Email { get; set; }

        [JsonProperty("phone")]
        public Phone[] Phone { get; set; }
    }

    public partial class Address
    {
        [JsonProperty("line1")]
        public string Line1 { get; set; }

        [JsonProperty("line2")]
        public string Line2 { get; set; }

        [JsonProperty("line3")]
        public object Line3 { get; set; }

        [JsonProperty("line4")]
        public object Line4 { get; set; }

        [JsonProperty("line5")]
        public object Line5 { get; set; }

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("state_province")]
        public string StateProvince { get; set; }

        [JsonProperty("postal_code")]
        public string PostalCode { get; set; }

        [JsonProperty("country")]
        public ValueDesc Country { get; set; }

        [JsonProperty("address_note")]
        public string AddressNote { get; set; }

        [JsonProperty("preferred")]
        public bool Preferred { get; set; }

        [JsonProperty("segment_type")]
        public string SegmentType { get; set; }

        [JsonProperty("address_type")]
        public ValueDesc[] AddressType { get; set; }
    }

    public partial class Email
    {
        [JsonProperty("email_address")]
        public string EmailAddress { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("preferred")]
        public bool Preferred { get; set; }

        [JsonProperty("segment_type")]
        public string SegmentType { get; set; }

        [JsonProperty("email_type")]
        public ValueDesc[] EmailType { get; set; }
    }

    public partial class Phone
    {
        [JsonProperty("phone_number")]
        public string PhoneNumber { get; set; }

        [JsonProperty("preferred")]
        public bool Preferred { get; set; }

        [JsonProperty("preferred_sms")]
        public bool PreferredSms { get; set; }

        [JsonProperty("segment_type")]
        public string SegmentType { get; set; }

        [JsonProperty("phone_type")]
        public ValueDesc[] PhoneType { get; set; }
    }

    public partial class UserIdentifier
    {
        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("id_type")]
        public ValueDesc IdType { get; set; }

        [JsonProperty("note")]
        public string Note { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("segment_type")]
        public string SegmentType { get; set; }
    }

    public partial class UserRole
    {
        [JsonProperty("status")]
        public ValueDesc Status { get; set; }

        [JsonProperty("scope")]
        public ValueDesc Scope { get; set; }

        [JsonProperty("role_type")]
        public ValueDesc RoleType { get; set; }

        [JsonProperty("parameter")]
        public Parameter[] Parameter { get; set; }
    }

    public partial class Parameter
    {
        [JsonProperty("type")]
        public ValueDesc Type { get; set; }

        [JsonProperty("scope")]
        public ValueDesc Scope { get; set; }

        [JsonProperty("value")]
        public ValueDesc Value { get; set; }
    }

    public partial class UserStatistic
    {
        [JsonProperty("statistic_category")]
        public ValueDesc StatisticCategory { get; set; }

        [JsonProperty("category_type")]
        public ValueDesc CategoryType { get; set; }

        [JsonProperty("statistic_note")]
        public string StatisticNote { get; set; }

        [JsonProperty("segment_type")]
        public string SegmentType { get; set; }
    }
}
