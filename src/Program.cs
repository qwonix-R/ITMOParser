using AngleSharp;
using AngleSharp.Dom;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using dotenv.net;
using System.Data.Common;
using System.Runtime.ConstrainedExecution;

namespace ITMOParser
{
    internal class Program
    {
        private static int timerPeriodInMinutes = 30;
        internal static string? dbConnectionString;
        private static async Task Main(string[] args)
        { 


            if (Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") != null)
            {
                dbConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
            }
            else
            {
                var envPath = Path.Combine(Environment.CurrentDirectory, ".env");
                DotEnv.Load();
                var envVars = DotEnv.Read();
                Console.WriteLine(envVars["DB_CONNECTION_STRING"]);
                dbConnectionString = envVars["DB_CONNECTION_STRING"];
            }


            using (ApplicationContext db = new ApplicationContext())
            {
                db.Database.EnsureCreated();
            }

            using var cts = new CancellationTokenSource();


            AppDomain.CurrentDomain.ProcessExit += (s, e) => cts.Cancel();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
            

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(timerPeriodInMinutes));

            try
            {
                await CycleLogic.RunCycle();

                while (await timer.WaitForNextTickAsync(cts.Token))
                {
                    await CycleLogic.RunCycle();
                }
            }

            catch (OperationCanceledException)
            {
                Console.WriteLine($"[{DateTime.Now}] Process stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}] Timer runtime error: {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"[{DateTime.Now}] Stopped.");
            }


        }
    }
}
