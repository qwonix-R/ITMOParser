using AngleSharp;
using AngleSharp.Dom;
using Microsoft.EntityFrameworkCore;

namespace ITMOParser
{
    internal class CycleLogic
    {
        internal readonly IConfiguration config;
        internal readonly IBrowsingContext ctx;
        internal CycleLogic()
        {
            config = Configuration.Default.WithDefaultLoader();
            ctx = BrowsingContext.New(config);
        }


        public Dictionary<string, string> PageDict = new Dictionary<string, string> { };

        // if actual parsing results have less elements by this number than in bd, it doesn't update
        internal int UpdateLowerThreshold = 250;


        internal async Task RunCycle()
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
                                Console.WriteLine($"[{DateTime.Now}] (TRY {tries}) Fetched 0 records for profile {page.Key}, retrying..");
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
                                Console.WriteLine($"[{DateTime.Now}] Last parsing result had at least {UpdateLowerThreshold} less records than in bd ({prevLength}).");
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

        private async Task<List<Application>> ParsePage(string profile, string url)
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
                    bool isBVI;
                    if (group == "Без вступительных испытаний")
                    {
                        isBVI = true;
                        try
                        {
                            var tablediv = heading.NextElementSibling;

                            var allAbit = tablediv.QuerySelectorAll(".RatingPage_table__item__qMY0F");
                            foreach (var abitCard in allAbit)
                            {
                                try
                                {
                                    IHtmlCollection<IElement> RowsDivs = abitCard.QuerySelectorAll(".RatingPage_table__infoLeft__Y_9cA");
                                    List<int> firstRow;
                                    (int individualAchievements, int pointsBase, int pointsFinal, bool advantage) secondRow;
                                    int abiturientId;
                                    int pointsFinal;
                                    int pointsBase;
                                    List<int> exams;
                                    int individualAchievements;
                                    int priority;
                                    bool confirmation = false;
                                    bool advantage;

                                    string? applicationIdWithNumber = abitCard.QuerySelector(".RatingPage_table__position__uYWvi")?.TextContent;
                                    abiturientId = int.Parse(applicationIdWithNumber.Split()[^1][1..]);

                                    firstRow = await ParseFirstRow(isBVI, RowsDivs);
                                    priority = firstRow[0];
                                    exams = [100, 100, 100];


                                    secondRow = await ParseSecondRow(isBVI, RowsDivs);
                                    individualAchievements = secondRow.individualAchievements;
                                    pointsBase = 300;
                                    pointsFinal = 300 + individualAchievements;
                                    advantage = true;
                                    confirmation = await ParseConfirmation(abitCard);

                                    appsList.Add(new Application
                                    {
                                        AbiturientId = abiturientId,
                                        PointsFinal = pointsFinal,
                                        PointsBase = pointsBase,
                                        Exams = exams,
                                        IndividualAchievements = individualAchievements,
                                        Priority = priority,
                                        Advantage = advantage,
                                        Confirmation = confirmation,
                                        Profile = profile
                                    });

                                }
                                catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{DateTime.Now}] Application parsing error: {ex.Message}");
                        }
                    }
                    else if (group == "Общий конкурс")
                    {
                        isBVI = false;

                        var tablediv = heading.NextElementSibling;
                        var allAbit = tablediv.QuerySelectorAll(".RatingPage_table__item__qMY0F");


                        foreach (IElement abitCard in allAbit)
                        {
                            try
                            {
                                IHtmlCollection<IElement> RowsDivs = abitCard.QuerySelectorAll(".RatingPage_table__infoLeft__Y_9cA");
                                List<int> firstRow;
                                (int individualAchievements, int pointsBase, int pointsFinal, bool advantage) secondRow;
                                int abiturientId;
                                int pointsFinal;
                                int pointsBase;
                                List<int> exams;
                                int individualAchievements;
                                int priority;
                                bool confirmation = false;
                                bool advantage;

                                string? applicationIdWithNumber = abitCard.QuerySelector(".RatingPage_table__position__uYWvi")?.TextContent;
                                abiturientId = int.Parse(applicationIdWithNumber.Split()[^1][1..]);

                                firstRow = await ParseFirstRow(isBVI, RowsDivs);
                                priority = firstRow[0];
                                exams = firstRow.Take(2..).ToList();


                                secondRow = await ParseSecondRow(isBVI, RowsDivs);
                                individualAchievements = secondRow.individualAchievements;
                                pointsBase = secondRow.pointsBase;
                                pointsFinal = secondRow.pointsFinal;
                                advantage = secondRow.advantage;
                                confirmation = await ParseConfirmation(abitCard);

                                appsList.Add(new Application
                                {
                                    AbiturientId = abiturientId,
                                    PointsFinal = pointsFinal,
                                    PointsBase = pointsBase,
                                    Exams = exams,
                                    IndividualAchievements = individualAchievements,
                                    Priority = priority,
                                    Advantage = advantage,
                                    Confirmation = confirmation,
                                    Profile = profile
                                });

                            }
                            catch (Exception ex) { }

                        }


                    }

                }

                Console.WriteLine($"[{DateTime.Now}] Records fetched for {profile}: {appsList.Count}");
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


        private static async Task<List<int>> ParseFirstRow(bool isBVI, IHtmlCollection<IElement> RowsDivs)
        {
            IElement rowDiv = RowsDivs[0];
            List<int> rowList = new List<int>();
            IHtmlCollection<IElement> firstRow = rowDiv.QuerySelectorAll("p");
            if (!isBVI)
            {
                for (int i = 0; i < firstRow.Count; i++)
                {

                    string pQuery = firstRow[i].TextContent;
                    string[] splittedPQuery = pQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    try
                    {
                        rowList.Add(Int32.Parse(splittedPQuery[^1]));
                    }
                    catch
                    {

                    }

                }
                return rowList;
            }
            else
            {
                try
                {
                    string pQuery = firstRow[0].TextContent;
                    string[] splittedPQuery = pQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int priority = Int32.Parse(splittedPQuery[^1]);
                    rowList.Add(priority);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now}] ERROR parsing first row, BVI={isBVI}: {ex.Message}");
                    throw;
                }
                return rowList;
            }
        }
        private static async Task<(int individualAchievements, int pointsBase, int pointsFinal, bool advantage)> ParseSecondRow(bool isBVI, IHtmlCollection<IElement> RowsDivs)
        {
            IElement rowDiv = RowsDivs[1];
            IHtmlCollection<IElement> row = rowDiv.QuerySelectorAll("p");

            (int individualAchievements, int pointsBase, int pointsFinal, bool advantage) rowTuple = (0, 0, 0, false);
            if (!isBVI)
            {
                for (int i = 0; i < row.Count; i++)
                {

                    string pQuery = row[i].TextContent;
                    string[] splitedPQuery = pQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    try
                    {
                        switch (i)
                        {
                            case 0:
                                rowTuple.individualAchievements = (Int32.Parse(splitedPQuery[^1]));
                                break;
                            case 1:
                                rowTuple.pointsBase = (Int32.Parse(splitedPQuery[^1]));
                                break;
                            case 2:
                                rowTuple.pointsFinal = (Int32.Parse(splitedPQuery[^1]));
                                break;
                            case 3:
                                if (splitedPQuery[^1].ToLower() == "нет")
                                {
                                    rowTuple.advantage = false;
                                }
                                else if (splitedPQuery[^1].ToLower() == "да")
                                {
                                    rowTuple.advantage = true;
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    catch { throw; }

                }

            }
            else
            {
                try
                {
                    string pQuery = row[0].TextContent;
                    string[] splitedPQuery = pQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    rowTuple.individualAchievements = (Int32.Parse(splitedPQuery[^1]));
                }
                catch { throw; }
            }
            return rowTuple;
        }
        private static async Task<bool> ParseConfirmation(IElement abitCard)
        {
            bool confirmation = false;
            string confirmationP = abitCard.QuerySelectorAll("p")
                                        .FirstOrDefault(p => p.TextContent.Contains("Есть согласие", StringComparison.OrdinalIgnoreCase))
                                        .TextContent;
            string[] confirmationParts = confirmationP.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (confirmationParts[^1].ToLower() == "да")
            {
                confirmation = true;
            }
            return confirmation;
        }
    }
}
