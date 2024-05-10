using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace alma_authorizenet_payment_reporting
{
    public enum SchemaVersion
    {
        V1 = 1,
        V2,
        V3
    }

    public enum Table 
    {
        Alma,
        Aeon
    }

    public abstract class Schema
    {
        public SchemaVersion Version { get; }
        public string AlmaTableName { get; }
        public string AeonTableName { get; }

        public Schema(SchemaVersion version, string almaTableName, string aeonTableName = null)
        {
            Version = version;
            AlmaTableName = almaTableName;
            AeonTableName = aeonTableName;
        }

        public static Schema Get(SchemaVersion version, IConfiguration config) =>
            version switch
            {
                SchemaVersion.V1 => new SchemaV1(config["ALMA_TABLE_NAME"]),
                SchemaVersion.V2 => new SchemaV2(config["ALMA_TABLE_NAME"]),
                SchemaVersion.V3 => new SchemaV3(config["ALMA_TABLE_NAME"], config["AEON_TABLE_NAME"]),
                _ => throw new ArgumentException("Unrecognized schema version."),
            };

        public abstract bool Supports(Table table);
        public abstract string TableCreationSql(Table table);
        public abstract string InsertDataSql(Table table);
        public abstract string MigrationSql(Schema currentSchema);

        public string GetName(Table table) => table switch
        {
            Table.Alma => AlmaTableName,
            Table.Aeon => AeonTableName,
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };

        public IEnumerable<Table> GetTables()
        {
            foreach (Table table in Enum.GetValues(typeof(Table)))
            {
                if (Supports(table))
                {
                    yield return table;
                }
            }
        }

        protected void EnsureSupported(Table table)
        {
            if (!Supports(table)) {
                throw new InvalidOperationException($"Table '{table}' not supported by this schema version ({Version}).");
            }
        }
    }

    class SchemaV1 : Schema
    {
        public SchemaV1(string tableName) : base(SchemaVersion.V1, tableName) { }

        public override bool Supports(Table table) => table == Table.Alma;
       
        public override string InsertDataSql(Table table)
        {
            EnsureSupported(table);
            return $@"
            begin
                insert into {AlmaTableName}
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
                update {AlmaTableName} set
                    TransactionSubmitTime = :TransactionSubmitTime,
                    PatronUserId = :PatronUserId,
                    PatronName = :PatronName,
                    PaymentCategory = :PaymentCategory,
                    PaymentAmount = :PaymentAmount
                where
                    AlmaFeeId = :AlmaFeeId and
                    AuthorizeTransactionId = :AuthorizeTransactionId;
            end;";
        }

        public override string MigrationSql(Schema currentSchema)
        {
            if (currentSchema.Version == this.Version)
                return null;
            else if (currentSchema.Version == SchemaVersion.V2)
            {
                return $@"
                    alter table {AlmaTableName} drop (
                        TransactionSettledTime,
                        TransactionStatus,
                        SettlementState
                    )";
            }
            else
                throw new NotImplementedException();
        }

        public override string TableCreationSql(Table table)
        {
            EnsureSupported(table);
            return $@"
            create table {AlmaTableName}
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
    }

    class SchemaV2 : Schema
    {
        public SchemaV2(string tableName) : base(SchemaVersion.V2, tableName) { }

        public override bool Supports(Table table) => table == Table.Alma;

        public override string InsertDataSql(Table table)
        {
            EnsureSupported(table);
            return $@"
            begin
                insert into {AlmaTableName}
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
                update {AlmaTableName} set
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
        }

        public override string MigrationSql(Schema currentSchema)
        {
            if (currentSchema.Version == this.Version)
                return null;
            else if (currentSchema.Version == SchemaVersion.V1)
            {
                return $@"
                    alter table {AlmaTableName} add (
                        TransactionSettledTime date null,
                        TransactionStatus varchar2(100),
                        SettlementState varchar2(100) null
                    )";
            }
            else
                throw new NotImplementedException();
        }


        public override string TableCreationSql(Table table)
        {
            EnsureSupported(table);
            return $@"
            create table {AlmaTableName}
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
            )";
        }
    }

    class SchemaV3 : Schema
    {
        private SchemaV2 AlmaSchema { get; }
        public SchemaV3(string almaTableName, string aeonTableName) : base(SchemaVersion.V3, almaTableName, aeonTableName)
        {
            AlmaSchema = new SchemaV2(almaTableName);
        }

        public override bool Supports(Table table) => table == Table.Alma || table == Table.Aeon;

        public override string InsertDataSql(Table table)
        {
            EnsureSupported(table);
            if (table == Table.Alma) {
                return AlmaSchema.InsertDataSql(table);
            }
            return $@"
            begin
                insert into {AeonTableName}
                (
                    AuthorizeTransactionId,
                    TransactionSubmitTime,
                    TransactionSettledTime,
                    TransactionStatus,
                    SettlementState,
                    PatronName,
                    AeonTransactionNumbers,
                    PaymentAmount
                )
                values
                (
                    :AuthorizeTransactionId,
                    :TransactionSubmitTime,
                    :TransactionSettledTime,
                    :TransactionStatus,
                    :SettlementState,
                    :PatronName,
                    :AeonTransactionNumbers,
                    :PaymentAmount
                );
            exception when dup_val_on_index then
                update {AeonTableName} set
                    TransactionSubmitTime = :TransactionSubmitTime,
                    TransactionSettledTime = :TransactionSettledTime,
                    TransactionStatus = :TransactionStatus,
                    SettlementState = :SettlementState,
                    PatronName = :PatronName,
                    AeonTransactionNumbers = :AeonTransactionNumbers,
                    PaymentAmount = :PaymentAmount
                where
                    AuthorizeTransactionId = :AuthorizeTransactionId;
            end;";
        }

        public override string MigrationSql(Schema currentSchema) => currentSchema.Version switch
        {
            SchemaVersion.V1 => AlmaSchema.MigrationSql(currentSchema),
            SchemaVersion.V2 => TableCreationSql(Table.Aeon),
            SchemaVersion.V3 => null,
            _ => throw new NotImplementedException(),
        };

        public override string TableCreationSql(Table table)
        {
            EnsureSupported(table);
            if (table == Table.Alma) {
                return AlmaSchema.TableCreationSql(table);
            }
            return $@"
            create table {AeonTableName}
            (
                AuthorizeTransactionId varchar2(100) primary key,
                TransactionSubmitTime date,
                TransactionSettledTime date null,
                TransactionStatus varchar2(100),
                SettlementState varchar2(100) null,
                PatronName varchar2(100) null,
                AeonTransactionNumbers varchar2(1000),
                PaymentAmount number
            )";
        }
    }
}