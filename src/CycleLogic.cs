using AngleSharp;
using AngleSharp.Dom;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Text;

namespace ITMOParser
{
    internal class CycleLogic
    {
        internal static readonly IConfiguration config = Configuration.Default.WithDefaultLoader();
        internal static readonly IBrowsingContext ctx = BrowsingContext.New(config);
        
        
        public static Dictionary<string, string> pageDict = new Dictionary<string, string>
        {
            ["090301"] = "https://abit.itmo.ru/rating/bachelor/budget/2339",
            ["090302"] = "https://abit.itmo.ru/rating/bachelor/budget/2340",
            ["090303"] = "https://abit.itmo.ru/rating/bachelor/budget/2341",
            ["090304"] = "https://abit.itmo.ru/rating/bachelor/budget/2342",
            ["110302"] = "https://abit.itmo.ru/rating/bachelor/budget/2344"
        };

        // if actual parsing results have less elements by this number than in bd, it doesn't update
        internal const int updateLowerThreshold = 250;

        
        internal static async Task RunCycle()
        {
            Console.WriteLine($"[{DateTime.Now}] RunCycle started.");
            List<Application> allProfiles = new List<Application>();
            try
            {
                int prevLength = 0;
                foreach (var page in pageDict)
                {
                    int tries = 1;
                    bool success = false;
                    while (tries < 4 && !success)
                    { 
                        try
                        {
                            allProfiles.AddRange(await ParsePage(page.Key, page.Value));
                            success = true;
                        }
                        catch (Exception ex) 
                        {
                            Console.WriteLine($"[{DateTime.Now}] (TRY {tries}) Did not fetch parsed data for profile {page.Key}: {ex.Message}");
                            tries++;
                            Thread.Sleep(2000);
                        }
                    }
                }
                using (ApplicationContext db = new ApplicationContext())
                {
                    prevLength = await db.itmo.CountAsync();

                    if (prevLength - allProfiles.Count < updateLowerThreshold && allProfiles != null)
                    {
                        db.Database.ExecuteSqlRaw($"TRUNCATE TABLE itmo");
                        await UpdateDb(allProfiles);
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now}] Last parsing result had at least {updateLowerThreshold} less than in bd ({prevLength}).");
                    }
                }

                Console.WriteLine($"[{DateTime.Now}] RunCycle ended successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}]Cycle runtime error: {ex.Message}");
            }


        }

        private static async Task<List<Application>> ParsePage(string profile, string url)
        {
            try
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
                                catch (Exception ex) { }

                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{DateTime.Now}] Application parsing error: {ex.Message}");
                        }
                    }
                    else if (group == "Общий конкурс")
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

                                   if (soglP.TextContent == "Есть согласие: да") { confirmation = true; }


                                   appsList.Add(new Application
                                   {
                                       applicationId = applicationId,
                                       points = points,
                                       priority = priority,
                                       confirmation = confirmation,
                                       profile = profile
                                   });

                               }
                               catch (Exception ex) { }

                            }
                        

                    }

                }

                Console.WriteLine($"[{DateTime.Now}] List.Count for {profile}: {appsList.Count}");
                return appsList;
            }
            catch (Exception ex)
            {
                throw;
            }

        }
        private static async Task UpdateDb(List<Application> appsList)
        {
            using (ApplicationContext db = new ApplicationContext())
            {
                
                db.itmo.AddRange(appsList);
                await db.SaveChangesAsync();
            }
        }
    }
}
