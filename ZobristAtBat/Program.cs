using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Configuration;
using System.Threading;
using TweetSharp;


namespace ZobristAtBat
{
    class Program
    {
        #region ConsumerKey & ConsumerSecret
        private static string ConsumerSecret
        {
            get { return ConfigurationManager.AppSettings["ConsumerSecret"]; }
        }
        private static string ConsumerKey
        {
            get { return ConfigurationManager.AppSettings["ConsumerKey"]; }
        }
        private static string AccessToken
        {
            get { return ConfigurationManager.AppSettings["AccessToken"]; }
        }
        private static string AccessTokenSecret
        {
            get { return ConfigurationManager.AppSettings["AccessTokenSecret"]; }
        }
        #endregion

        private const bool DEBUG = false;

        //Zobrist: 450314
        private const string PLAYER_ID = "450314";

        private const int IN_THE_HOLE = 1;
        private const int ON_DECK = 2;
        private const int AT_BAT = 3;
        private const int PICKING_ASS_IN_DUGOUT = 4;

        static void Main(string[] args)
        {
            string month = DateTime.Now.ToString("MM");
            string date = DateTime.Now.ToString("dd");
            string year = DateTime.Now.ToString("yyyy");
            
            //assemble the root directory on mlb.com based on today's date
            string todaysURL = "http://gd2.mlb.com/components/game/mlb/year_" + year + "/month_" + month + "/day_" + date + "/";

            //this XML file has details on today's games, find the game id where Tampa Bay is playing
            string TodaysGameURL =  "";
            string BatterURL = "";
            String URLString = todaysURL + "epg.xml";
            XmlTextReader reader = new XmlTextReader(URLString);

            XmlDocument doc = new XmlDocument();
            doc.Load(URLString);                       
            XmlElement root = doc.DocumentElement;

            XmlNode gameNode = root.SelectSingleNode("game[@away_name_abbrev='TB' or @home_name_abbrev='TB']");
            XmlAttributeCollection gameDetails = gameNode.Attributes;

            string gameid = gameDetails.GetNamedItem("id").Value;

            //this will open the plays.xml file (this xml file has the real time at bat data)
            if (gameid != "")
            {
                gameid = gameid.Replace("/", "_");
                gameid = gameid.Replace("-", "_");
                gameid = "gid_" + gameid;

                TodaysGameURL = todaysURL + gameid;
                URLString = TodaysGameURL + "/plays.xml";
                BatterURL = TodaysGameURL + "/batters/" + PLAYER_ID + ".xml";
                //debug
                //URLString = "http://gd2.mlb.com/components/game/mlb/year_2013/month_07/day_19/gid_2013_07_19_tbamlb_tormlb_1/plays.xml";
                doc = new XmlDocument();
                doc.Load(URLString);
                root = doc.DocumentElement;
            }

            string tweet = "";
            string curBatterId = "";
            string curGameStatus = "";
            string lastGameStatus = "";
            string curDateStr = DateTime.Today.Date.ToShortDateString();
            int curStatus = PICKING_ASS_IN_DUGOUT;
            int curAtBat = 1;
            curGameStatus = root.SelectSingleNode("@status_ind").InnerText;

            if (curGameStatus != "O" && curGameStatus != "F" && curGameStatus != "DR")
            {
                DateTime gametime = Convert.ToDateTime(gameDetails.GetNamedItem("start").Value);

                string home_team_name = gameDetails.GetNamedItem("home_team_name").Value;
                string away_team_name = gameDetails.GetNamedItem("away_team_name").Value;
                string venue = gameDetails.GetNamedItem("venue").Value;
                string home_time = gameDetails.GetNamedItem("home_time").Value;
                string home_ampm = gameDetails.GetNamedItem("home_ampm").Value;
                string home_time_zone = gameDetails.GetNamedItem("home_time_zone").Value;
                string home_win = gameDetails.GetNamedItem("home_win").Value;
                string home_loss = gameDetails.GetNamedItem("home_loss").Value;
                string away_win = gameDetails.GetNamedItem("away_win").Value;
                string away_loss = gameDetails.GetNamedItem("away_loss").Value;

                string homeStats = home_team_name + " (" + home_win + "-" + home_loss + ") ";
                string awayStats = away_team_name + " (" + away_win + "-" + away_loss + ") ";

                Console.WriteLine("The next Tampa Bay Rays game is at: " + gametime);
                SendTweet(awayStats + " visit " + homeStats + " at " + venue + ", game time " + home_time + " " + home_ampm + " " + home_time_zone);


                TimeSpan idletime = gametime.Subtract(DateTime.Now.AddMinutes(5));
                if (idletime.Ticks > 0)
                    Thread.Sleep(idletime);
            }


           
            //if the game isn't final
            while (curGameStatus != "O" && curGameStatus != "F" && curGameStatus != "DR")
            {
                tweet = "";
                if (curGameStatus == "PR" && lastGameStatus != "PR")
                {
                    tweet = curDateStr + " - The game has been delayed.";
                }
                else if (curGameStatus == "I")
                {
                    
                    curBatterId = root.SelectSingleNode("players/batter/@pid").InnerText;
                    if (curBatterId == PLAYER_ID)
                    {
                        if (curStatus != AT_BAT)
                        {
                            tweet = curDateStr + " - Ben Zobrist is at bat for the " + GetAtBatString(curAtBat) + " time!";
                            curStatus = AT_BAT;
                            curAtBat += 1;
                        }
                    }
                    else
                    {
                        //curBatterId is now used for on deck
                        curBatterId = root.SelectSingleNode("players/deck/@pid").InnerText;
                        if (curBatterId == PLAYER_ID)
                        {
                            if (curStatus != ON_DECK)
                            {
                                tweet = curDateStr + " - Ben Zobrist is on deck for his " + GetAtBatString(curAtBat) + " at bat!";
                                curStatus = ON_DECK;
                            }
                        }
                        else
                        {
                            //curBatterId is now used for in the hole
                            curBatterId = root.SelectSingleNode("players/hole/@pid").InnerText;
                            if (curBatterId == PLAYER_ID)
                            {
                                if (curStatus != IN_THE_HOLE)
                                {
                                    tweet = curDateStr + " - Ben Zobrist is in the hole for his " + GetAtBatString(curAtBat) + " at bat!";
                                    curStatus = IN_THE_HOLE;
                                }
                            }
                            else
                            {
                                if (curStatus == AT_BAT)
                                {
                                    //get the last atbat stats
                                    tweet = curDateStr = " - Ben Zobrist had a " + GetAtBatOutcomeString(curAtBat, BatterURL) + " in his " + GetAtBatString(curAtBat) + " at bat!";
                                    curStatus = PICKING_ASS_IN_DUGOUT;
                                }
                            }
                        }
                    }
                }
                    if (!string.IsNullOrEmpty(tweet))
                    {
                        SendTweet(tweet);
                    }

                    doc = new XmlDocument();
                    doc.Load(URLString);
                    root = doc.DocumentElement;
                    lastGameStatus = curGameStatus;
                    curGameStatus = root.SelectSingleNode("@status_ind").InnerText;
                
            }

            if (curGameStatus == "F" || curGameStatus == "O") 
            {
                URLString = todaysURL + gameid + "/boxscore.xml";
                doc.Load(URLString);
                root = doc.DocumentElement;

                XmlNode zobieNode = root.SelectSingleNode("batting/batter[@id='" + PLAYER_ID + "']");

                if (zobieNode != null)
                {
                    XmlAttributeCollection zobieStats = zobieNode.Attributes;
                  
                    int hits = Convert.ToInt32(zobieStats.GetNamedItem("h").Value);
                    int ab = Convert.ToInt32(zobieStats.GetNamedItem("ab").Value);
                    int r = Convert.ToInt32(zobieStats.GetNamedItem("r").Value);
                    int hr = Convert.ToInt32(zobieStats.GetNamedItem("hr").Value);
                    int rbi = Convert.ToInt32(zobieStats.GetNamedItem("rbi").Value);
                    int sb = Convert.ToInt32(zobieStats.GetNamedItem("sb").Value);

                    tweet = "That's the ballgame! Ben Zobrist went " + hits + "/" + ab;

                    if (r > 0 || hr > 0 || rbi > 0 || sb > 0)
                    {
                        tweet = tweet + " and collected ";

                        if (r == 1)
                        {
                            tweet = tweet + r + " run ";
                        }
                        else if (r > 1)
                        {
                            tweet = tweet + r + " runs ";
                        }

                        if (hr > 0)
                        {
                            tweet = tweet + r + " HR ";
                        }

                        if (rbi > 0)
                        {
                            tweet = tweet + r + " RBI ";
                        }

                        if (sb > 0)
                        {
                            tweet = tweet + r + " SB ";
                        }
                    }
                    SendTweet(tweet);
                }                
            }
            else if (curGameStatus == "DR")
            {
                tweet = curDateStr + " - The game has been postponed!";
                SendTweet(tweet);
            }
            Console.WriteLine("Game has ended.");

            // Keep the console window open in debug mode.
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }


        private static string GetAtBatString(int bat)
        {
            string ret = bat + "";
         
            int last = bat % 10;
            switch (last)
            {
                case 1:
                    ret += "st";
                    break;
                case 2:
                    ret += "nd";
                    break;
                case 3:
                    ret += "rd";
                    break;
                default:
                    ret += "th";
                    break;
            }
            return ret;
        }

        private static string GetAtBatOutcomeString(int curAtBat, string xml)
        {
            string outcome = "";
            string ret = "";
            XmlDocument batterDoc = new XmlDocument();
            batterDoc.Load(xml);
            XmlElement root = batterDoc.DocumentElement;
            XmlNodeList zobieNodes = root.SelectNodes("atbats/ab/@event");
            if (zobieNodes.Count >= curAtBat)
            {
                outcome = zobieNodes[curAtBat - 1].InnerText;
            }

            return outcome;
        }

        private static void SendTweet(string tweet)
        {
            if (DEBUG == false)
            {
                TwitterClientInfo twitterClientInfo = new TwitterClientInfo();
                twitterClientInfo.ConsumerKey = ConsumerKey; //Read ConsumerKey out of the app.config
                twitterClientInfo.ConsumerSecret = ConsumerSecret; //Read the ConsumerSecret out the app.config
                TwitterService twitterService = new TwitterService(twitterClientInfo);

                SendTweetOptions tweetOps = new SendTweetOptions() { Status = tweet };
                twitterService.AuthenticateWith(AccessToken, AccessTokenSecret);
                twitterService.SendTweet(tweetOps);
                var responseText = twitterService.Response.Response;
                Console.WriteLine("Tweet has been sent: " + tweet);
            }
            else
            {
                Console.WriteLine(tweet);
            }
        }
    }
}
