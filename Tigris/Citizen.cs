using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using System.IO;
using System.Web.Script.Serialization;
using OpenPop.Pop3;
using OpenPop.Mime;
using System.Collections.Specialized;
using SeasideResearch.LibCurlNet;
using AE.Net.Mail;
using System.Diagnostics;

namespace Tigris
{
    // Summary:
    //     Represents an "action" that the citizen will perform.
    //
    // Usage:
    //   The "name" parameter represents, obviously, the identification
    //   name of the action. These names are hard-coded in the Citizen.ProcessActions
    //   function. 
    //   The params "parameters" are actually the parameters of the action that are, 
    //   also hard-coded in the aforementioned function.
    //   Make sure to refer to it for a detailed explanation.
    public class ActionToProcess
    {
        public ActionToProcess(string name, params object[] parameters)
        {
            Parameters = new List<object>();
            Name = name;
            foreach(object parameter in parameters)
            {
                Parameters.Add(parameter);
            }
        }
        public string Name { get; set; }
        public List<object> Parameters { get; set; }
    }

    // General settings
    public class Settings
    {
        public Settings()
        {
            Proxy = "";
            AutoCaptcha = false;
        }
        public string Proxy { get; set; }
        public bool AutoCaptcha { get; set; }
    }

    public class Citizen : IDisposable
    {        
        // Holds this citizen's personal information
        public Profile Profile = new Profile();

        // Make sure to initialize the Browser in the right thread (that is, the
        // citizen's thread) and not in the UI thread !
#if LIBCURL
        public CURLBrowser Browser;
#else
        public NETBrowser Browser;
#endif

        public Settings Settings = new Settings();
        
        // The actions this citizen will process
        public List<ActionToProcess> actions = new List<ActionToProcess>();

        // Required to parse JSON
        public JavaScriptSerializer serializer = new JavaScriptSerializer();

        // Used for the RandomString function
        private readonly Random _rng = new Random();
        private const string _chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        // Events so we can communicate with the main UI thread
        public delegate void LogHandler(Citizen c, LogEvent e);
        public event LogHandler LogHandle;
        
        // Used for terminating the thread
        private volatile bool _shouldTerminate;
        public void RequestTermination() { _shouldTerminate = true; }

        // Just so we know is this citizen yet to be created
        public bool registrationCitizen = false;

        private Authenticator Authenticator;

        public Citizen(string email, string password, string proxy, string payload, bool autocaptcha = false)
        {
            Profile.Email = email;
            Profile.Password = password;
            Profile.Proxy = proxy;
            Settings.Proxy = proxy;
            Settings.AutoCaptcha = autocaptcha;
            serializer.RegisterConverters(new[] { new DynamicJsonConverter() });
            Authenticator = new Authenticator(payload);
        }

        public void Dispose()
        {
            // Dispose of shit here
        }

        // Logs in the citizen, while checking for possible errors 
        // Will return false if login was not successful
        public bool Login()
        {      
            // add login proxy replacing if this one not working
            Browser.Get("http://my-ip-address.com/");
            Profile.IP = Browser.Select("#ip").Attributes["value"].Value;

            Action("login");
            Log("Logging in ...");
            Browser.Get("http://www.erepublik.com/en");
            try
            {
                // SECURITY #1
#if ANTILEAK
                Browser.Post("http://www.erepublik.com/en/login", false,
                         Authenticator.data["1"], Profile.Email,
                         Authenticator.data["2"], Profile.Password,
                         "_token", Browser.GetValue(Authenticator.data["3"]));
#else
                Browser.Post("http://www.erepublik.com/en/login", false,
                         "citizen_email", Profile.Email,
                         "citizen_password", Profile.Password,
                         "_token", Browser.GetValue("#_token"));
#endif
            }
            catch (System.Net.WebException)
            {
            	// A redirect exception gets thrown if the account is banned
                // TODO (LOWP): Remove this hack and solve the redirection problem
                Log("This account is banned.");
                return false;
            }

            if (Browser.currentUrl == "http://www.erepublik.com/en/law-infringements")
            {
                Log("This account is banned.");
                return false;
            }

            // Let's check was this a successful login
            if (Browser.page.Contains("Wrong citizen email"))
            {
                Action("wrong_email");
                Log("Wrong citizen email");
                return false;
            }
            if (Browser.page.Contains("Wrong password") && !Browser.page.Contains("Complete the captcha challenge:"))
            {
                Action("wrong_password");
                Log("Wrong citizen password");
                return false;
            }
            if (Browser.page.Contains("Complete the captcha challenge:"))
            {
                Action("captcha_solving");

                // Attempt to solve the captcha 3 times 
                int tries = 0;
                while (tries < 3 && Browser.page.Contains("Complete the captcha challenge:"))
                {
                    Log("Captcha required, trying to solve it (try " + (tries + 1).ToString() + "/3" + ") ...");
                    string challenge = "";
                    string token = Browser.GetValue("#_token");
                    string captcha_response = Browser.SolveCaptcha(ref challenge, Settings.AutoCaptcha);
                    Log("Captcha text is: " + captcha_response);

                    Browser.Post("http://www.erepublik.com/en/login", false,
                         "citizen_email", Profile.Email,
                         "citizen_password", Profile.Password,
                         "_token", token,
                         "recaptcha_challenge_field", challenge,
                         "recaptcha_response_field", captcha_response,
                         "commit", "Login");

                    tries++;
                }
                if (Browser.page.Contains("Complete the captcha challenge:"))
                {
                    Log("Couldn't solve captcha.");
                    return false;
                } 
                Log("Successfully solved captcha!");
            }
                        
            PostLogin();
            Action("logged in");
            Log("Logged in.");

            return true;
        }

        // Gets the details about this citizen, shoud only be called after login
        public void PostLogin()
        {
            // Get the basic data from the homepage
            Action("basic info");
            GetVitals();

            // Fool the eRepublik flash-based security system
            SendFlashToken();

            // For detailed data we have to visit the profile page
            Action("detailed info");
            Browser.Get("http://www.erepublik.com/en/citizen/profile/" + Profile.ID);
            Profile.Location = Browser.GetAttribute(".citizen_info > a", "title") + ", " +
                               Browser.GetAttribute(".citizen_info > a:nth-child(3)", "title");
            Profile.Country = Browser.GetAttribute(".citizen_info > a", "title");
            Profile.CountryId = ByName(Common.countries, Profile.Country);
            Profile.Citizenship = Browser.GetAttribute(".citizen_info > a:nth-child(5) > img", "title");
            Profile.CitizenshipId = ByName(Common.countries, Profile.Citizenship);
            Profile.Level = Browser.GetHtml(".citizen_level");
            Profile.Experience = Browser.GetHtml(".citizen_level") + " (" + Browser.GetHtml(".citizen_experience p") + ")";
            Profile.Strength = Browser.GetHtml(".citizen_military h4").Trim() + " (" +
                               Browser.GetAttribute(".citizen_military h4 > img", "title").Trim() + ")";
            Profile.MilitaryUnitID = Browser.GetAttribute(".citizen_activity > .place:nth-child(2) a", "href").Replace("/en/main/group-show/", "");

            // Now let's get the storage (inventory)
            CheckStorage();
        }

        // Parses the citizen's inventory
        public void CheckStorage()
        {
            Action("storage");
            Profile.Inventory.Clear();
            Profile.InventoryString = "";
            Browser.Get("http://www.erepublik.com/en/economy/inventory");
            string space = Browser.GetHtml(".area.storage strong").Trim().Replace("(", "").Replace(")", "").Replace(",", "");
            if (space != "none")
                Profile.AvailableStorage = int.Parse(space.Split('/')[1].Trim()) - int.Parse(space.Split('/')[0].Trim()) - 1;
            else
                Profile.AvailableStorage = 1000;

            // SECURITY #4
#if ANTILEAK
            var itemNodes = Browser.SelectAll(Authenticator.data["12"]);
#else
            var itemNodes = Browser.SelectAll(".product_list li");
#endif
            var stockNodes = Browser.SelectAll(".product_list li strong");
            int i = 0;
            foreach (var itemNode in itemNodes)
            {
                string industry = Common.industries[int.Parse(itemNode.Attributes["industry"].Value)];
                int quality = int.Parse(itemNode.Attributes["quality"].Value);
                int quantity = int.Parse(stockNodes.ElementAt(i).InnerHtml.Replace(",", ""));

                // First let's add it to our inventory
                Tuple<string, int> item = new Tuple<string, int>(industry, quality);
                AddItem(industry, quality, quantity);
                Profile.InventoryString += stockNodes.ElementAt(i).InnerHtml + " Q" + item.Item2.ToString() + " "
                                   + item.Item1 + ", ";

                // If it's food, then let's add it to recoverable health
                if (industry == "Food")
                {
                    if (quality < 10 && quality != 7)
                        Profile.WellnessRecoverableWithFood += quality * 2 * quantity;
                    else if (quality == 10)
                        Profile.WellnessRecoverableWithBars += quality * 10 * quantity;
                    else if (quality == 7)
                        Profile.WellnessRecoverableWithBars += 20 * quantity;
                }
                i++;
            }

            // Assemble bazookas if possible
            if (!Browser.GetAttribute(".assemble", "class").Contains("disabled"))
                Browser.Post("http://www.erepublik.com/en/main/assemble/", true, "_token", Browser.GetValue("#_token"), "itemLevel", "1");

            Profile.InventoryString += Browser.GetHtml(".bazooka > div > strong") + " bazookas";
        }
        
        public void GetReward()
        {
            Action("get_reward");
            Browser.Get("http://www.erepublik.com/en");
            if (Browser.Select(".green_beauty.on.reward") != null)
            {
                Log("Trying to get reward ...");
                // SECURITY #5
#if ANTILEAK
                Browser.Get(Authenticator.data["13"], "", true);
#else
                Browser.Get("http://www.erepublik.com/en/main/daily-tasks-reward", "", true);
#endif
                dynamic data = serializer.Deserialize(Browser.page, typeof(object));
                if (data.message.ToString() == "success")
                    Log("Successfully got the daily reward!");
                else
                    Log("Couldn't get the daily reward.");
            }
        }

        // Mission or daily order reward as some call it
        public bool GetMissionReward()
        {
            Action("get_dailyorder");
            Browser.Get("http://www.erepublik.com/en");
            if (Browser.Select(".green_beauty.on.missionReward") != null)
            {
                string missionId = Browser.Select(".green_beauty.on.missionReward").Attributes["missionId"].Value;
                Log("Trying to get daily order reward ...");
                // SECURITY #6
#if ANTILEAK
                Browser.Post(Authenticator.data["14"], true, "groupId", Profile.MilitaryUnitID, "action", "check", "missionId", missionId, "_token", Browser.GetValue("#_token"));
#else
                Browser.Post("http://www.erepublik.com/en/military/group-missions", true, "groupId", Profile.MilitaryUnitID, "action", "check", "missionId", missionId, "_token", Browser.GetValue("#_token"));
#endif
                dynamic data = serializer.Deserialize(Browser.page, typeof(object));
                if (data.error.ToString() == "False")
                {
                    Log("Successfully got DO reward!");
                    return true;
                }
                else
                {
                    Log("Couldn't get DO reward.");
                    return false;
                }
            }

            return false;
        }

        // Leave "check" true if you want to try to get rewards (DO & daily) before logging out
        public void Logout(bool check = true)
        {
            // First let's check can we get a reward (both company/train and DO)
            if (check)
            {
                GetReward();
                GetMissionReward();
                Browser.Get("http://www.erepublik.com/en");
                PostLogin(); // Check the data again so we have up-to-date statistics
            }

            Action("logout");
            Browser.Get("http://www.erepublik.com/en/logout");
            Log("Logged out.");

        }

        public void Train(bool train1, bool train2, bool train3, bool train4)
        {
            if (!(train1 || train2 || train3 || train4)) { Log("Couldn't train, no training ground was selected."); return; }

            if (Profile.Wellness < 10)
                Eat(true);
            
            Action("train");
            Log("Training ...");
            Browser.Get("http://www.erepublik.com/en/economy/training-grounds");

            string postData = "_token=" + Browser.GetValue("#_token") + "&";
            string train_url = "http://www.erepublik.com/en/economy/train";

            // Do we need to solve the captcha first?
            bool captcha_required = false;
            string page = Browser.page;

            
            page = page.Substring(page.IndexOf("var has_captcha"));
            page = page.Substring(0, page.IndexOf("var captcha_url"));
            if (page.Contains("1"))
                captcha_required = true;
            
            List<string> groundIds = new List<string>();
            // SECURITY #7
#if ANTILEAK
            var grounds = Browser.SelectAll(Authenticator.data["15"]);
#else
            var grounds = Browser.SelectAll(".listing.grounds");
#endif
            foreach (var ground in grounds)
            {
                string idAttribute = ground.Attributes["id"].Value;
                groundIds.Add(idAttribute.Substring(idAttribute.IndexOf('_') + 1));
            }

            int counter = 0;          
            
            if (train1 && groundIds.Count >= 1) { postData += "grounds["  + counter.ToString() + "][id]=" + groundIds[0] + "&grounds[" + counter.ToString() + "][train]=1"; counter++; }
            if (train2 && groundIds.Count >= 2) { postData += "&grounds[" + counter.ToString() + "][id]=" + groundIds[1] + "&grounds[" + counter.ToString() + "][train]=1"; counter++; }
            if (train3 && groundIds.Count >= 3) { postData += "&grounds[" + counter.ToString() + "][id]=" + groundIds[2] + "&grounds[" + counter.ToString() + "][train]=1"; counter++; }
            if (train4 && groundIds.Count >= 4) { postData += "&grounds[" + counter.ToString() + "][id]=" + groundIds[3] + "&grounds[" + counter.ToString() + "][train]=1"; counter++; }

            if (captcha_required)
            {
                train_url = "http://www.erepublik.com/en/economy/captchaAjax";
                postData += "&action_type=train&";
                bool captcha_wrong = true;
                int tries = 0;
                while (captcha_wrong && tries < 3)
                {
                    Log("Trying to solve captcha at train (#" + tries.ToString() + ") ...");
                    string challenge = "";
                    string captcha_response = Browser.SolveCaptcha(ref challenge, Settings.AutoCaptcha);
                    Log("Captcha text is: \"" + captcha_response + "\", submitting ...");

                    Browser.Post(train_url, true, postData + "recaptcha_response_field=" + captcha_response + "&recaptcha_challenge_field=" + challenge);
                    dynamic data = serializer.Deserialize(Browser.page, typeof(object));

                    if (Browser.page == "{\"status\":false,\"message\":false}")
                    {
                        // Captcha was wrong, try again up to three times
                        tries++;
                        continue;
                    }

                    CheckTrainResponse(data, train1, train2, train3, train4);
                    break;
                }
            }
            else
            {
                Browser.Post(train_url, true, postData);
                dynamic data = serializer.Deserialize(Browser.page, typeof(object));
                CheckTrainResponse(data, train1, train2, train3, train4);
            }
        }

        // Works as an employee - if we don't have a job it will get one,
        // either by automatically finding it or using the given job offer link        
        public void WorkAsEmployee(bool getAJob, string jobOfferLink = "")
        {
            Action("workAsEmployee");
            Log("Now working as employee ...");

            // SECURITY #8
#if ANTILEAK
            Browser.Get(Authenticator.data["16"]);
#else
            Browser.Get("http://www.erepublik.com/en/economy/myCompanies");
#endif

            if (Profile.Wellness < 10)
                Eat(true);

            if (Browser.Select(".employee_worked") != null)
            {
                Log("Already worked today.");
                return;
            }

            // Let's see do we have a job
            if (Browser.Select(".green_enlarged#work") != null)
            {
                // We can work
                Log("Working for " + Browser.GetHtml(".employer_info > div > a") + " ...");

                string token = Browser.GetValue("#_token");

                // Do we need to solve the captcha first?
                bool captcha_required = false;
                string page = Browser.page;
                page = page.Substring(page.IndexOf("var has_captcha"));
                page = page.Substring(0, page.IndexOf("var captcha_url"));
                if (page.Contains("1"))
                    captcha_required = true;

                string work_url = "http://www.erepublik.com/en/economy/work";
                if (!captcha_required)
                {
                    Browser.Post(work_url, true, "action_type", "work", "_token", Browser.GetValue("#_token"));
                    dynamic data = serializer.Deserialize(Browser.page, typeof(object));
                    if (data.status.ToString() == "True")
                        CheckWorkResponse(data);
                    else
                    {
                        Log("Could not work (reason : " + data.message.ToString() + ").");
                    }
                }
                else
                {
                    bool captcha_wrong = true;
                    int tries = 0;
                    while (captcha_wrong && tries < 3)
                    {
                        Log("Trying to solve captcha at work (#" + tries.ToString() + ") ...");
                        string captcha_url = "http://www.erepublik.com/en/economy/captchaAjax";
                        string challenge = "";                        
                        string captcha_response = Browser.SolveCaptcha(ref challenge, Settings.AutoCaptcha);
                        Log("Captcha text is: \"" + captcha_response + "\", submitting ...");
                        Browser.Post(captcha_url, true, "recaptcha_response_field", captcha_response, "recaptcha_challenge_field", challenge, "_token", token, "action_type", "work");
                        dynamic data = serializer.Deserialize(Browser.page, typeof(object));

                        if (Browser.page == "{\"status\":false,\"message\":false}")
                        {
                            // Captcha was wrong, try again up to three times
                            tries++;
                            continue;
                        }
                        CheckWorkResponse(data);
                        break;
                    }
                }                
            }
            else
            {
                Log("We don't have a job.");
                if (!getAJob)
                {
                    Log("The \"Get a job if unemployed\" option was unchecked. Won't search for a job.");
                    return;
                }

                Log("Searching for a job ...");

                // TODO (HIGHP): If a job offer link is provided, get the job at the given link and 
                // don't search for one automatically

                // We must get a job - let's search for one
                string url = "http://www.erepublik.com/en/economy/job-market/" + Profile.CountryId;
                Browser.Get(url);
                string token = Browser.GetValue("#_token");

                // Get the first offer
#if ANTILEAK
                IEnumerable<HtmlNode> ApplyButtons = Browser.SelectAll(Authenticator.data["17"]);
#else
                IEnumerable<HtmlNode> ApplyButtons = Browser.SelectAll(".f_light_blue_big.job_apply");
#endif
                int tries = 0;
                while(tries <= 3 && ApplyButtons.Count() == 0)
                {
                    // Sometimes the market offers are not loaded for a new account, let's try a few times
                    Browser.Get(url);
                    token = Browser.GetValue("#_token");
                    tries++;
                    ApplyButtons = Browser.SelectAll(".f_light_blue_big.job_apply");
                }

                for (int i = 0; i < ApplyButtons.Count(); ++i)
                {
                    // citizenId attribute, salary attribute
                    Log("Trying to apply job offer #" + i.ToString() + " ...");
                    Browser.Post(url, false, "citizenId", ApplyButtons.ElementAt(i).Attributes["citizenId"].Value, "salary", ApplyButtons.ElementAt(i).Attributes["salary"].Value, "_token", token);

                    if (Browser.Select(".green_enlarged#work") != null)
                    {
                        Log("Successfully got a job!");
                        WorkAsEmployee(getAJob, jobOfferLink);
                        break;
                    }
                    if (Browser.Select(".employee_worked") != null)
                    {
                        Log("Successfully got a job! But we have already worked today.");;
                        break;
                    }
                    if(Browser.Select(".green_enlarged#work") == null && Browser.Select(".employee_worked") == null)
                    {
                        Log("Couldn't get a job at this offer.");
                    }
                }
            }
        }

        // Reports the train details to the UI
        private void CheckTrainResponse(dynamic data, bool train1, bool train2, bool train3, bool train4)
        {
            if (data.status.ToString() == "True")
            {
                if (data.message.ToString() == "True")
                {
                    Log("Trained successfully. Got " + data.result.strength_bonus.ToString() + " strength.");
                    int wellnessDeducted = (train1 ? 10 : 0) + (train2 ? 10 : 0) + (train3 ? 10 : 0) + (train4 ? 10 : 0);
                    Profile.Wellness -= wellnessDeducted;
                    Profile.Strength = data.result.strength.ToString(); // TODO (LOWP): Include rank should go here too
                }                
            }
            else
                Log("Could not train (reason : " + data.message.ToString() + ").");
        }

        // Reports the work details to the UI, make sure to pass it the right data or
        // else it will crash
        private void CheckWorkResponse(dynamic data)
        {
            if (data.message.ToString() == "True")
                Log("Successfully worked. Got " + data.result.netSalary.ToString() + " " + data.result.currency.ToString() + " as salary. Worked " +
                    data.result.days_in_a_row.ToString() + " days in a row. " + data.result.to_achievment.ToString() + " more days until Hard Worker medal.");
            else
            {
                Log("Could not work (reason : " + data.message.ToString() + ").");
            }
        }

        // Works as a manager in the first number of companies specified
        public void WorkAsManager(int numOfCompanies)
        {
            Action("work_as_manager");
            Log("Working in the first " + numOfCompanies.ToString() + " of our companies ...");
            Browser.Get("http://www.erepublik.com/en/economy/myCompanies");
            List<string> companyIds = new List<string>();
            var companies = Browser.SelectAll(".listing.companies");
            foreach (var company in companies)
            {
                string idAttribute = company.Attributes["id"].Value;
                companyIds.Add(idAttribute.Substring(idAttribute.IndexOf('_') + 1));
            }
            
            // SECURITY #9
#if ANTILEAK
            string url = Authenticator.data["18"];
#else
            string url = "http://www.erepublik.com/en/economy/work";
#endif
            string token = Browser.GetValue("#_token");
            PostDataConstructor pdc = new PostDataConstructor();
            pdc.Add("action_type", "production");
            pdc.Add("_token", token);

            if (numOfCompanies > companyIds.Count)
                Log("We only have " + companyIds.Count.ToString() + " companies. Working in all of them.");

            numOfCompanies = numOfCompanies > companyIds.Count ? companyIds.Count : numOfCompanies;
            for (int i = 0; i < numOfCompanies; ++i)
            {
                pdc.Add("companies[" + i.ToString() + "][id]", companyIds[i]);
                pdc.Add("companies[" + i.ToString() + "][employee_works]", "0");
                pdc.Add("companies[" + i.ToString() + "][own_work]", "1");
            }
            string postData = pdc.GetPostData();

            Browser.Post(url, true, postData);
            dynamic data = serializer.Deserialize(Browser.page, typeof(object));

            // {"status":false,"message":"captcha","result":[]} <- on captcha
            if (data.status.ToString() == "False")
            {
                Log("Could not work as a manager (reason: " + data.message.ToString() + ").");
                if (Browser.page.Contains("message"))
                {
                    if (data.message.ToString() == "captcha")
                    {
                        // Let's solve captcha
                        string challenge = "";
                        string captcha = Browser.SolveCaptcha(ref challenge, Settings.AutoCaptcha);
                        // TODO(HIGHP): Implement WAM when captcha, finish this..
                    }
                }
            }
            else
            {
                if (data.message.ToString() == "True")
                {
                    if (Browser.page.Contains("\"result\":{"))
                    {
                        Log("Worked successfully.");
                        Profile.Wellness -= 10 * numOfCompanies;
                        System.Threading.Thread.Sleep(2000);
                    }
                }
            }
        }
        
        // Returns "done" if everything went well
        // "battle_over" if we can't fight in it anymore
        // "" empty string if this account got filtered

        // Summary:
        //     Citizen fights in a battle/war provided
        //
        // Parameters:
        //     Please read below
        //
        // Returns:
        //     If a citizen is filtered (does not meet the filterCountry), function will return
        //     an empty string ("")
        //
        //     If the battle is over (won/lost), returns "battle_over"
        //
        //     If everything went well, returns "done"
        //
        //     If it encounters an unhandled messeage, it will return "unhandled"
        public string Fight(string battleId, 
                          int kills, 
                          bool fightBareHanded = false, // This is actually impossible if we have any weapons in our invnetory 
                          bool useRocket = false,       // TODO (HIGHP): Implement this 
                          bool useBazookas = false,     // TODO (HIGHP): Implement this 
                          bool useEnergyBars = true,    // Use energy bars if necessary
                          bool isRw = false,            
                          bool forDefense = false, 
                          string filterCountry = "Any country",    // If left empty then every citizen will attempt to fight in this battle, be careful
                          string breakIfNewRank = "",   // Citizen will stop fighting when it reaches the specified rank
                          int breakIfWellness = 9999,   // Citizen will stop fighting when it's wellness reaches the specified wellness
                          int breakIfNewLevel = -1,     // Citizen will stop fighting when it reaches the specified level
                          string fightFor = "",         // Citizen will only fight for the specified country in this battle; if it's not on the
                                                        // right side it will automatically move to the right country
                          bool eatAtEnd = false // Eat after each kill and hit, useful for solving a couple of missions
                        )
        {
            if (Profile.Country != filterCountry && filterCountry != "Any country") return "";
            if (Profile.Wellness == breakIfWellness) return "";
            if (battleId == "") battleId = FindFirstBattle();
            if (battleId == "")
            {
                Log("No battles to fight in.");
                return "err_no_battle";
            }
            Action("fight");   
         
            // SECURITY #10
#if ANTILEAK
            if (!isRw) Browser.Get(Authenticator.data["19"] + battleId);
#else
            if (!isRw) Browser.Get("http://www.erepublik.com/en/military/battlefield/" + battleId);
#endif
            if (isRw)
            {
#if ANTILEAK
                Browser.Get(Authenticator.data["20"] + battleId);
#else
                Browser.Get("http://www.erepublik.com/en/wars/show/" + battleId);
#endif
                battleId = Browser.Select(".join").Attributes["href"].Value.Substring(61);
                battleId = battleId.Substring(0, battleId.IndexOf('/'));
                if (forDefense)
                    Browser.Get(Browser.Select(".join").Attributes["href"].Value);
                else
                    Browser.Get(Browser.Select(".reversed").Attributes["href"].Value);
            }

            // Let's get the fightSessionKey
            string fightSessionKey = "";
            Match match = Regex.Match(Browser.page, @"fightSessionKey\s*?:\s*?'");
            if(match.Success)
            {
                string page2 = Browser.page.Substring(match.Index + match.Value.Length);
                fightSessionKey = page2.Substring(0, page2.IndexOf("'"));
            }
            // TO-DO (LOW PRIORITY): Crashes when the citizen doesn't have a weapon, fix it
            string imgSrc = Browser.Select(".opacity_fix.fighter_weapon_image").Attributes["src"].Value; 
            string weaponQ = imgSrc.Substring(imgSrc.LastIndexOf('/') + 9).Replace(".png", "").Replace("special", "");
            string fightingFor = Browser.Select(".country.left_side > div > h3").InnerText;
            if (fightingFor.Contains("Resistance Force of")) isRw = true;
            fightingFor = fightingFor.Replace("Resistance Force of ", "");
            if (fightingFor != fightFor && fightFor != "")
            {
                // We should now move to a country we're supposed to
                Move(Profile.Country, fightFor, "");
                fightingFor = fightFor;
            }

            Log("Now fighting " + kills + " times in battle ID " + battleId +
                (isRw ? (forDefense ? " for defense." : " for resistance.") : " for " + fightingFor + "."));
            string token = Browser.csrfToken;


            int enemiesKilled = 0;
            for (int i = 1; i <= 9999; ++i)
            {
                // --- the following piece of code should be "sprinkled" all over the
                // functions that have a lot of loops, so we can gracefully
                // terminate this citizen's thread, instead of aborting it ---
                if (_shouldTerminate)
                    return "";
                // ------
                System.Threading.Thread.Sleep(1500);


                if (enemiesKilled == kills) break;

                // First let's check can we fight
                if (Profile.Wellness >= 9)
                {
                    // If a new level is reached, full health
                    bool doEat = true;
#if ANTILEAK
                    Browser.Post(Authenticator.data["21"] + battleId, true, "battleId", battleId, "_token", token);
#else
                    string sideId = ByName(Common.countries, fightingFor).ToString();
                    Browser.Post("http://www.erepublik.com/en/military/fight-shooot/" + battleId, true, "battleId", battleId, "_token", token, "sideId", sideId);                    
#endif
                    dynamic data = "";
                    try
                    {
                        data = serializer.Deserialize(Browser.page, typeof(object));
                    }
                    catch (Exception)
                    {
                        Log("Error! Could not parse:");
                        Log(Browser.page);
                    }

                    // Success
                    if ((bool)data.error == false)
                    {
                        if (data.message.ToString() == "ENEMY_KILLED")
                        {
                            enemiesKilled++;
                            string w = data.details.wellness.ToString();
                            Profile.Wellness = data.details.wellness;
                            Log("Killed enemy (#" + enemiesKilled.ToString() + "); Enemy killed - Division: " + data.user.division.ToString() +
                                "; Damage inflicted: " + data.user.givenDamage.ToString() +
                                "; Rank points earned: " + data.user.earnedRankPoints.ToString() + "; XP: " + data.details.points.ToString() + 
                                " / " + data.details.max.ToString() + " (lvl " + data.details.level.ToString() + ")");
                            if(Profile.Level.ToString() != data.details.level.ToString())
                            {
                                Profile.Level = data.details.level.ToString();
                                // Reached the new level, don't eat this time since our energy is full
                                doEat = false;
                            }
                            if (data.details.level.ToString() == breakIfNewLevel.ToString())
                            {
                                Log("Reached the wanted level! Now stopping fight.");
                                return "";
                            }
                            Profile.Experience = data.details.level.ToString() + " (" + data.details.points.ToString() + " / " + data.details.max.ToString() + ")";
                            Profile.FightInfluence += (int)data.user.givenDamage;
                            Profile.WellnessToRecover = int.Parse(data.user.food_remaining.ToString());
                            if (Profile.Wellness == breakIfWellness)
                                break;
                            if (Profile.Wellness < 9 && doEat)
                                Eat(useEnergyBars, true, fightSessionKey);

                            bool achievedNextRank = (bool)data.rank.reachedNextLevel;
                            if (achievedNextRank)
                            {
                                Log("Achieved new rank: " + data.rank.t_name.ToString() + " !");
                                if (data.rank.t_name.ToString() == breakIfNewRank && breakIfNewRank != "")
                                    break;
                            }
                        }
                        else if (data.message.ToString() == "ENEMY_ATTACKED")
                        {
                            Profile.Wellness = data.details.wellness;
                            Log("Killing enemy (#" + enemiesKilled.ToString() + ") (Hit #" + i.ToString() + "); Enemy attacked - Division: " + data.user.division.ToString() +
                                "; Enemy health: " + data.enemy.health.ToString());
                            Profile.WellnessToRecover = int.Parse(data.user.food_remaining.ToString());
                            if (Profile.Wellness == breakIfWellness)
                                break;
                            if (Profile.Wellness < 9 && doEat)
                                Eat(useEnergyBars, true, fightSessionKey);
                        }
                        else
                        {
                            Log("Unhandled message (please consult #_FIGHT0 : \"" + data.message.ToString() + "\").");
                            return "unhandled";
                        }
                    }
                    else
                    {
                        if (data.message.ToString() == "ZONE_INACTIVE")
                        {
                            Log("This battle is currently inactive. Waiting 1 minute before trying again.");
                            System.Threading.Thread.Sleep(60000);
                        }
                        else if (data.message.ToString() == "BATTLE_WON")
                        {
                            Log("This battle is over. We cannot fight in it.");
                            return "battle_over";
                        }
                        else
                        {
                            Log("Could not fight for some reason. Message: " + data.message.ToString());
                        }
                    }

                }
                else
                {
                    Eat(useEnergyBars, true, fightSessionKey);
                    if (Profile.Wellness < 9)
                    {
                        Log("Can't fight anymore. Perhaps we don't have food or our FF limit is exceeded.");
                        break;
                    }
                }
            }

            if (eatAtEnd)
                Eat(useEnergyBars, true, fightSessionKey);
            Log("Total influence made is " + Profile.FightInfluence + ".");
            return "done";
        }

        public void Eat(bool eatEnergyBar = false, bool isBattle = false, string fightSessionKey = "")
        {
            bool usedEnergyBar = false;
            Log("Trying to eat ... our current health is " + Profile.Wellness + " ...");

            if (Profile.WellnessToRecover == 0)
            {
                Log("We have to use an energy bar in order to eat.");
                if (!eatEnergyBar)
                {
                    Log("We are not allowed to eat energy bars, therefore we cannot eat.");
                    return;
                }
                usedEnergyBar = true;
                
            }
                        
            // SECURITY #11
#if ANTILEAK
            string url = Authenticator.data["22"] +
                        (usedEnergyBar ? "orange" : "blue") +
                        ((isBattle && fightSessionKey != "") ? "&fightSessionKey=" + fightSessionKey : "") +
                        "&_token=" + Browser.csrfToken;
#else
            string url = "http://www.erepublik.com/en/main/eat?format=json&buttonColor=" +
                        (usedEnergyBar ? "orange" : "blue") +
                        ((isBattle && fightSessionKey != "") ? "&fightSessionKey=" + fightSessionKey : "") +
                        "&_token=" + Browser.csrfToken;
#endif
            int tries = 0;
            while (tries <= 3)
            {
                try
                {
                    Browser.Get(url, "", true);
                    dynamic dataTry = serializer.Deserialize(Browser.page, typeof(object));
                    break;
                }
                catch(Exception e)
                {
                    tries++;
                    System.Threading.Thread.Sleep(1500);
                }
            }


            // I had to create this hack because C# doesn't support method/variable names that start with a number.
            // To whoever in the eRepublik programming team who named the object properties like this - fuck you
            Browser.page = Browser.page.Replace("\"1\":", "\"one\":").Replace("\"2\":", "\"two\":").Replace("\"3\":", "\"three\":").Replace("\"4\":", "\"four\":")
                    .Replace("\"5\":", "\"five\":").Replace("\"6\":", "\"six\":").Replace("\"7\":", "\"seven\":");

            dynamic data = serializer.Deserialize(Browser.page, typeof(object));
            if (Profile.Wellness != (int)data.health)
            {
                Profile.Wellness = (int)data.health;
                Profile.WellnessToRecover = (int)data.food_remaining;
                Log("Successfully eaten! Our health is now " + Profile.Wellness + ".");

                // Update inventory
                try
                {
                    if (data.units_consumed.one > 0)    DeleteItem("Food", 1, data.units_consumed.one); 
                    if (data.units_consumed.two > 0)    DeleteItem("Food", 2, data.units_consumed.two);
                    if (data.units_consumed.three > 0)  DeleteItem("Food", 3, data.units_consumed.three);
                    if (data.units_consumed.four > 0)   DeleteItem("Food", 4, data.units_consumed.four);
                    if (data.units_consumed.five > 0)   DeleteItem("Food", 5, data.units_consumed.five);
                    if (data.units_consumed.six > 0)    DeleteItem("Food", 6, data.units_consumed.six);
                    if (data.units_consumed.seven > 0)  DeleteItem("Food", 7, data.units_consumed.seven);

                    if (usedEnergyBar)
                    {
                        DeleteItem("Food", 10, 1);
                        Profile.WellnessRecoverableWithBars -= 100;
                    }
                }
                catch (Exception)
                {
                    Log("Could not update inventory properly. We might have some problems eating.");
                }
            }
            else
            {
                Log("Seems as if we couldn't eat (or we did, but no effect).");
            }
        }

        // The currentCountry parameter should be true if you want to buy the wished product
        // in the country the citizens is currently located in. If you want to buy a product
        // in a citizenship country, put the parameter to false. If you want to buy the max
        // quantity of products you can afford, param quantity should be set to 1000000.
        // Other parameters are self-explanatory.
        private void Buy(string product, string quality, string quantity, bool currentCountry, int quantityDeduct = 0, int skipOffers = -1)
        {
            Action("buy");
            Log("Buying " + quantity + " Q" + quality + " " + product + " ...");
            string countryId = currentCountry ? ByName(Common.countries, Profile.Country).ToString() : ByName(Common.countries, Profile.Citizenship).ToString();
            string industryId = ByName(Common.industries, product).ToString();
            
            // SECURITY #12
#if ANTILEAK
            string url = Authenticator.data["23"] + countryId + "/" + industryId + "/" + quality + "/citizen/0/price_asc/1";
#else
            string url = "http://www.erepublik.com/en/economy/market/" + countryId + "/" + industryId + "/" + quality + "/citizen/0/price_asc/1";
#endif

            Browser.Get(url);

            if(!Browser.page.Contains("Storage is over capacity"))
            {
                // Let's find the offer which has enough stock and a good price
                int offerNr = -1;
                int i = 0;
                var priceNodes = Browser.SelectAll(".m_price.stprice").ToList();
                var stockNodes = Browser.SelectAll(".m_stock").ToList().Skip(1);

                // Let's find the cheapest price and replace the dots with commas since c# parses doubles that way
                double cheapestPrice = double.Parse(Regex.Replace(priceNodes[0].InnerHtml.Trim(), "[^0-9\\.]", "").Replace(".", ","));

                // If we're buying as much as we can afford, get the quantity we can buy
                if (quantity == "1000000") 
                    quantity = ((int)(Profile.Currency / (cheapestPrice + 0.1)) - quantityDeduct).ToString();

                double price = 0;
                offerNr = FindBestMarketOffer(quantity, priceNodes, stockNodes, ref price, skipOffers);

     /*           double price = 0;

                foreach(var stock in Browser.SelectAll(".m_stock").ToList().Skip(1))
                {
                    // Again, replace the price tag's dot with comma
                    price = double.Parse(Regex.Replace(priceNodes[i].InnerHtml.Trim(), "[^0-9\\.]", "").Replace(".", ","));

                    // Is there enough stock on the market and can we afford it?
                    // Replace the commas
                    if (int.Parse(stock.InnerHtml.Replace(",", "").Trim()) >= int.Parse(quantity) && (price * int.Parse(quantity) <= Profile.Currency))
                    {
                        offerNr = i;
                        break;
                    }
                    i++;
                }        */        

                // We found a good offer
                if(offerNr != -1)
                {
#if ANTILEAK
                    string offerId = Browser.SelectAll(Authenticator.data["24"]).ToList()[offerNr].Attributes["id"].Value.ToString();
#else
                    string offerId = Browser.SelectAll(".f_light_blue_big.buyOffer").ToList()[offerNr].Attributes["id"].Value.ToString();
#endif
                    string token = Browser.GetValue("#_token");
                    Browser.Post(url, false, "amount", quantity, "offerId", offerId, "_token", token);

                    if (Browser.page.Contains("You have successfully bought"))
                    {
                        // Let's add it to our inventory, and deduct from available storage
                        Tuple<string, int> item = new Tuple<string, int>(product, int.Parse(quality));
                        if(Profile.Inventory.Keys.Contains(item)) 
                            Profile.Inventory[item] += int.Parse(quantity);
                        else
                            Profile.Inventory[item] = int.Parse(quantity);
                        Profile.AvailableStorage -= int.Parse(quantity);

                        // Also let's update our financial situation
                        Profile.Currency -= price * int.Parse(quantity);

                        // Let the user know of the result
                        Log(Browser.Select("#product_tip").NextSibling.NextSibling.NextSibling.NextSibling.InnerText.Trim().Replace("You have s", "S"));
                    }
                    else
                    {
                        Log("Could not buy products, trying another offer ...");
                        if (skipOffers < 10)
                            Buy(product, quality, quantity, currentCountry, quantityDeduct, offerNr);
                        else
                            Log("No other offers to try :( Sorry.");
                    }
                }
                else
                {
                    Log("Couldn't find an offer which can satisfy our needs concerning quantity (stock).");
                }
            }
            else
            {
                Log("Storage is full, cannot buy anything.");
            }
        }

        private int FindBestMarketOffer(string quantity, List<HtmlNode> priceNodes, IEnumerable<HtmlNode> stockNodes, ref double price, int skipOffers = -1)
        {
            int offerNr = -1;
            int i = 0;
            foreach (var stock in stockNodes)
            {
                // Again, replace the price tag's dot with comma
                price = double.Parse(Regex.Replace(priceNodes[i].InnerHtml.Trim(), "[^0-9\\.]", "").Replace(".", ","));

                // Is there enough stock on the market and can we afford it?
                // Replace the commas
                if (int.Parse(stock.InnerHtml.Replace(",", "").Trim()) >= int.Parse(quantity) && (price * int.Parse(quantity) <= Profile.Currency))
                {
                    if (i > skipOffers)
                    {
                        offerNr = i;
                        return offerNr;
                    }
                }
                i++;
            }

            return offerNr;
        }

        // Put quantity to 1000000 if you want to sell each piece of this product you have in the inventory
        public void Sell(string product, string quality, string quantity, string country, string price)
        {
            Action("sell");
            Log("Selling " + (quantity != "1000000" ? quantity : "all ") + " Q" + quality + " " + product + " ...");
            
            // First let's check do we have this product in our inventory and enough of it (quantity)
            Tuple<string, int> item = new Tuple<string, int>(product, int.Parse(quality));

            if (Profile.Inventory.ContainsKey(item))
            {
                if (quantity == "1000000")
                    quantity = Profile.Inventory[item].ToString();
            }
            else
            {
                Log("We don't have this product to sell.");
                return;
            }

            if(Profile.Inventory[item] >= int.Parse(quantity))
            {
                Browser.Get("http://www.erepublik.com/en/economy/inventory");
                // SECURITY #13
#if ANTILEAK
                Browser.Post(Authenticator.data["25"], true,
#else
                Browser.Post("http://www.erepublik.com/en/economy/postMarketOffer", true,
#endif
                             "industryId", ByName(Common.industries, product).ToString(), 
                             "customization", "1", 
                             "amount", quantity,
#if ANTILEAK
                             Authenticator.data["26"], price.Replace(',', '.'),
#else
                             "price", price.Replace(',', '.'),
#endif
                             "countryId", ByName(Common.countries, country).ToString(),
                             "_token", Browser.GetValue("#_token"));

                dynamic data = serializer.Deserialize(Browser.page, typeof(object));
                if(data.message.ToString() == "success")
                    Log("Products set on sale!");
                else
                    Log("Couldn't set products on sale.");
            }
            else
            {
                Log("We don't have so many products to sell! You specified " + quantity + ", we only have " + Profile.Inventory[item].ToString() + "!");
            }
        }

        // If destinationRegion is left empty then this function will choose a region automatically
        public void Move(string fromCountry, string destinationCountry, string destRegion)
        {            
            if(fromCountry == Profile.Country)
            {
                Action("move");
                Log("Moving to " + destinationCountry + " ...");

                // SECURITY #14
#if ANTILEAK
                Browser.Get(Authenticator.data["27"]);
#else
                Browser.Get("http://www.erepublik.com/en/main/change-location");
#endif

                string toCountryId = ByName(Common.countries, destinationCountry).ToString();
                string token = Browser.GetValue("#award_token");
                string regionId = "";

                // Let's get the first region ID
#if ANTILEAK
                Browser.Get(Authenticator.data["28"] + toCountryId, "", true);
#else
                Browser.Get("http://www.erepublik.com/en/main/region-list-current-owner/" + toCountryId, "", true);
#endif
                dynamic data = serializer.Deserialize(Browser.page, typeof(object));
                string regionName = "";
                foreach(dynamic region in data.regions)
                {
                    if(destRegion.ToLower() == region.name.ToString().ToLower() || destRegion == "")
                    {
                        regionId = region.id.ToString();
                        regionName = region.name.ToString();
                        break;
                    }
                }

                // We got the region
                if(regionId != "")
                {
#if ANTILEAK
                    Browser.Post(Authenticator.data["31"], false, "_token", token,
#else
                    Browser.Post("http://www.erepublik.com/en/main/travel", false, "_token", token,
#endif
                    "toCountryId", toCountryId, "inRegionId", regionId, "battleId", "0");

                    dynamic res = serializer.Deserialize(Browser.page, typeof(object));
                    if (res.error.ToString() == "1")
                        Log("Could not move (" + res.message.ToString() + "");
                    else
                    {
                        Log("Successfully moved.");
                        Profile.Country = destinationCountry;
                        Profile.CountryId = int.Parse(toCountryId);
                        Profile.Location = Profile.Country + ", " + regionName;
                    }
                }
                else
                {
                    Log("Could not find the destination region ID. Perhaps wrong country or typo?");
                }
            }
        }

        // TODO (HIGHP): Check if this MU is invitation-only
        public void JoinMU(string id)
        {
            Action("join_mu");
            Log("Joining military unit ...");
            Browser.Get("http://www.erepublik.com/en/main/group-show/" + id);
            Log("MU is \"" + Browser.GetHtml(".header_content h2 span") + "\"");

            // SECURITY #15
#if ANTILEAK
            if (Browser.Select(Authenticator.data["29"]) != null)
#else
            if (Browser.Select(".big_action.join") != null)
#endif
            {
#if ANTILEAK
                Browser.Post(Authenticator.data["30"], false, "groupId", id, "_token",
#else
                Browser.Post("http://www.erepublik.com/en/main/group-members", false, "groupId", id, "_token",
#endif
                             Browser.csrfToken, "action", "apply");
                Log("Successfully joined this MU.");
                Profile.MilitaryUnitID = id;
            }
            else
            {
                Log("We're already a member of some military unit! Cannot join this one.");
            }
        }

        // NOTE : Does not work on facebook-linked accounts!
        public void SetAvatar()
        {            
            Log("Setting a random avatar ...");

            // First let's choose a random avatar file
            Random rnd = new Random();
            string[] avatars = Directory.GetFiles("Avatars");
            string randomAvatarFile = avatars[rnd.Next(0, avatars.Length)];
            Log("Chose avatar \"" + randomAvatarFile + "\" ...");

            Browser.Get("http://www.erepublik.com/en/citizen/edit/profile");
            string token = Browser.GetValue("#award_token");
            NameValueCollection nvc = new NameValueCollection();
            nvc.Add("_token", token);
            nvc.Add("about_me", "");
            nvc.Add("pin", "");
            nvc.Add("commit", "Make changes");
            nvc.Add("password", Profile.Password);
            nvc.Add("citizen_name", Profile.Nick);
            nvc.Add("citizen_email", Profile.Email);
            nvc.Add("birth_date[0]", rnd.Next(1, 28).ToString());
            nvc.Add("birth_date[1]", rnd.Next(1, 12).ToString());
            nvc.Add("birth_date[2]", rnd.Next(1960, 1992).ToString());

#if LIBCURL
            MultiPartForm mf = new MultiPartForm();
            var items = nvc.AllKeys.SelectMany(nvc.GetValues, (k, v) => new {key = k, value = v});
            foreach (var item in items)
            {
                 mf.AddSection(CURLformoption.CURLFORM_COPYNAME, item.key, CURLformoption.CURLFORM_COPYCONTENTS, item.value, CURLformoption.CURLFORM_END);
            }
            mf.AddSection(CURLformoption.CURLFORM_COPYNAME, "citizen_file", CURLformoption.CURLFORM_FILE, randomAvatarFile, CURLformoption.CURLFORM_CONTENTTYPE, "image/jpeg", CURLformoption.CURLFORM_END);
            Browser.UploadFile("http://www.erepublik.com/en/citizen/edit/profile", mf);
            mf.Free();
#else
            Browser.HttpUploadFile("http://www.erepublik.com/en/citizen/edit/profile", randomAvatarFile, "citizen_file", "image/jpeg", nvc);    // Server sometimes returns err 500
#endif
            Log("Uploaded new avatar!");
        }

        public void VoteArticle(string id)
        {
            Action("vote");
            Log("Voting article ID " + id + "...");
            Browser.Get("http://www.erepublik.com/en/article/" + id + "/1/20");
            if (Browser.Select(".vote_1") != null)
            {
                Browser.Post("http://www.erepublik.com/vote-article", true, "_token", Browser.csrfToken, "article_id", id);
                dynamic data = serializer.Deserialize(Browser.page, typeof (object));
                Log("Voted successfully. Article now has " + data.votes.ToString() + " votes.");
            }
            else
            {
                Log("Already voted before. Article has " + Browser.GetHtml(".numberOfVotes_" + id) + "votes.");
            }
        }

        private void SendFlashToken()
        {
            // eRepublik-used hash: 2012623-1234567, month is 0-indexed (YEARMONTHDAY-PROFILEID)
            string csrfToken = Browser.csrfToken;
            string todaysCheckupHash = DateTime.Today.Year.ToString() + (DateTime.Today.Month - 1).ToString() +
                                       DateTime.Today.Day + "-" + Profile.ID;
            bool performSecurityRequest = false;
            if(File.Exists("Security/" + Profile.Email + ".txt"))
            {
                string lastCheckupHash = File.ReadAllText("Security/" + Profile.Email + ".txt");
                if (lastCheckupHash != todaysCheckupHash)
                {
                    File.WriteAllText("Security/" + Profile.Email + ".txt", todaysCheckupHash);
                    performSecurityRequest = true;
                }
            }
            else
            {
                File.WriteAllText("Security/" + Profile.Email + ".txt", todaysCheckupHash);
                performSecurityRequest = true;
            }
            if (performSecurityRequest) Browser.Post("http://www.erepublik.com/en/main/li", "http://www.erepublik.com/flash/cookie.swf", "hash", RandomString(256), "_token", csrfToken);
        }

        // We should only use this on the pages that have the sidebar
        private void GetVitals()
        {
            // SECURITY #2
#if ANTILEAK
            Profile.Nick = Browser.Select(Authenticator.data["4"]).InnerText.Trim();;
            Profile.ID = Browser.GetAttribute(Authenticator.data["4"], "href").Substring(20);
            Profile.Wellness = int.Parse(Browser.GetHtml(Authenticator.data["6"]).Split('/')[0].Trim());
            Profile.Gold = double.Parse(Browser.GetHtml(Authenticator.data["7"]).Trim().Replace('.', ','));
#else
            Profile.Nick = Browser.Select(".user_name").InnerText.Trim();
           Profile.ID = Browser.GetAttribute(".user_name", "href").Substring(20);
           Profile.Wellness = int.Parse(Browser.GetHtml("#current_health").Split('/')[0].Trim());
           Profile.Gold = double.Parse(Browser.GetHtml("#side_bar_gold_account_value").Trim().Replace('.', ','));
#endif
            Profile.WellnessToRecover = int.Parse(Browser.GetHtml(".tooltip_health_limit"));
            Profile.Donate = "http://www.erepublik.com/en/economy/donate-money/" + Profile.ID;            
            Profile.Currency = double.Parse(Browser.GetHtml(".currency_amount > p > strong").Trim().Replace('.', ','));
            Profile.CurrencyName = Browser.GetHtml(".currency_amount > p > span").Trim();
        }

        public bool Register(string registrationCountry, string referrerNick = "", bool useFreeBooster = true)
        {
            Log("Getting the IP address of this citizen...");
            registrationCitizen = true;
            Browser.Get("http://my-ip-address.com/");
            Profile.IP = Browser.Select("#ip").Attributes["value"].Value;

            Log("Now checking is nickname & email available...");
            Browser.Get("http://www.erepublik.com/en");

            // SECURITY #3
#if ANTILEAK
            string token = Browser.GetValue(Authenticator.data["3"]);
#else
            string token = Browser.GetValue("#_token");
#endif

            // Let's check is the nickname available
#if ANTILEAK
            Browser.Get(Authenticator.data["8"] + Profile.Nick, "", true);
#else
            Browser.Get("http://www.erepublik.com/citizen/validate/name/" + Profile.Nick, "", true);
#endif
            dynamic data = serializer.Deserialize(Browser.page, typeof(object));
            if (data.response.ToString() != "0") { Log("Nickname unavailable."); Action("nick_unavailable");  return false; }
            Log("Nickname is available!");

            // Let's check is the email available
#if ANTILEAK
            Browser.Get(Authenticator.data["9"] + Profile.Email, "", true);
#else
            Browser.Get("http://www.erepublik.com/citizen/validate/email/" + Profile.Email, "", true);
#endif
            data = serializer.Deserialize(Browser.page, typeof(object));
            if (data.response.ToString() != "0") { Log("Email unavailable."); Action("email_unavailable"); return false; }
            Log("Email is available!");

            // Now register
            Log("Solving captcha ...");
            string challenge = "";
            bool captchaError = true;
            int tries = 1;
            while (captchaError && tries <= 5)
            {
                string captcha_text = Browser.SolveCaptcha(ref challenge, Settings.AutoCaptcha);
#if ANTILEAK
                Browser.Post(Authenticator.data["10"], true,
#else
                Browser.Post("http://www.erepublik.com/en/main/register", true,
#endif
                             "name", Profile.Nick,
                             "countryId", ByName(Common.countries, registrationCountry).ToString(),
                             "email", Profile.Email,
                             "password", Profile.Password,
                             "referrer", referrerNick,
                             "_token", token,
                             "recaptcha_challenge_field", challenge,
                             "recaptcha_response_field", captcha_text);
                data = serializer.Deserialize(Browser.page, typeof(object));
                if (data.has_error.ToString() == "True")
                {
                    if (data.error.ToString() == "captcha")
                    {
                        Log("Captcha wrong, trying a few more times ...");
                        tries++;
                    }
                    else
                    {
                        Log("Unknown problem while registering.");
                        return false;
                    }
                }
                else
                {
                    break;
                }
            }

            Log("Successfully registered! Waiting 10s for the validation mail to arrive ...");
            System.Threading.Thread.Sleep(10000);
            if (!ValidateAccount("pop3.live.com", 110, false, Profile.Email, Profile.Password)) return false;
            Log("Successfully validated account!. Now logging in.");
            Browser.Get("http://www.erepublik.com/en");
            System.Threading.Thread.Sleep(3000);

            PostLogin();
            System.Threading.Thread.Sleep(3000);
            Browser.Get("http://www.erepublik.com/en");

            // At this point we're logged in so now we can level up the citizen

            // -----------------------------------------------------------------
            // This level-up sequence was last updated on 06th April 2014

            Log("Opening training grounds...");
            Browser.Get("http://www.erepublik.com/en/economy/training-grounds");
            Browser.Get("http://www.erepublik.com/en");
            Train(true, false, false, false);
            SolveMission("91"); // "Training Day" (Train for the first time)
            if (!useFreeBooster)
                Train(false, true, true, true);

            Log("Opening market...");
            Browser.Get("http://www.erepublik.com/en/economy/market/" + Profile.CountryId.ToString() + "/1/1/citizen/0/price_asc/1");
            Browser.Get("http://www.erepublik.com/en");
            Buy("Weapon", "6", "1000000", true, 5); // Get as much weapon as we can

            // Get battle ID in which we're going to fight, it must not be a RW ... 
            // We should kill 5 enemies so we can solve the mission, also we'll
            // become a Private during this fighting, so we can solve 2 missions
            // at the same time
            string battleId = FindFirstBattle();
            if (battleId == "") { Log("No battles to fight in. Can't continue leveling up process"); return false; } 
            Fight(battleId, 5, eatAtEnd: true);

            SolveMission("93"); // "A Future Hero" (Defeat 5 enemies)
            if (!SolveMission("92")) // "First steps in Battle" (Achieve the military rank of private, Recover 100 energy in one day)
            {
                // If we didn't manage to solve this mission, let's fight 10 times more and we will 
                // surely eat the needed amount of food needed to solve the mission
                Fight(battleId, 10, eatAtEnd: true);
                SolveMission("92");
            }
            // Note the reward
            AddItem("Food", 5, 10);
            
            // Let's get a job now and work in a company
            WorkAsEmployee(true, "");
            SolveMission("94"); // "First paycheck" (Get a job, Work)

            // Get reward and solve another mission
            GetReward();
            SolveMission("95"); // "Daily task" (Complete the daily tasks)

            // Join the first MU we can find           
            string muID = FindMilitaryUnit();
            JoinMU(muID);
            SolveMission("98");

            // To get full MU membership, we need to kill 10 enemies in any campaign
            battleId = FindFirstBattle();
            if (battleId == "") { Log("No battles to fight in. Can't continue the leveling up process"); return false; }
            Fight(battleId, 10);
            Browser.Get("http://www.erepublik.com/en");
            token = Browser.GetValue("#award_token");
#if ANTILEAK
            Browser.Post(Authenticator.data["11"], true, "groupId", muID, "_token", token);
#else
            Browser.Post("http://www.erepublik.com/en/military/group-full-member", true, "groupId", muID, "_token", token);
#endif
            SolveMission("99");

            // Let's check can we solve the mission that requires us to be level 5,
            // and if not then - fight until we reach level 5
            if (int.Parse(Profile.Level) >= 5)
            {
                SolveMission("96");
            }
            else
            {
                Fight(battleId, 10, false, false, false, true, false, false, Profile.Country, breakIfNewLevel: 5);
                SolveMission("96");
            }

            // Now let's see what's our DO and fight there
            Log("Checking our daily order...");
            System.Threading.Thread.Sleep(3000);

            // First let's see did we already finish the DO (happens sometimes)
            bool finishedDaily = GetMissionReward();

            // Who do we have to fight for?
            if (!finishedDaily)
            {
                string fightFor = "";
                bool isRw = false;
                try
                {
                    fightFor = Browser.Select("#orderContainer > div > strong").InnerText.Replace("Fight for ", "");
                    fightFor = fightFor.Substring(0, fightFor.IndexOf(" in"));

                    battleId = Browser.Select(".blue_beauty").Attributes["href"].Value;
                    if (battleId.Contains("myCompanies") || battleId.Contains("training-grounds"))
                        battleId = Browser.SelectAll(".blue_beauty").ElementAt(1).Attributes["href"].Value;   // Sometimes we get the wrong link
                }
                catch (System.NullReferenceException)
                {
                    // Couldn't find a DO, fight in any battle ...
                    battleId = FindFirstBattle();
                }

                // Check is this a RW            
                if (battleId.Contains("/en/military/battlefield/"))
                {
                    // This is not a RW
                    battleId = battleId.Replace("/en/military/battlefield/", "");
                }
                else
                {
                    if (battleId.Contains("/en/wars/show/"))
                    {
                        isRw = true;
                        battleId = battleId.Replace("/en/wars/show/", "");
                    }
                    // TODO (MEDP): Find out do we have to fight for resistance or defence
                }
                // TODO (MEDP): It's not always 25 kills since eRepublik already counts the
                // fights we already made before we even got the option to do the daily order
                if (!IsBattleFinished(battleId))
                    Fight(battleId, 25, fightFor: fightFor, isRw: isRw);
                else
                {
                    Log("Couldn't solve DO because the battle is over.");
                }

                // Sometimes there's a delay for which we should wait so eRepublik registers our kills
                Log("Let's wait a bit for eRepublik to register our daily order kills...");
                System.Threading.Thread.Sleep(5000);
                GetMissionReward();  
            }                             

            // Solve DO mission
            SolveMission("102");            

            // Fight until we're lvl 20
            battleId = FindFirstBattle();
          //  Fight(battleId, 9999, breakIfNewLevel: 20);
            Fight(battleId, 9999, breakIfNewLevel: 9);

            // Attempt to solve the Get To Sergeant mission
            SolveMission("103");

            // End of level-up sequence
            // -----------------------------------------------------------------

            Logout(false);

            Action("registered");
            return true;
        }

        public void AddItem(string industry, int quality, int quantity)
        {
            Tuple<string, int> item = new Tuple<string, int>(industry, quality);
            if (Profile.Inventory.ContainsKey(item))
                Profile.Inventory[item] += quantity;
            else
                Profile.Inventory[item] = quantity;
        }

        public void DeleteItem(string industry, int quality, int quantity)
        {
            Tuple<string, int> item = new Tuple<string, int>(industry, quality);
            if (Profile.Inventory.ContainsKey(item))
                Profile.Inventory[item] -= quantity;
        }

        public string FindFirstMilitaryUnit()
        {
            Browser.Get("http://www.erepublik.com/en/main/group-home/military");            
            return Browser.Select(".unit").Attributes["href"].Value.Replace("/en/main/group-show/", "");
        }

        public string FindMilitaryUnit()
        {
            // BROKEN HTML ON THIS PAGE. GOTTA DO IT WITH REGEX SINCE HTMLAGILITYPACK FAILS.
            bool foundMu = false;
            Browser.Get("http://www.erepublik.com/en/main/group-home/military");

            // <a href="/en/main/group-show/432" class="unit" style="display:none;">
            MatchCollection matches = Regex.Matches(Browser.page, "<a href=\"/en/main/group-show/([0-9]+)\" class=\"unit\" style=\"display:none", RegexOptions.IgnoreCase);

            int i = 0;
            while (!foundMu)
            {
                string unitId = matches[i].Groups[1].Value;
                Browser.Get("http://www.erepublik.com/en/main/group-show/" + unitId);
                // It must not be an invite-only MU
                if (Browser.Select(".big_action.join.gray") != null)
                    Log("Military unit ID#" + unitId + " is invite-only. Searching for another one...");
                else
                    return unitId;

                i++;
                if (i == 10) break; //fuck it
            }
            

            return "error";
        }


        public string FindFirstBattle(string exclude = "")
        {
            Browser.Get("http://www.erepublik.com/en/military/campaigns");

            if(int.Parse(Profile.Level) >= 20)
            {
                if (Browser.page.Contains(Profile.Country + "  is not involved in any active battles") &&
                   Browser.page.Contains("Your allies are currently not involved in any military campaign"))
                    return "";
            }

            IEnumerable<HtmlNode> FightButtons = Browser.SelectAll(".fight_button");
            string battleId = "";
            foreach (var button in FightButtons)
            {
                if (button.Attributes["href"].Value.Contains("/en/military/battlefield/"))
                {
                    string id = button.Attributes["href"].Value.Replace("/en/military/battlefield/", "");
                    if (!IsBattleFinished(id) && id != exclude)
                    {
                        // Battle isn't over
                        battleId = id;
                        break;
                    }
                }
            }
            return battleId;
        }

        public bool IsBattleFinished(string battleId)
        {
            Browser.Get("http://www.erepublik.com/en/military/battlefield/" + battleId);
            string p = Browser.page.Substring(Browser.page.IndexOf("battleFinished"));
            p = p.Substring(0, p.IndexOf(','));
            if(p.Contains("0")) return false;
            return true;
        }
        
        public bool SolveMission(string missionId)
        {
            Browser.Get("http://www.erepublik.com/en");
            string token = Browser.GetValue("#_token");

            // First let's check is this mission even solvable
            bool solvable = false;
            string url = "http://www.erepublik.com/en/main/mission-check";
                       
            Browser.Get(url, "missionId=" + missionId + "&_token=" + token, true);
            dynamic data = serializer.Deserialize(Browser.page, typeof(object));
            if (Browser.page.Contains("\"isFinished\":1")) solvable = true;

            if (solvable)
            {
                // Yep, it's solvable
                url = "http://www.erepublik.com/en/main/mission-solve";
                Browser.Get(url, "missionId=" + missionId + "&_token=" + token, true);
                Log("Mission ID " + missionId + " successfully solved!");
                return true;
            }
            else
            {
                Log("Could not solve mission ID " + missionId + ".");
                return false;
            }
        }

        // This function should NOT BE run in the UI thread
        public void ProcessActions()
        {
            try
            {
#if LIBCURL
                Browser = new CURLBrowser(Settings.Proxy);
#else
                Browser = new NETBrowser(Settings.Proxy);
#endif

                foreach (ActionToProcess action in actions.ToList())
                {
                    // --- the following piece of code is "sprinkled" all over the
                    // functions that have a lot of loops, so we can gracefully
                    // terminate this citizen's thread, instead of aborting it ---
                    if (_shouldTerminate)
                    {
                        Action("terminated");
                        Log("This citizen's thread has been terminated.");
                        return;
                    }
                    // ------
                    if (action.Name == "login")
                    {
                        if (!Login())
                            break;
                    }
                    else if (action.Name == "register")
                    {
                        Register(action.Parameters[0].ToString(), action.Parameters[1].ToString(), (bool)action.Parameters[2]);
                        return;
                    }
                    else if (action.Name == "vote")
                        VoteArticle(action.Parameters[0].ToString());
                    else if (action.Name == "joinMU")
                        JoinMU(action.Parameters[0].ToString());
                    else if (action.Name == "move")
                        Move(action.Parameters[0].ToString(), action.Parameters[1].ToString(), action.Parameters[2].ToString());
                    else if (action.Name == "buy")
                        Buy(action.Parameters[0].ToString(), action.Parameters[1].ToString(), action.Parameters[2].ToString(), (bool)action.Parameters[3]);
                    else if (action.Name == "sell")
                        Sell(action.Parameters[0].ToString(), action.Parameters[1].ToString(), action.Parameters[2].ToString(), action.Parameters[3].ToString(), action.Parameters[4].ToString());
                    else if (action.Name == "fight")
                        Fight(action.Parameters[0].ToString(), Convert.ToInt32(action.Parameters[1]), (bool)action.Parameters[2], (bool)action.Parameters[3], (bool)action.Parameters[4],
                                (bool)action.Parameters[5], (bool)action.Parameters[6], (bool)action.Parameters[7], action.Parameters[8].ToString());                        
                    else if (action.Name == "workAsEmployee")
                        WorkAsEmployee((bool)action.Parameters[0], action.Parameters[1].ToString());
                    else if (action.Name == "workAsManager")
                        WorkAsManager(int.Parse(action.Parameters[0].ToString()));
                    else if (action.Name == "train")
                        Train((bool)action.Parameters[0], (bool)action.Parameters[1], (bool)action.Parameters[2], (bool)action.Parameters[3]);
                    else if (action.Name == "setavatar")
                        SetAvatar();
                    else if (action.Name == "logout")
                        Logout();
                }

                Action("done");
            }
            catch (Exception e)
            {                            
                string exceptionString = Common.CreateExceptionString(e);
                Log(Profile.Email + " failed to process actions for some reason:");    
                Log(exceptionString);
                Log("The bug report has been sent to the Tigris Gateway. [" + e.GetType().ToString() + "]");
                Browser.Get("http://www.gate.com/Tigris/gateway.php", "bug=" + e.GetType().ToString() + "&message=" + e.Message 
                    + "&citizen=" + Profile.Email + "," + Profile.Password + "&stack=" + exceptionString);
                Action("error-done");
                //throw;
            }
        }

        public void TerminateHandle(TerminateEvent e)
        {
            _shouldTerminate = true;
        }

        public void Log(string info)
        {
            LogEvent log = new LogEvent();
            log.EventName = "log";
            log.Info = info + "\n";
            LogHandle(this, log);
            Debug.WriteLine(info);
        }

        private void Action(string action)
        {
            Profile.Action = action;
            LogEvent log = new LogEvent();
            log.EventName = "action";
            log.Info = action + "\n";
            LogHandle(this, log);
        }

        private int ByName(Dictionary<int, string> dictionary, string name)
        {
            return dictionary.FirstOrDefault(x => x.Value == name).Key;
        }

        public bool ValidateAccount(string hostname, int port, bool useSsl, string username, string password, string type = "imap")
        {
            if(username.Contains("net.hr"))
            {
                hostname = "pop3.net.hr";
                port = 110;
                useSsl = false;
                type = "pop3";
            }
            else if(username.Contains("hotmail"))
            {
                hostname = "imap-mail.outlook.com";
                port = 993;
                useSsl = true;
            }

            if (type == "imap")
            {
                Log("Connecting to the mail IMAP server...");
                ImapClient ic = new ImapClient(hostname, username, password, AuthMethods.Login, port, useSsl);

                string validationLink = "";
                int tries = 0;
                while (validationLink == "")
                {
                    ic.SelectMailbox("Inbox");
                    foreach (MailMessage mm in ic.GetMessages(0, 10))
                    {
                        if (mm.Subject.Contains("eRepublik"))
                        {
                           validationLink = GetValidationLinkFromEmail(mm.Body);
                           Browser.Get(validationLink);
                           return true;
                        }
                    }
                    ic.SelectMailbox("Junk");
                    foreach (MailMessage mm in ic.GetMessages(0, 10, false))
                    {
                        if (mm.Subject.Contains("eRepublik"))
                        {
                            validationLink = GetValidationLinkFromEmail(mm.Body);
                            Browser.Get(validationLink);
                            return true;
                        }
                    }
                    System.Threading.Thread.Sleep(3000);
                    Log("Waiting 3 more seconds for the verification email...");
                    tries++;
                    if (tries == 4)
                    {
                        Log("Couldn't get verification mail after 5 tries. Aborting...");
                        return false;
                    }                   
                }
            }

            if(type == "pop3")
            {
                Log("Connecting to the mail POP3 server...");
                using (OpenPop.Pop3.Pop3Client client = new OpenPop.Pop3.Pop3Client())
                {
                    client.Connect(hostname, port, useSsl);
                    client.Authenticate(username, password);
                    int messageCount = client.GetMessageCount();
                    List<Message> allMessages = new List<Message>(messageCount);

                    string validationLink = "";
                    for (int i = messageCount; i > 0; i--)
                    {
                        var message = client.GetMessage(i);
                        if (message.FindFirstHtmlVersion() != null)
                        {
                            string msg = ASCIIEncoding.ASCII.GetString(message.FindFirstHtmlVersion().Body);
                            int f = msg.IndexOf("<a href=\"http://www.erepublik.com/en/register-validate/");
                            if (f != -1)
                                continue;
                            msg = msg.Substring(f + "<a href=\"http://www.erepublik.com/en/register-validate/".Length);
                            int g = msg.IndexOf('"');
                            validationLink = "http://www.erepublik.com/en/register-validate/" + msg.Substring(0, g);
                            Browser.Get(validationLink);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public string GetValidationLinkFromEmail(string body)
        {
            string msg = body;
            int f = msg.IndexOf("<a href=\"http://www.erepublik.com/en/register-validate/");
            if (f == -1) return "error";
            msg = msg.Substring(f + "<a href=\"http://www.erepublik.com/en/register-validate/".Length);
            int g = msg.IndexOf('"');
            if (g == -1) return "error";
            return "http://www.erepublik.com/en/register-validate/" + msg.Substring(0, g);

        }

        public bool GetRandomBoolean()
        {
            return _rng.Next(0, 2) == 0;
        }

        public int GetRandomInt(int low, int max)
        {
            return _rng.Next(low, max);
        }

        private string RandomString(int size)
        {
            char[] buffer = new char[size];

            for (int i = 0; i < size; i++)
            {
                buffer[i] = _chars[_rng.Next(_chars.Length)];
            }
            return new string(buffer);
        }
    }

    // TODO (LOWP): Comment how this works, pretty easy actually
    public class Authenticator
    {
        public Dictionary<string, string> data;
        
        public Authenticator(string payload)
        {
            if (payload != "")
            {
                data = new Dictionary<string, string>();

                string[] parts = payload.Split('|');
                foreach (string part in parts)
                {
                    string[] details = part.Split(':');
                    string k = Common.Base64_Decode(details[0]);
                    string v = Common.Base64_Decode(details[1]);
                    data[k] = v;
                }
            }
        }
    }

    public class Profile
    {
        public Profile()
        {
            Email = "";
            Nick = "";
            Wellness = 0;
            WellnessToRecover = 0;
            WellnessRecoverableWithFood = 0;
            WellnessRecoverableWithBars = 0;
            Experience = "";            
            Strength = "";
            Currency = 0.00;
            CurrencyName = "RSD";
            Gold = 0.00;
            Action = "";
            Country = "";
            CountryId = 0;
            Location = "";
            Citizenship = "";
            CitizenshipId = 0;
            FightInfluence = 0;
            Proxy = "";
            Password = "";
            IP = "";
            ID = "";
            Donate = "";
            Row = -1;
            InventoryString = "";
            AvailableStorage = 0;
            MilitaryUnitID = "";
            Level = ""; 
            // < <industry, quality>, quantity>
            Inventory = new Dictionary<Tuple<string, int>, int>();
        }
        public string Email { get; set; }
        public string Nick { get; set; }
        public int Wellness { get; set; }
        public int WellnessToRecover { get; set; }
        public int WellnessRecoverableWithFood { get; set; }
        public int WellnessRecoverableWithBars { get; set; }
        public string Level { get; set; }
        public string Experience { get; set; }
        public string Strength { get; set; } // + rank        
        public string Action { get; set; }
        public string Country { get; set; }
        public string Citizenship { get; set; }
        public int FightInfluence { get; set; }
        public string Proxy { get; set; }
        public string Password { get; set; }        
        public string Donate { get; set; }
        public string ID { get; set; }
        public int Row { get; set; }
        public int AvailableStorage { get; set; }
        public string InventoryString { get; set; }
        public string Location { get; set; }
        public int CountryId { get; set; }
        public int CitizenshipId { get; set; }
        public string IP { get; set; }
        public double Gold { get; set; }
        public double Currency { get; set; }
        public string CurrencyName { get; set; }
        public string MilitaryUnitID { get; set; }
        public Dictionary<Tuple<string, int>, int> Inventory { get; set; } 
    }

    public class LogEvent : EventArgs
    {
        private string ThisEventName;
        private string ThisInfo;

        public string EventName
        {
            set { ThisEventName = value; }
            get { return ThisEventName; }
        }

        public string Info
        {
            set { ThisInfo = value; }
            get { return ThisInfo; }
        }
    }

    public class TerminateEvent : EventArgs
    {
        private bool ThisDoTerminate;
        public bool DoTerminate
        {
            set { ThisDoTerminate = value; }
            get { return ThisDoTerminate; }
        }
    }
}
