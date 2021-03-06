﻿namespace VisualMutator.Console
{
    using System.Threading.Tasks;
    using Console = System.Console;

    internal class Program
    {
        public Program()
        {
            T2();
            T1();
        }

        private async Task<object> L1()
        {
            int i = 0;
            return await Task.Run(() =>
            {
                while (i >= 0)
                {
                }
                return new object();
            });
        }

        private async Task T2()
        {
            await L1();
        }

        private async Task T1()
        {
            await L1();
        }

        private static void Main(string[] args)
        {
            Console.WriteLine("Started VisualMutator.Console with params: " + args.MakeString());

            if (args.Length >= 5)
            {
                var parser = new CommandLineParser();
                parser.ParseFrom(args);
                var connection = new EnvironmentConnection(parser);
                var boot = new ConsoleBootstrapper(connection, parser);
                boot.Initialize().Wait();
            }
            else
            {
                Console.WriteLine("Too few parameters.");
            }
        }
    }
}