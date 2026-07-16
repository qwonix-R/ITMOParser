using AngleSharp;
using AngleSharp.Dom;
using Microsoft.EntityFrameworkCore;

namespace ITMOParser
{
    internal class CycleLogic
    {
        internal static readonly IConfiguration config = Configuration.Default.WithDefaultLoader();
        internal static readonly IBrowsingContext ctx = BrowsingContext.New(config);
        
        
        public static Dictionary<string, string> PageDict = new Dictionary<string, string> { };

        // if actual parsing results have less elements by this number than in bd, it doesn't update
        internal static int UpdateLowerThreshold = 250;

        
        internal static async Task RunCycle()
        {
            Console.WriteLine($"[{DateTime.Now}] RunCycle started.");
            List<Application> allProfiles = new List<Application>();
            try
            {
                int prevLength = 0;
                foreach (var page in PageDict)
                {
                    int tries = 1;
                    bool success = false;
                    while (tries < 4 && !success)
                    { 
                        try
                        {
                            List<Application> profileApps = await ParsePage(page.Key, page.Value);
                            if (profileApps.Count > 0) 
                            {
                                allProfiles.AddRange(profileApps);
                                success = true;
                            }
                            else
                            {
                                Console.WriteLine($"[{DateTime.Now}] (TRY {tries}) Fetched 0 applications for profile {page.Key}, retrying..");
                                tries++;
                            }
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
                    int tries = 1;
                    bool success = false;
                    prevLength = await db.itmo.CountAsync();
                    while (tries < 4 && !success)
                    {
                        try
                        {
                            if (prevLength - allProfiles.Count < UpdateLowerThreshold && allProfiles != null)
                            {
                                db.Database.ExecuteSqlRaw($"TRUNCATE TABLE itmo RESTART IDENTITY");
                                await UpdateDb(allProfiles);
                            }
                            else
                            {
                                Console.WriteLine($"[{DateTime.Now}] Last parsing result had at least {UpdateLowerThreshold} less than in bd ({prevLength}).");
                            }
                            success = true;
                        }
                        catch (Exception ex) 
                        {
                            Console.WriteLine($"[{DateTime.Now}] (TRY {tries}) Did not save parsed data in db: {ex.Message}");
                            tries++;
                            Thread.Sleep(2000);
                        }
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
                try
                {
                    db.itmo.AddRange(appsList);
                    await db.SaveChangesAsync();
                }
                catch { throw; }
                
            }
        }
    }
}
