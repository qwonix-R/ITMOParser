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

namespace ITMOParser
{
    public class Program
    {
        public static string? dbConnectionString;
        private static IConfiguration config = Configuration.Default.WithDefaultLoader();
        private static IBrowsingContext ctx = BrowsingContext.New(config);
        private static Dictionary<string, string> pageDict = new Dictionary<string, string>
        {
            ["090301"] = "https://abit.itmo.ru/rating/bachelor/budget/2339",
            ["090302"] = "https://abit.itmo.ru/rating/bachelor/budget/2340",
            ["090303"] = "https://abit.itmo.ru/rating/bachelor/budget/2341",
            ["090304"] = "https://abit.itmo.ru/rating/bachelor/budget/2342",
            ["110302"] = "https://abit.itmo.ru/rating/bachelor/budget/2344"
        };
        private static async Task RunCycle()
        {
            Console.WriteLine($"[{DateTime.Now}] RunCycle started.");
            try
            {
                using (ApplicationContext db = new ApplicationContext())
                {
                    db.Database.ExecuteSqlRaw($"TRUNCATE TABLE ITMO");
                }
                foreach (var page in pageDict)
                {

                    await UpdateDb(await ParsePage(page.Key, page.Value));

                }
                Console.WriteLine($"[{DateTime.Now}] RunCycle ended successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}]Cycle runtime error: {ex.Message}" );
            }
            

        }

        private static async Task<List<Application>> ParsePage(string profile, string url)
        {
            List<Application> appsList = new List<Application>();
            IDocument document = await ctx.OpenAsync(url);
            
            Console.WriteLine($"[{DateTime.Now}] Started parsing on profile {profile}.");
            
            var headings = document.QuerySelectorAll("h5.RatingPage_title__zlsGy");
            foreach (var heading in headings)
            {
                string group = heading.TextContent.Trim();

                if (group == "Без вступительных испытаний")
                {
                    try
                    {
                        var tablediv = heading.NextElementSibling;

                        var allAbit = tablediv.QuerySelectorAll(".RatingPage_table__item__qMY0F");
                        foreach (var abit in allAbit)
                        {
                            try
                            {
                                int applicationId;
                                int points;
                                int priority;
                                bool confirmation = false;

                                
                                string? applicationIdWithNumber = abit.QuerySelector(".RatingPage_table__position__uYWvi")?.TextContent;
                                applicationId = int.Parse(applicationIdWithNumber.Split()[^1][1..]);


                                var priorityAndTypeDiv = abit.QuerySelector(".RatingPage_table__infoLeft__Y_9cA");
                                string priorityStr = priorityAndTypeDiv.QuerySelector("p").TextContent;
                                priority = int.Parse(priorityStr.Substring(priorityStr.Length - 1));


                                points = 310; // Эквивалент БВИ 

                                var soglP = abit.QuerySelectorAll("p")
                                    .FirstOrDefault(p => p.TextContent.Contains("Есть согласие", StringComparison.OrdinalIgnoreCase));
                                if (soglP.TextContent == "Есть согласие: да")
                                {
                                    confirmation = true;
                                }
                                appsList.Add(new Application
                                {
                                    applicationId = applicationId,
                                    points = points,
                                    priority = priority,
                                    confirmation = confirmation,
                                    profile = profile
                                });
                            }
                            catch (Exception ex)
                            {
                                
                            }


                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.Now}] Application parsing error: {ex.Message}");
                    }
                }
                else if (group == "Общий конкурс")
                {
                    try
                    {
                        var tablediv = heading.NextElementSibling;
                        var allAbit = tablediv.QuerySelectorAll(".RatingPage_table__item__qMY0F");
                    
                        foreach (var abit in allAbit)
                        {
                            try
                            {
                                int applicationId;
                                int points;
                                int priority;
                                bool confirmation = false;
                                string? applicationIdWithNumber = abit.QuerySelector(".RatingPage_table__position__uYWvi")?.TextContent;

                                applicationId = int.Parse(applicationIdWithNumber.Split()[^1][1..]);


                                var priorityAndTypeDiv = abit.QuerySelector(".RatingPage_table__infoLeft__Y_9cA");
                                string priorityStr = priorityAndTypeDiv.QuerySelector("p").TextContent;
                                priority = int.Parse(priorityStr.Substring(priorityStr.Length - 1));
                                var pointsP = abit.QuerySelectorAll("p")
                                    .FirstOrDefault(p => p.TextContent.Contains("Балл ВИ+ИД:", StringComparison.OrdinalIgnoreCase));
                                string pointsStr = pointsP.TextContent;
                                points = int.Parse(pointsStr.Split()[^1]);

                                var soglP = abit.QuerySelectorAll("p")
                                    .FirstOrDefault(p => p.TextContent.Contains("Есть согласие", StringComparison.OrdinalIgnoreCase));
                                if (soglP.TextContent == "Есть согласие: да")
                                {
                                    confirmation = true;
                                }
                                appsList.Add(new Application
                                {
                                    applicationId = applicationId,
                                    points = points,
                                    priority = priority,
                                    confirmation = confirmation,
                                    profile = profile
                                });

                            }
                            catch (Exception ex)
                            {
                                
                            }
                            

                        }
                    }
                    catch (Exception ex)
                    {
                    Console.WriteLine($"[{DateTime.Now}] Application parsing error: {ex.Message}");
                    }

                }
                
            }
            Console.WriteLine($"[{DateTime.Now}] List.Count for {profile}: {appsList.Count}");
            return appsList;
        }
        private static async Task UpdateDb(List<Application> appsList)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                db.itmo.AddRange(appsList);
                await db.SaveChangesAsync();
            }
        }
        static async Task Main(string[] args)
        {


            if (Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") != null)
            {
                dbConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
            }
            else
            {
                DotEnv.Load();
                var envVars = DotEnv.Read();
                dbConnectionString = envVars["DB_CONNECTION_STRING"];
            }


            using (ApplicationContext db = new ApplicationContext())
            {
                db.Database.EnsureCreated();
            }

            using var cts = new CancellationTokenSource();

            AppDomain.CurrentDomain.ProcessExit += (s, e) => cts.Cancel();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
            

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));

            try
            {
                await RunCycle();

                while (await timer.WaitForNextTickAsync(cts.Token))
                {
                    await RunCycle();
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
