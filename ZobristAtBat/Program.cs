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

        static void Main(string[] args)
        {
            string month = DateTime.Now.ToString("MM");
            string date = DateTime.Now.ToString("dd");
            string year = DateTime.Now.ToString("yyyy");            
            string currentDate = DateTime.Today.Date.ToShortDateString();
            string tweet = "";

            string batterTeamID = GetPlayerTeamID(PLAYER_ID);
            
            //assemble the root directory on mlb.com based on today's date
            string todayRootURL = "http://gd2.mlb.com/components/game/mlb/year_" + year + "/month_" + month + "/day_" + date + "/";

            try
            {
                //this XML file has details on today's games, find the game id where batter's team is playing
                XmlDocument documentScoreboard = new XmlDocument();
                documentScoreboard.Load(todayRootURL + "/miniscoreboard.xml");
                XmlElement elementScoreboard = documentScoreboard.DocumentElement;
                XmlNode gameScoreboardNode = elementScoreboard.SelectSingleNode("game[@away_name_abbrev='" + batterTeamID + "' or @home_name_abbrev='" + batterTeamID + "']");
                XmlAttributeCollection gameScoreboardAttributes = gameScoreboardNode.Attributes;

                string home_team_name = gameScoreboardAttributes["home_team_name"].Value;
                string away_team_name = gameScoreboardAttributes["away_team_name"].Value;
                string venue = gameScoreboardAttributes["venue"].Value;
                string home_time = gameScoreboardAttributes["home_time"].Value;
                string home_ampm = gameScoreboardAttributes["home_ampm"].Value;
                string home_time_zone = gameScoreboardAttributes["home_time_zone"].Value;
                string home_win = gameScoreboardAttributes["home_win"].Value;
                string home_loss = gameScoreboardAttributes["home_loss"].Value;
                string away_win = gameScoreboardAttributes["away_win"].Value;
                string away_loss = gameScoreboardAttributes["away_loss"].Value;

                string homeStats = home_team_name + " (" + home_win + "-" + home_loss + ")";
                string awayStats = away_team_name + " (" + away_win + "-" + away_loss + ")";

                SendTweet(awayStats + " visit " + homeStats + " at " + venue + ", game time " + home_time + " " + home_ampm + " " + home_time_zone);

                string game_data_directory = todayRootURL + "gid_" + gameScoreboardAttributes["gameday_link"].Value;


                string gameid = gameScoreboardAttributes["id"].Value;
                DateTime gametime = Convert.ToDateTime(gameScoreboardAttributes["time_date"].Value + gameScoreboardAttributes["ampm"].Value);
                //sleep until gametime
                TimeSpan idletime = gametime.Subtract(DateTime.Now.AddMinutes(5));
                if (idletime.Ticks > 0)
                    Thread.Sleep(idletime);


                XmlDocument documentPlays = new XmlDocument();
                documentPlays.Load(game_data_directory + "/plays.xml");

                string currentGameStatus = documentPlays.SelectSingleNode("game").Attributes["status_ind"].Value;

                string gameAtBatCount = "";
                string gamePreviousAtBatCount = "";
                string batterAtBat = "";
                string batterOnDeck = "";
                string batterInHole = "";
                string batterPlayDesc = "";
                int BatterEventAtBat = 0;
                bool getBatterEvent = false;

                //tracks how many hits we make to the XML file
                int counter = 0;

                //used for player name
                XmlDocument documentBatter = new XmlDocument();
                documentBatter.Load(game_data_directory + "/batters/" + PLAYER_ID + ".xml");
                string playerName = documentBatter.SelectSingleNode("Player").Attributes["first_name"].Value
                    + " " + documentBatter.SelectSingleNode("Player").Attributes["last_name"].Value;

                int batterAtBatCount = documentBatter.SelectSingleNode("Player/atbats").ChildNodes.Count;
                if (batterAtBatCount == 0)
                {
                    batterAtBatCount = 1;
                }
                else
                {
                    batterAtBatCount += 1;
                }

                //used to get at bat event description
                XmlDocument documentBatterEvent = new XmlDocument();

                while (currentGameStatus != "O" && currentGameStatus != "F")
                {
                    if (currentGameStatus == "DR")
                    {
                        tweet = currentDate + " - The game has been postponed.";
                        SendTweet(tweet);
                        break;
                    }

                    if (currentGameStatus == "PR")
                    {
                        tweet = currentDate + " - The game has been delayed.";
                        SendTweet(tweet);
                        while (currentGameStatus == "PR")
                        {
                            documentPlays.Load(game_data_directory + "/plays.xml");
                            currentGameStatus = documentPlays.SelectSingleNode("game").Attributes["status_ind"].Value;
                            Thread.Sleep(60000);
                        }
                    }

                    if (gamePreviousAtBatCount != gameAtBatCount)
                    {
                        batterAtBat = documentPlays.SelectSingleNode("game/players/batter").Attributes["pid"].Value;
                        batterOnDeck = documentPlays.SelectSingleNode("game/players/deck").Attributes["pid"].Value;
                        batterInHole = documentPlays.SelectSingleNode("game/players/hole").Attributes["pid"].Value;

                        if (getBatterEvent == true)
                        {
                            documentBatterEvent.Load(game_data_directory + "/game_events.xml");
                            batterPlayDesc = documentBatterEvent.SelectSingleNode("//atbat[@num='" + (BatterEventAtBat) + "']").Attributes["des"].Value;
                            SendTweet(batterPlayDesc.Split(new Char[] { '.' }).GetValue(0).ToString());
                            getBatterEvent = false;
                        }

                        if (batterAtBat == PLAYER_ID)
                        {
                            //batterAtBatCount = documentPlays.SelectSingleNode("game/players/batter").ChildNodes.Count;
                            SendTweet(currentDate + " " + playerName + " is at bat for the " + GetAtBatString(batterAtBatCount) + " time!");

                            getBatterEvent = true;
                            BatterEventAtBat = Convert.ToInt32(gameAtBatCount);
                            batterAtBatCount += 1;
                        }

                        if (batterOnDeck == PLAYER_ID)
                        {
                            SendTweet(currentDate + " " + playerName + " is on deck for the " + GetAtBatString(batterAtBatCount) + " time.");
                        }

                        if (batterInHole == PLAYER_ID)
                        {
                            SendTweet(currentDate + " " + playerName + " is in the hole for the " + GetAtBatString(batterAtBatCount) + " time.");
                        }
                        Console.WriteLine("Game at bat number: " + gameAtBatCount);
                        Console.WriteLine("XML file reads: " + counter);
                        Console.WriteLine("--------------------------------");
                        counter = 0;
                    }

                    documentPlays.Load(game_data_directory + "/plays.xml");
                    currentGameStatus = documentPlays.SelectSingleNode("game").Attributes["status_ind"].Value;
                    gamePreviousAtBatCount = gameAtBatCount;
                    
                    gameAtBatCount = documentPlays.SelectSingleNode("game/atbat").Attributes["num"].Value;

                    Thread.Sleep(10000);
                    counter += 1;
                }


                //game summary
                if (currentGameStatus == "F" || currentGameStatus == "O")
                {
                    XmlDocument documentBoxScore = new XmlDocument();
                    documentBoxScore.Load(game_data_directory + "/boxscore.xml");

                    XmlNode batterNode = documentBoxScore.SelectSingleNode("boxscore/batting/batter[@id='" + PLAYER_ID + "']");
                    string batterName = batterNode.Attributes["name_display_first_last"].Value;

                    if (batterNode != null)
                    {
                        XmlAttributeCollection batterStats = batterNode.Attributes;

                        int hits = Convert.ToInt32(batterStats["h"].Value);
                        int ab = Convert.ToInt32(batterStats["ab"].Value);
                        int r = Convert.ToInt32(batterStats["r"].Value);
                        int hr = Convert.ToInt32(batterStats["hr"].Value);
                        int rbi = Convert.ToInt32(batterStats["rbi"].Value);
                        int sb = Convert.ToInt32(batterStats["sb"].Value);

                        tweet = "That's the ballgame! " + batterName + " went " + hits + "/" + ab;

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
                                tweet = tweet + hr + " HR ";
                            }

                            if (rbi > 0)
                            {
                                tweet = tweet + rbi + " RBI ";
                            }

                            if (sb > 0)
                            {
                                tweet = tweet + sb + " SB ";
                            }
                        }
                    }
                    else
                    {
                        tweet = batterName + " did not play in today's game.";
                    }
                    SendTweet(tweet);
                }

                Console.WriteLine("Game has ended.");

                // Keep the console window open in debug mode.
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was an error: " + ex.Message);
            }
        }

        private static string GetPlayerTeamID(string playerID)
        {
            //batters XML file
            string batterURL = "http://gd2.mlb.com/components/game/mlb/year_" + DateTime.Now.ToString("yyyy") + "/batters/" + PLAYER_ID + ".xml";
            XmlDocument doc = new XmlDocument();
            doc.Load(batterURL);
            XmlElement root = doc.DocumentElement;

            string batterLastGameID = root.SelectSingleNode("@game_id").InnerText;
            string[] batterLastGameArray = batterLastGameID.Split(new Char[] { '/' });

            batterLastGameID = batterLastGameID.Replace("/", "_");
            batterLastGameID = batterLastGameID.Replace("-", "_");
            batterLastGameID = "gid_" + batterLastGameID;

            string lastGameURL = "http://gd2.mlb.com/components/game/mlb/year_" + batterLastGameArray[0] + "/month_" + batterLastGameArray[1] + "/day_" + batterLastGameArray[2] + "/" + batterLastGameID + "/players.xml";
            doc.Load(lastGameURL);
            root = doc.DocumentElement;
            XmlAttributeCollection batterTeamAttr = root.SelectSingleNode("team/player[@id='" + PLAYER_ID + "']").ParentNode.Attributes;

            return batterTeamAttr["id"].Value;

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
