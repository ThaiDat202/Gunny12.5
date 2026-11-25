using System;

namespace LeadCli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            return GunnyRequestStudio.Core.Cli.Run(args);
        }
    }
}
