using System;
using CommandLine;

namespace alma_authorizenet_payment_reporting
{
    public class Options
    {
        [Option('f', "from", Required = true, HelpText = "Get transactions starting from this date.")]
        public DateTime FromDate { get; set;}
        [Option('t', "to", Required = false, HelpText = "Get transactions up to this date. Defaults to today.")]
        public DateTime? ToDate { get; set; }
        [Option('l', "log", Required = false, Default = false, HelpText = "Log messages to the console when set.")]
        public bool Log { get; set; }
    }
}