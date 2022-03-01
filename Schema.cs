using System;

namespace alma_authorizenet_payment_reporting
{
    public enum SchemaVersion
    {
        V1 = 1,
        V2
    }

    public abstract class Schema
    {
        public SchemaVersion Version { get; }
        public string TableName { get; }

        public Schema(SchemaVersion version, string tableName)
        {
            Version = version;
            TableName = tableName;
        }

        public static Schema Get(SchemaVersion version, string tableName) =>
            version switch
            {
                SchemaVersion.V1 => new SchemaV1(tableName),
                SchemaVersion.V2 => new SchemaV2(tableName),
                _ => throw new ArgumentException("Unrecognized schema version."),
            };

        public abstract string TableCreationSql();
        public abstract string InsertDataSql();
        public abstract string MigrationSql(Schema currentSchema);
    }

    class SchemaV1 : Schema
    {
        public SchemaV1(string tableName) : base(SchemaVersion.V1, tableName) { }

        public override string InsertDataSql() => $@"
            begin
                insert into {TableName}
                (
                    AlmaFeeId,
                    AuthorizeTransactionId,
                    TransactionSubmitTime,
                    PatronUserId,
                    PatronName,
                    PaymentCategory,
                    PaymentAmount
                )
                values
                (
                    :AlmaFeeId,
                    :AuthorizeTransactionId,
                    :TransactionSubmitTime,
                    :PatronUserId,
                    :PatronName,
                    :PaymentCategory,
                    :PaymentAmount
                );
            exception when dup_val_on_index then
                update {TableName} set
                    TransactionSubmitTime = :TransactionSubmitTime,
                    PatronUserId = :PatronUserId,
                    PatronName = :PatronName,
                    PaymentCategory = :PaymentCategory,
                    PaymentAmount = :PaymentAmount
                where
                    AlmaFeeId = :AlmaFeeId and
                    AuthorizeTransactionId = :AuthorizeTransactionId;
            end;";

        public override string MigrationSql(Schema currentSchema)
        {
            if (currentSchema.Version == this.Version)
                return null;
            else if (currentSchema.Version == SchemaVersion.V2)
            {
                return $@"
                    alter table {TableName} drop (
                        TransactionSettledTime,
                        TransactionStatus,
                        SettlementState
                    )";
            }
            else
                throw new NotImplementedException();
        }

        public override string TableCreationSql() => $@"
            create table {TableName}
            (
                AlmaFeeId varchar2(100),
                AuthorizeTransactionId varchar2(100),
                TransactionSubmitTime date,
                PatronUserId varchar2(100),
                PatronName varchar2(100),
                PaymentCategory varchar2(100),
                PaymentAmount number,
                primary key(AlmaFeeId, AuthorizeTransactionId)
            )
        ";
    }

    class SchemaV2 : Schema
    {
        public SchemaV2(string tableName) : base(SchemaVersion.V2, tableName) { }

        public override string InsertDataSql() => $@"
            begin
                insert into {TableName}
                (
                    AlmaFeeId,
                    AuthorizeTransactionId,
                    TransactionSubmitTime,
                    TransactionSettledTime,
                    TransactionStatus,
                    SettlementState,
                    PatronUserId,
                    PatronName,
                    PaymentCategory,
                    PaymentAmount
                )
                values
                (
                    :AlmaFeeId,
                    :AuthorizeTransactionId,
                    :TransactionSubmitTime,
                    :TransactionSettledTime,
                    :TransactionStatus,
                    :SettlementState,
                    :PatronUserId,
                    :PatronName,
                    :PaymentCategory,
                    :PaymentAmount
                );
            exception when dup_val_on_index then
                update {TableName} set
                    TransactionSubmitTime = :TransactionSubmitTime,
                    TransactionSettledTime = :TransactionSettledTime,
                    TransactionStatus = :TransactionStatus,
                    SettlementState = :SettlementState,
                    PatronUserId = :PatronUserId,
                    PatronName = :PatronName,
                    PaymentCategory = :PaymentCategory,
                    PaymentAmount = :PaymentAmount
                where
                    AlmaFeeId = :AlmaFeeId and
                    AuthorizeTransactionId = :AuthorizeTransactionId;
            end;";

        public override string MigrationSql(Schema currentSchema)
        {
            if (currentSchema.Version == this.Version)
                return null;
            else if (currentSchema.Version == SchemaVersion.V1)
            {
                return $@"
                    alter table {TableName} add (
                        TransactionSettledTime date null,
                        TransactionStatus varchar2(100),
                        SettlementState varchar2(100) null
                    )";
            }
            else
                throw new NotImplementedException();
        }


        public override string TableCreationSql() => $@"
            create table {TableName}
            (
                AlmaFeeId varchar2(100),
                AuthorizeTransactionId varchar2(100),
                TransactionSubmitTime date,
                TransactionSettledTime date null,
                TransactionStatus varchar2(100),
                SettlementState varchar2(100) null,
                PatronUserId varchar2(100),
                PatronName varchar2(100),
                PaymentCategory varchar2(100),
                PaymentAmount number,
                primary key(AlmaFeeId, AuthorizeTransactionId)
            )
        ";
    }
}