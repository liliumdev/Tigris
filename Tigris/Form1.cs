using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Specialized;
using OpenPop.Pop3;
using System.IO;
using System.Management;
using Tigris.Properties;
using System.Resources;

#if LIBCURL
using SeasideResearch.LibCurlNet;
#endif

namespace Tigris
{
    public partial class Form1 : Form
    {
        // A dictionary that contains citizens' logs, key is their row number in the main statistics table
        public Dictionary<int, string> logs = new Dictionary<int, string>();
        public class Account
        {            
            public string email, password, nickname;
            public Account(string e, string p, string n = "")
            {
                email = e;
                password = p;
                nickname = n;
            }
        }
        
        public Queue<Account> citizens_queue = new Queue<Account>();
        public Queue<string> proxies = new Queue<string>();
        public Queue<Account> citizens_to_register_queue = new Queue<Account>();

        // Used only for terminating threads when exiting the form
        private volatile bool _terminating = false;
        public delegate void TerminateHandler(TerminateEvent e);
        public event TerminateHandler TerminateHandle;
        public void Terminate()
        {
            TerminateEvent t = new TerminateEvent();
            TerminateHandle(t);
        }
        
        // key: email, value: row in the statistics table
        public Dictionary<string, int> citizen_rows = new Dictionary<string, int>();

        // The small window that shows the citizen logs
        public LogForm lf = new LogForm();

        // Which citizen's log are we showing right now
        public int currentLog = 0;

        // Actions which will be performed by each citizen
        public List<ActionToProcess> actions = new List<ActionToProcess>();

        // Used for authentication/anti-leak protection
        public string c_payload = "";
        public NETBrowser br = new NETBrowser();
        public string a;    // Hardware ID
        public string b;    // Username
        public string c;    // Password
 
        public Form1()
        {            
            InitializeComponent();
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            // Now let's authenticate to the Tigris Gateway
            bool authenticated = false;

#if ANTILEAK
            this.Text = "Tigris " + Constants.VERSION;
            FormSplash splash = new FormSplash();
            splash.Show();
            splash.Update();

            br.Get(Constants.HOST + "/Tigris/version.html");
            if (Constants.VERSION != br.page)
                MessageBox.Show("You have an outdated version, please download the newest one!", "Update warning");

            kryptonNavigator1.Enabled = false;
            groupBoxActions.Enabled = false;
            
            List<string> authLines = LoadFileLines("auth.txt");
            if (authLines.Count >= 1)
            {
                string[] authDetails = authLines[0].Split(',');
                if (authDetails.Count() == 2)
                {
                    a = GetHWID(); // hardware id
                    b = authDetails[0]; // username
                    c = authDetails[1]; // password                    
                    br.Get(Constants.HOST + "/Tigris/gateway.php", "a=" + a + "&b=" + b + "&c=" + c + "&d=init");
                    string r = br.page;
                    if (r.Length > 100 || r.StartsWith("wrongc"))
                    {
                        if (r.StartsWith("gjmate"))
                        {
                            // Successful login
                            r = r.Replace("gjmate", "");
                            c_payload = Decrypt(r, GetHWID(false));
                            if (c_payload.Length < 100)
                            {
                                UpdateSplashForm(splash, Resources.unspecified, 2000, false);
                                MessageBox.Show("Unspecified error, please report the following to the creator: " + r, "Error");
                                Application.Exit();
                            }
                        }
                        else if (r == "wrongc")
                        {
                            UpdateSplashForm(splash, Resources.wrong, 2000, true);
                        }
                        else
                        {
                            UpdateSplashForm(splash, Resources.unspecified, 2000, false);
                            MessageBox.Show("Unspecified error, please report the following to the creator: " + r, "Error");
                            Application.Exit();
                        }
                    }
                    else
                    {
                        MessageBox.Show("The Tigris Gateway might be experiencing some problems at the moment. Please try again later.");
                    }

                    splash.Hide();
                }
                else
                {
                    UpdateSplashForm(splash, Resources.badly, 3000, true);
                }
            }
            else
            {
                UpdateSplashForm(splash, Resources.badly, 3000, true);
            }
#endif
        

            kryptonNavigator1.Enabled = true;
            groupBoxActions.Enabled = true;
            this.Show();
            this.Opacity = 100;
            lf.Show();
            authenticated = true;

            tableStatistics.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; // Once the window is resized, make the table resize properly            
            listActions.Items.Add("Login.");

            // Default action that must be at the beginning of every citizen
            actions.Add(new ActionToProcess("login"));

            // Add country names to the controls that have those
            comboBattleCountry.Items.Add("Any country");

            foreach (KeyValuePair<int, string> pair in Common.countries)
            {
                comboBattleCountry.Items.Add(pair.Value);
                comboMarketCountry.Items.Add(pair.Value);
                comboMoveCountry.Items.Add(pair.Value);
                comboDestCountry.Items.Add(pair.Value);
                comboRegistrationCountry.Items.Add(pair.Value);
            }

            try
            {
                // Let's load the citizens
                List<string> tempCitizens = LoadFileLines("citizens.txt");
                foreach (string line in tempCitizens)
                {
                    if (line.Contains(','))
                    {
                        // Split the line so we get the email and password
                        string[] parameters = line.Split(',');
                        if (parameters.Count() > 1)
                        {
                            Account citizen = new Account(parameters[0], parameters[1]);
                            int n = tableStatistics.Rows.Add();
                            tableStatistics.Rows[n].Cells[0].Value = citizen.email;
                            citizen_rows[citizen.email] = n;
                            citizens_queue.Enqueue(citizen);
                        }
                    }
                }

                // Let's load the citizens that have to be registered
                List<string> nicks_to_register = LoadFileLines("nicknames_register.txt");
                List<string> emails_to_register = LoadFileLines("emails_register.txt");

                bool error = false;
                if (authenticated)
                {
                    if (nicks_to_register.Count < emails_to_register.Count)
                    {
                        lf.AddBoldedText("WARNING: No enough nicknames for the Register function! Registration will be disabled.");
                        error = true;
                    }
                    if (emails_to_register.Count == 0)
                    {
                        lf.AddBoldedText("WARNING: No emails found in emails_register.txt for the Register function! Registration will be disabled.");
                        error = true;
                    }
                }

                if (error)
                    btnRegisterAll.Enabled = false;
                else
                {
                    int i = 0;
                    foreach (string email in emails_to_register)
                    {
                        string[] parameters = email.Split(',');
                        if (parameters.Count() > 0)
                        {
                            Account citizen_to_register = new Account(parameters[0], parameters[1], nicks_to_register[i]);
                            citizens_to_register_queue.Enqueue(citizen_to_register);
                            int n = tableStatistics.Rows.Add();
                            tableStatistics.Rows[n].Cells[0].Value = citizen_to_register.email;
                            citizen_rows[citizen_to_register.email] = n;
                            logs[n] = "";

                            listEmails.Items.Add(parameters[0]);
                            listPasswords.Items.Add(parameters[1]);
                            listNicknames.Items.Add(nicks_to_register[i]);
                        }
                        ++i;
                    }
                }
            }
            catch (FileNotFoundException exc)
            {
                lf.AddBoldedText("Could not find file " + exc.FileName + "!. Registration will be disabled.");
            }

            if (authenticated)
            {
                List<string> proxiesList = LoadFileLines("proxies.txt");
                proxiesList.Shuffle<string>();
                foreach (string proxy in proxiesList)
                    proxies.Enqueue(proxy);

                if (proxiesList.Count < citizens_queue.Count)
                    lf.AddBoldedText("WARNING: There is not enough proxies to process the regular citizens queue! Please be careful!");
                if (proxies.Count < citizens_to_register_queue.Count)
                    lf.AddBoldedText("WARNING: There is not enough proxies to process the register citizens queue! Please be careful!");
            }

#if LIBCURL
            Curl.GlobalInit((int)CURLinitFlag.CURL_GLOBAL_ALL);
#endif
        
        }    
        
        // Summary:
        //     Starts a citizen thread. The citizen will perform actions that are in the "actions"
        //     list, unless this is a citizen yet to be registered.
        //
        // Parameters:
        //   email:
        //     The citizen's email.
        //
        //   password:
        //     The citizen's password.
        //
        //   proxy:
        //     Proxy in format IP:PORT which will be used by this citizen.
        //
        //   register:
        //     Should be true only if this is a citizen yet to be registered.
        //
        //   nick:
        //     Nick to register this citizen with. Provide only if parameter register is true.
        private void StartCitizen(string email, string password, string proxy, bool register = false, string nick = "")
        {
            Citizen c = new Citizen(email, password, (checkUseProxies.Checked ? proxy : ""), c_payload, checkAutocaptcha.Checked);
            if (!register)
                c.actions = actions;
            else
            {
                List<ActionToProcess> registerActions = new List<ActionToProcess>();
                registerActions.Add(new ActionToProcess("register", comboRegistrationCountry.Text, textReffererNick.Text, radioOnlyFreeBooster.Checked));
                c.actions = registerActions;
                c.Profile.Nick = nick;
            }

            c.Profile.Row = citizen_rows[email];
            c.LogHandle += new Citizen.LogHandler(LogHandler);
            this.TerminateHandle += new TerminateHandler(c.TerminateHandle);
            new Thread(new ThreadStart(c.ProcessActions)).Start();
        }

        // Before we close the form, we should terminate all the threads
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                _terminating = true;
                if(TerminateHandle != null)
                    TerminateHandle(new TerminateEvent());
            }
            catch (NullReferenceException)
            {
            	// All threads have unsubscribed from our event
                // We're just going to ignore it =)
            }
            catch (Exception ex)
            {
                // Shit just got serious
                MessageBox.Show("Error", ex.Message);
            }
        }

        // The user has clicked the Start button, now initiate the Citizens
        private void btnStart_Click(object sender, EventArgs e)
        {
            // Default action that must be at the end of each citizen's action list
            actions.Add(new ActionToProcess("logout"));

            // Start as many citizens at the same time as it was specified by the user
            int started = 0;
            for (int i = 0; i < numThreads.Value; ++i)
            {
                StartCitizenFromMainQueue();
                started++;
            }

#if ANTILEAK
            this.Invoke(new MethodInvoker(delegate { 
                br.Get(Constants.HOST + "/Tigris/gateway.php", "a=" + a + "&b=" + b + "&c=" + c + "&d=start&e=" + started.ToString()); 
            }));
#endif
        }

        // Starts the citizens from the register queue
        private void btnRegisterAll_Click(object sender, EventArgs e)
        {
            int started = 0;
            for (int i = 0; i < numThreads.Value; ++i)
            {
                StartCitizenFromRegisterQueue();
                started++;
            }

#if ANTILEAK
            this.Invoke(new MethodInvoker(delegate
            {
                br.Get(Constants.HOST + "/Tigris/gateway.php", "a=" + a + "&b=" + b + "&c=" + c + "&d=reg&e=" + started.ToString());
            }));
#endif
        }

        private void StartCitizenFromMainQueue()
        {
            if (citizens_queue.Count > 0)
            {
                Account c = citizens_queue.Dequeue();
                StartCitizen(c.email, c.password, (proxies.Count > 0 ? proxies.Dequeue() : "")); // Don't forget to check if the proxy queue is empty, or else we'll get an exception here
            }
        }

        private void StartCitizenFromRegisterQueue()
        {
            if (citizens_to_register_queue.Count > 0)
            {
                Account c = citizens_to_register_queue.Dequeue();
                StartCitizen(c.email, c.password, (proxies.Count > 0 ? proxies.Dequeue() : ""), true, c.nickname);
            }
        }
        
        // This handles all the events sent by the citizen threads
        void LogHandler(Citizen c, LogEvent e)
        {
            if (_terminating) return;

            // First let's get this citizen's row in our statistics table
            int n = c.Profile.Row;

            // Update the table
            this.Invoke((MethodInvoker)delegate
            {
                tableStatistics.Rows[n].Cells["Nick"].Value = c.Profile.Nick;
                tableStatistics.Rows[n].Cells["Level"].Value = c.Profile.Experience;
                tableStatistics.Rows[n].Cells["Wellness"].Value = c.Profile.Wellness;
                tableStatistics.Rows[n].Cells["Strength"].Value = c.Profile.Strength;
                tableStatistics.Rows[n].Cells["Money"].Value = c.Profile.Gold.ToString() + " Gold, " + c.Profile.Currency + " " + c.Profile.CurrencyName;
                tableStatistics.Rows[n].Cells["Location"].Value = c.Profile.Location;
                tableStatistics.Rows[n].Cells["Citizenship"].Value = c.Profile.Citizenship;
                tableStatistics.Rows[n].Cells["Action"].Value = c.Profile.Action;
                tableStatistics.Rows[n].Cells["Influence"].Value = c.Profile.FightInfluence;
                tableStatistics.Rows[n].Cells["Inventory"].Value = c.Profile.InventoryString;
                tableStatistics.Rows[n].Cells["IP"].Value = c.Profile.IP;

                // Make sure we check this is not the first time the citizen
                // logs something, otherwise we'll get an exception by appending
                // data to an unexisting log
                if (e.EventName == "log")
                    if (logs.Keys.Contains(n)) logs[n] += e.Info; else logs[n] = e.Info;    

                // Are we currently watching this citizen's log in the log window?
                if (currentLog == n)
                    if (logs.Keys.Contains(n)) lf.Log(logs[n]);

                // Was this a citizen that has processed all actions? If so, let's run a new citizen
                if (c.Profile.Action == "done" || c.Profile.Action == "error-done")
                {
                    StartCitizenFromMainQueue();
                    c.LogHandle -= new Citizen.LogHandler(LogHandler);
                    this.TerminateHandle -= new TerminateHandler(c.TerminateHandle);
                }

                // Was this a citizen that just registered successfully? If so, let's start a new citizen to register
                else if (c.Profile.Action == "registered")
                {
                    // Incase it was, make sure to save the citizen email and password in a file
                    using (StreamWriter sw = File.AppendText("created_accounts.txt"))
                    {
                        sw.WriteLine(c.Profile.Email + "," + c.Profile.Password);
                    }

                    // Creates a new citizen register thread for the next citizen in our queue
                    StartCitizenFromRegisterQueue();
                    c.LogHandle -= new Citizen.LogHandler(LogHandler);
                    this.TerminateHandle -= new TerminateHandler(c.TerminateHandle);
                }             
                // A citizen that was unable to register? Anyhow, start a new citizen to register
                else if (c.Profile.Action == "nick_unavailable" || c.Profile.Action == "email_unavailable")
                {
                    // Creates a new citizen register thread for the next citizen in our queue
                    StartCitizenFromRegisterQueue();
                    c.LogHandle -= new Citizen.LogHandler(LogHandler);
                    this.TerminateHandle -= new TerminateHandler(c.TerminateHandle);
                }
            });
        }
        
        // Delets the selected action in the queue
        private void btnDeleteAction_Click(object sender, EventArgs e)
        {
            int i = listActions.SelectedIndex;
            // You can't remove login action
            if(i != 0) 
            {
                actions.RemoveAt(i);
                listActions.Items.RemoveAt(i);
            }
        } 
        
        // Make the UI fully resizeable and properly docked when we're on the statistics page
        private void kryptonNavigator1_TabClicked(object sender, ComponentFactory.Krypton.Navigator.KryptonPageEventArgs e)
        {
            bool visibility = e.Index == 5 ? false : true;
            DockStyle style = e.Index == 5 ? DockStyle.Fill : DockStyle.Left;
            groupBoxActions.Visible = visibility;
            kryptonNavigator1.Dock = style;
            tableStatistics.Dock = style;
        }

        // Show the log for selected multi
        private void tableStatistics_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            lf.Show();
            if (logs.ContainsKey(e.RowIndex))
            {
                lf.Log(logs[e.RowIndex]);
                currentLog = e.RowIndex;
            }
        }

        // I could have done this in so much other (better) ways, but meh
        // Each "action to process" is commented, please make sure to read the
        // comments so you understand it. Each function below has (atleast) 2 lines
        // of code; one is for adding the action in the action queue and the other
        // is updating the UI - letting the user know what action he has chosen.

        // Work as an employee in a company
        // 1st parameter: (bool)    - Will we get a job if we don't have one
        // 2nd parameter: (string)  - if 1st param is true,  
        private void btnWork_Click(object sender, EventArgs e)
        {
            actions.Add(new ActionToProcess("workAsEmployee", checkGetAJob.Checked, textJobOffer.Text));
            listActions.Items.Add("Work in the company we're employed in.");
        }

        // Work as a manager in the companies we own
        // 1st parameter: (int)     - the number of companies we'll work in

        // TODO (LOWP): Make it possible to work in, say, the 1st, 4th and the 7th 
        // company instead of working top-to-bottom in a number of companies
        private void btnWorkInOurCompanies_Click(object sender, EventArgs e)
        {
            actions.Add(new ActionToProcess("workAsManager", numCompanies.Value));
            listActions.Items.Add("Work as a manager in the first " + numCompanies.Value.ToString() + " companies.");
        }

        // Train in the given training grounds
        // 1st parameter: (bool)    - Train in "Weights Room" training ground
        // 2nd parameter: (bool)    - Train in "Climbing Center" training ground
        // 3rd parameter: (bool)    - Train in "Shooting Range" training ground
        // 4th parameter: (bool)    - Train in "Special Forces Center" training ground
        private void btnTrain_Click(object sender, EventArgs e)
        {
            actions.Add(new ActionToProcess("train", checkTrain1.Checked, checkTrain2.Checked, checkTrain3.Checked, checkTrain4.Checked));
            listActions.Items.Add("Train.");
        }

        // Fight in the given battle
        // 1st parameter: (string)  - The ID of the battle we'll fight in
        // 2nd parameter: (int)     - The number of enemies we'll kill
        // 3rd parameter: (bool)    - Will we fight barehanded
        // 4th parameter: (bool)    - Will we use rockets if possible
        // 5th parameter: (bool)    - Will we use bazookas if possible
        // 6th parameter: (bool)    - Will we use energy bars if possible and necessary
        // 7th parameter: (bool)    - Will we fight in an RW [true] or a regular battle [false]
        // 8th parameter: (bool)    - Will we fight for defence [true] or resistance [false]
        // 9th parameter: (string)  - Only citizens from this country will get involved in this battle


        // TODO (LOWP): The 3rd parameter is deprecated; it's impossible to fight any more in eRepublik with
        //              bare hands if we have a weapon in our inventory. This parameter should be deleted.
        //              The checkbox in the UI is disabled.
        private void btnFight_Click(object sender, EventArgs e)
        {
            actions.Add(new ActionToProcess("fight", textBattleID.Text, numEnemies.Value, checkBarehanded.Checked, checkRocket.Checked, checkBazookas.Checked, 
                                            checkEnergybars.Checked, checkRW.Checked, radioDefence.Checked, comboBattleCountry.Text));
            listActions.Items.Add("Accounts from " + comboBattleCountry.Text + " will fight in battle ID " + textBattleID.Text + ", killing " + numEnemies.Value.ToString() + 
                                  " enemies" + (checkRW.Checked ? (radioDefence.Checked ? " for defence." : " for resistance.") : "."));
        }
        
        // Buys the specified products
        // 1st parameter: (string)  - The industry name of the product
        // 2nd parameter: (int)     - The quality of the product
        // 3rd parameter: (int)     - The quantity of the product. If we're going to buy the max
        //                            quantity from an offer, this parameter should be 1000000
        private void btnBuy_Click(object sender, EventArgs e)
        {
            actions.Add(new ActionToProcess("buy", comboBuyProduct.Text, numBuyQuality.Value, numBuyQuantity.Value, radioCurrentCountry.Checked));
            listActions.Items.Add("Buy " + numBuyQuantity.Value.ToString() + " of Q" + numBuyQuality.Value.ToString() + " " + comboBuyProduct.Text + " in " + (radioCurrentCountry.Checked ? "current" : "citizenship") + " country.");
        }

        private void btnAllYouCanBuy_Click(object sender, EventArgs e)
        {
            numBuyQuantity.Value = 1000000;
        }

        private void btnAllYouCanSell_Click(object sender, EventArgs e)
        {
            numSellQuantity.Value = 1000000;
        }

        // Adds the specified products to the market for sale
        // 1st parameter: (string)  - The industry name of the product
        // 2nd parameter: (int)     - The quality of the product
        // 3rd parameter: (int)     - The quantity of the product for sale
        // 4th parameter: (string)  - The country in which we're going to sell the product
        // 5th parameter: (string)  - The price for which we're going to sell the product
        
        // TODO (LOWP): Check does the price include the taxes
        private void btnAddOnSale_Click(object sender, EventArgs e)
        {
           actions.Add(new ActionToProcess("sell", comboSellProduct.Text, numSellQuality.Value, numSellQuantity.Value, comboMarketCountry.Text, textPrice.Text));
           listActions.Items.Add("Sell " + numSellQuantity.Value.ToString() + " of Q" + numSellQuality.Value.ToString() + " " + comboSellProduct.Text + " in " + comboMarketCountry.Text + ".");
        }

        // Moves the citizen from one location to another
        // 1st parameter: (string)  - The current country in which the citizen is located (filter)
        // 2nd parameter: (string)  - Destination country to which the citizen will move
        // 3rd parameter: (string)  - Destination region (must be written absolutely properly!)
        private void btnMove_Click(object sender, EventArgs e)
        {
            actions.Add(new ActionToProcess("move", comboMoveCountry.Text, comboDestCountry.Text, textToRegion.Text));
            listActions.Items.Add("Accounts from " + comboMoveCountry.Text + " will move to " + comboDestCountry.Text + ", " + textToRegion.Text + ".");
        }

        // Votes the specified article
        // 1st parameter: (string)  - Article ID
        private void btnVote_Click(object sender, EventArgs e)
        {
            actions.Add(new ActionToProcess("vote", textVoteId.Text));
            listActions.Items.Add("Vote article ID " + textVoteId.Text + ".");
        }

        // Sets a random avatar
        private void btnSetAvatar_Click(object sender, EventArgs e)
        {
            actions.Add(new ActionToProcess("setavatar"));
            listActions.Items.Add("Set random avatar.");
        }

        // Joins a military unit
        // 1st parameter: (string)  - Military unit ID
        private void btnJoinMU_Click(object sender, EventArgs e)
        {
            actions.Add(new ActionToProcess("joinMU", textMUId.Text));
            listActions.Items.Add("Join military unit ID " + textMUId.Text + ".");
        }
        
        private void checkRW_CheckedChanged(object sender, EventArgs e)
        {
            labelBattleId.Text = (checkRW.Checked ? "War ID:" : "Battle ID:");
        }

        // Gets the hardware ID of this PC. The ID is generated by combining
        // CPU ID, the C:\ volume serial number and the motherboard serial number.
        // If you're sending the HWID to the server, make sure to leave the
        // parameter t true as the server expects the two random numbers
        // to be a part of the ID.
        public string GetHWID(bool t = true)
        {
            string id = "";
            ManagementObjectCollection mbsList = null;
            ManagementObjectSearcher mbs = new ManagementObjectSearcher("Select * From Win32_processor");
            mbsList = mbs.Get();
            foreach (ManagementObject mo in mbsList)
            {
                id += mo["ProcessorID"].ToString();
            }

            ManagementObject dsk = new ManagementObject(@"win32_logicaldisk.deviceid=""c:""");
            dsk.Get();
            id += dsk["VolumeSerialNumber"].ToString();

            ManagementObjectSearcher mos = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            ManagementObjectCollection moc = mos.Get();
            foreach (ManagementObject mo in moc)
            {
                id += (string)mo["SerialNumber"];
            }

            Random random = new Random();
            int randomNumber = random.Next(101, 999);

            id = Common.Base64_Encode(id.Replace(" ", "").Trim());

            if (t) return random.Next(1, 99).ToString() + id + random.Next(101, 999);
            return id;
        }

        // Decrypts the payload sent to us by the server. Parameter g is the
        // payload, while the parameter o is the key to parse the payload.
        // Uses the very simple triple XOR encryption.
        private string Decrypt(string g, string o)
        {
            string s = g;
            List<string> payload = new List<string>(s.Split(' '));
            int thisrnd = Convert.ToInt32(payload[0]);
            int rnd = Convert.ToInt32(payload[payload.Count - 1]);
            payload.RemoveAt(0);
            payload.RemoveAt(payload.Count - 1);

            string dec = "";
            int k = 0;
            foreach (string f in payload)
            {
                int i = Convert.ToInt32(f);
                i = i ^ thisrnd ^ rnd ^ (int)o[k];
                char c = (char)i;
                dec += c;
                k++;
                if (k >= o.Length) k = 0;
            }

            return dec;
        }    
                
        // Just a general function
        private List<string> LoadFileLines(string filename)
        {
            List<string> lines = new List<string>();
            if (File.Exists(filename))
            {
                using (StreamReader r = new StreamReader(filename))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }

            return lines;
        }    

        // Another general function for splash
        private void UpdateSplashForm(Form s, Bitmap b, int sleep, bool exit)
        {
            s.BackgroundImage = b;
            s.Update();
            Thread.Sleep(sleep);
            if(exit) Application.Exit();
        }        
    }


}
