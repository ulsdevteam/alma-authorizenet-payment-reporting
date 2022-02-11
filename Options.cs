using System;
using CommandLine;

namespace alma_authorizenet_payment_reporting
{
    [Verb("run", isDefault: true)]
    public class Options
    {
        [Option('f', "from", Required = false, HelpText = "Get transactions starting from this date. "
            + "If not supplied, defaults to the most recent transaction date in the database. "
            + "If the table is empty, defaults to one month before today.")]
        public DateTime? FromDate { get; set;}
        [Option('t', "to", Required = false, HelpText = "Get transactions up to this date. Defaults to today.")]
        public DateTime? ToDate { get; set; }
        [Option('l', "log", Required = false, HelpText = "Log messages to stdout when set.")]
        public bool Log { get; set; }
        [Option('d', "dryrun", Required = false, HelpText = "Will not connect to database when set.")]
        public bool DryRun { get; set; }
        [Option('s', "schema-version", Default = SchemaVersion.V2, HelpText = "Which version of the reporting table schema to use.")]
        public SchemaVersion SchemaVersion { get; set; }
    }

    [Verb("migrate")]
    public class MigrateOptions
    {
        [Value(0, Required = true)]
        public SchemaVersion CurrentSchema { get; set; }
        [Value(1, Default = SchemaVersion.V2)]
        public SchemaVersion NewSchema { get; set; }
    }

}