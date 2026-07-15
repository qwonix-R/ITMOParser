
using dotenv.net;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace ITMOParser
{
    internal class Program
    {
        // TimerPeriod in minutes
        private static int TimerPeriod;
        internal static string? DBConnectionString;
        private static async Task Main(string[] args)
        {
            // Getting settings from appsettings.json
            if (!LoadJsonSettings()) { return; }
            

            // DB connection fetch
            if (Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") != null)
            {
                DBConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
            }
            else
            {
                DotEnv.Load();
                var envVars = DotEnv.Read();
                DBConnectionString = envVars["DB_CONNECTION_STRING"];
            }

            // Migrations
            using (ApplicationContext db = new ApplicationContext())
            {
                db.Database.Migrate();
            }

            // CancellationToken init
            using var cts = new CancellationTokenSource();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => cts.Cancel();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
            
            // Time between cycles
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(TimerPeriod));

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
        private static bool LoadJsonSettings()
        {
            try
            {
                string settingsJson = File.ReadAllText("appsettings.json");
                using var settings = JsonDocument.Parse(settingsJson);

                CycleLogic.PageDict = JsonSerializer.Deserialize<Dictionary<string, string>>
                    (
                    settings.RootElement.GetProperty("PageDict").GetRawText()
                    );
                CycleLogic.UpdateLowerThreshold = settings.RootElement.GetProperty("UpdateLowerThreshold").GetInt32();
                TimerPeriod = settings.RootElement.GetProperty("TimerPeriod").GetInt32();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}] Failed to fetch settings from appsettings.json: {ex.Message}");
                Console.WriteLine($"[{DateTime.Now}] Stopping..");
                return false;
            }
            
        }
        
    }

}
