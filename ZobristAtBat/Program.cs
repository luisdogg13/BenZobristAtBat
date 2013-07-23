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

        private const string ZOBRIST_ID = "450314";

        private const int IN_THE_HOLE = 1;
        private const int ON_DECK = 2;
        private const int AT_BAT = 3;
        private const int PICKING_ASS_IN_DUGOUT = 4;

        static void Main(string[] args)
        {
            string month = DateTime.Now.ToString("MM");
            string date = DateTime.Now.ToString("dd");
            date = "22";
            string year = DateTime.Now.ToString("yyyy");
            
            //assemble the root directory on mlb.com based on today's date
            string todaysURL = "http://gd2.mlb.com/components/game/mlb/year_" + year + "/month_" + month + "/day_" + date + "/";


            //this XML file has details on today's games, find the game id where Tampa Bay is playing
            String URLString = todaysURL + "epg.xml";
            Console.WriteLine(URLString);
            XmlTextReader reader = new XmlTextReader(URLString);

            XmlDocument doc = new XmlDocument();
            doc.Load(URLString);

            string gameid = "";
           
            XmlElement root = doc.DocumentElement;

            gameid = root.SelectSingleNode("game[@away_name_abbrev='TB' or @home_name_abbrev='TB']/@id").InnerText;
            DateTime gametime = Convert.ToDateTime(root.SelectSingleNode("game[@id='" + gameid + "']/@start").InnerText);
            Console.WriteLine("The next Tampa Bay Rays game is at: " + gametime);

            TimeSpan idletime = gametime.Subtract(DateTime.Now.AddMinutes(5));  
            if(idletime.Ticks > 0)
                Thread.Sleep(idletime);


            //this will open the plays.xml file (this xml file has the real time at bat data)
            if (gameid != "")
            {
                gameid = gameid.Replace("/", "_");
                gameid = gameid.Replace("-", "_");
                gameid = "gid_" + gameid;

                URLString = todaysURL + gameid + "/plays.xml";
                //debug
                //URLString = "http://gd2.mlb.com/components/game/mlb/year_2013/month_07/day_19/gid_2013_07_19_tbamlb_tormlb_1/plays.xml";
                doc = new XmlDocument();
                doc.Load(URLString);
                root = doc.DocumentElement;
            }

            string tweet = "";
            string curBatterId = "";
            string curGameStatus = "";
            string curDateStr = DateTime.Today.Date.ToShortDateString();
            int curStatus = PICKING_ASS_IN_DUGOUT;
            int curAtBat = 0;
            curGameStatus = root.SelectSingleNode("@status_ind").InnerText;

            //if the game isn't final
            while (curGameStatus != "F" && curGameStatus != "O")
            {
                tweet = "";
                curBatterId = root.SelectSingleNode("players/batter/@pid").InnerText;
                if (curBatterId == ZOBRIST_ID)
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
                    if (curBatterId == ZOBRIST_ID)
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
                        if (curBatterId == ZOBRIST_ID)
                        {
                            if (curStatus != IN_THE_HOLE)
                            {
                                tweet = curDateStr + " - Ben Zobrist is in the hole for his " + GetAtBatString(curAtBat) + " at bat!";
                                curStatus = IN_THE_HOLE;
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

                curGameStatus = root.SelectSingleNode("@status_ind").InnerText;


            }

            if (curGameStatus == "F") 
            {
                URLString = todaysURL + gameid + "/boxscore.xml";
                doc.Load(URLString);
                root = doc.DocumentElement;

                XmlNode zobieNode = root.SelectSingleNode("batting/batter[@id='" + ZOBRIST_ID + "']");

                if (zobieNode != null)
                {
                    XmlAttributeCollection zobieStats = zobieNode.Attributes;
                  
                    int hits = Convert.ToInt32(zobieStats.GetNamedItem("h").Value);
                    int ab = Convert.ToInt32(zobieStats.GetNamedItem("ab").Value);
                    int r = Convert.ToInt32(zobieStats.GetNamedItem("r").Value);
                    int hr = Convert.ToInt32(zobieStats.GetNamedItem("hr").Value);
                    int rbi = Convert.ToInt32(zobieStats.GetNamedItem("rbi").Value);
                    int sb = Convert.ToInt32(zobieStats.GetNamedItem("sb").Value);

                    tweet = "That's ballgame! Ben Zobrist went " + hits + "/" + ab;

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

                        if (hr < 0)
                        {
                            tweet = tweet + r + " HR ";
                        }

                        if (rbi < 0)
                        {
                            tweet = tweet + r + " RBI ";
                        }

                        if (sb < 0)
                        {
                            tweet = tweet + r + " SB ";
                        }
                    }
                    Console.WriteLine(tweet);
                    SendTweet(tweet);
                }                
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

        private static void SendTweet(string tweet)
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
    }
}
