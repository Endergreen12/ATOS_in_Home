using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium.Edge;
using System.Collections.ObjectModel;
using OpenQA.Selenium.Interactions;

namespace ATOS_in_Home
{
    public class Train
    {
        public string? type; // 種別
        public string? dest; // 行先
        public DateTime departTime; // 出発時間
    }

    internal class Functions
    {
        public enum AnnounceType
        {
            ArrivalNotice,
            Arrival,
            Departing
        }

        public static EdgeDriver driver;
        public static int carsNum = 1;
        public static int trackNum = 1;
        public static string jrUrl = "https://www.jreast-timetable.jp/cgi-bin/st_search.cgi?mode=0&ekimei=";
        public static string atosSimuUrl = "https://sound-ome201.elffy.net/simulator/simulator_atos/";

        #if DEBUG
        public static DateTime customDate;

        static public void TickCustomDate()
        {
            while (true)
            {
                customDate = customDate.AddSeconds(1);
                Console.WriteLine(customDate.ToString("T"));
                Thread.Sleep(1000);
            }
        }
        #endif

        static public EdgeDriver GenerateDriver()
        {
            // Edge Web Driverからのログ出力を無効化する
            var service = EdgeDriverService.CreateDefaultService();
            #if !DEBUG
            service.HideCommandPromptWindow = true;
            #endif

            // Edgeのウィンドウを非表示
            var options = new EdgeOptions();
            #if !DEBUG
            options.AddArgument("--headless=new");
            #endif

            return new EdgeDriver(service, options);
        }

        static public Train GetNextTrain(string station, string lineName, string direction)
        {
            // JR東日本で次発の時間を取得
            Console.WriteLine("[GetNextTrainTime] JR東日本から次の電車の時間を取得中");

            // 駅名を検索してその駅の指定された路線の時刻表に移動
            driver.Navigate().GoToUrl(jrUrl + station);
            driver.FindElement(By.PartialLinkText(station)).Click();
            var resList = driver.FindElements(By.ClassName("result_02"));

            // 新幹線と在来線がある駅は二つ帰ってくるのでforeach
            foreach(IWebElement res in resList)
            {
                var list = res.FindElements(By.TagName("tr"));

                foreach (IWebElement element in list)
                {
                    if (element.Text.Contains(lineName) && element.Text.Contains(direction))
                    {
                        bool isHoliday = DateTime.Now.DayOfWeek == DayOfWeek.Sunday || DateTime.Now.DayOfWeek == DayOfWeek.Saturday;
                        element.FindElement(By.PartialLinkText(isHoliday ? "休日" : "平日")).Click();
                        break;
                    }
                }
            }

            // 時刻表のテーブルからその時間の電車を取得
            var diaTable = driver.FindElement(By.ClassName("result_03"));
            DateTime dateTime =
            #if DEBUG
                    customDate
            #else
                    DateTime.Now
            #endif
            ;
            int hour = dateTime.Hour;
            ReadOnlyCollection<IWebElement> trains;
            Train nextTrain = new Train();

            while (true)
            {
                if(diaTable.FindElements(By.Id("time_" + hour)).Count == 0) // その時間帯の時刻表自体が存在しない場合
                {
                    hour++;
                    continue;
                }

                trains = diaTable.FindElement(By.Id("time_" + hour)).FindElements(By.ClassName("timetable_time")); // その時間の電車を取得
                // もし今の時間に電車がないか既に全部発車している場合は1時間足してもう一回確認
                if (trains.Count == 0 || int.Parse(
                    String.Concat(Array.FindAll(trains.ElementAt(trains.Count - 1).Text.ToCharArray(), Char.IsDigit))) // 文字列から数字以外の文字を消す
                    < dateTime.Minute && dateTime.Hour == hour)
                    hour++;
                else
                    break;
            }

            // 取得した時間の電車がそれぞれ既に発車している電車でないかを分を比較して確認
            foreach(var train in trains)
            {
                int trainMinute = int.Parse(train.FindElement(By.ClassName("minute")).Text);
                if (trainMinute > dateTime.Minute || hour != dateTime.Hour)
                {
                    nextTrain.departTime =
                    #if DEBUG
                            customDate
                    #else
                            DateTime.Now
                    #endif
                    .AddHours(hour - dateTime.Hour).AddMinutes(trainMinute - dateTime.Minute);
                    Thread.Sleep(500); // 早すぎるとarrow_boxがロードされておらずエラーが出る ロードされた目印を見つけて明示的なwaitをするべきだが全然見つからないのであまりよくないがSleep
                    // 時間の上でホバーすると追加の情報が出るのでホバーさせる
                    new Actions(driver)
                    .MoveToElement(train)
                    .Perform();

                    var arrowBox = train.FindElement(By.ClassName("arrow_box"));
                    var rawType = arrowBox.FindElement(By.ClassName("arrowbox_train")).Text;
                    nextTrain.type = rawType.Substring(rawType.LastIndexOf("：") + 1); // "無印:普通"となっていたりするので種別以前の文字を消す
                    var rawDest = arrowBox.FindElement(By.ClassName("arrowbox_dest")).Text;
                    nextTrain.dest = rawDest.Substring(rawDest.LastIndexOf("：") + 1);

                    break;
                }
            }

            ShowNextTrain(nextTrain.type, nextTrain.departTime, nextTrain.dest);
            Announce(AnnounceType.ArrivalNotice, nextTrain.departTime.Hour, nextTrain.departTime.Minute, nextTrain.dest, nextTrain.type, carsNum, trackNum);

            return nextTrain;
        }

        static public void Announce(AnnounceType type, int hour, int minute, string stationStr, string typeStr, int carsNum, int trackNum)
        {
            Console.WriteLine("[Announce] 放送の準備中");

            if(driver.Url != atosSimuUrl)
                driver.Navigate().GoToUrl(atosSimuUrl);

            // 変数の初期化
            var allList = new SelectElement(driver.FindElement(By.Id("all")));
            var station = new SelectElement(driver.FindElement(By.Id("station")));
            var hourList = new SelectElement(driver.FindElement(By.Id("hour")));
            var minList = new SelectElement(driver.FindElement(By.Id("min")));
            var kindList = new SelectElement(driver.FindElement(By.Id("kind")));
            var carNumberList = new SelectElement(driver.FindElement(By.Id("carnumber")));
            var trackNumberList = new SelectElement(driver.FindElement(By.Id("tracknumber")));

            var greetings = driver.FindElements(By.Name("morning"));
            var yellowLine = driver.FindElements(By.Name("yelowline"));
            var greenCar = driver.FindElements(By.Name("green-car"));
            var announceType = driver.FindElements(By.Name("bro_set"));
            var kakekomiWarn = driver.FindElement(By.Name("kakekomi"));
            var gene = driver.FindElement(By.Id("gene"));
            var inputList = driver.FindElement(By.Id("inputList"));

            Console.WriteLine("[Announce] サイトの準備を待っています...");

            // 準備ができるまで待つ
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
            wait.Until(d => inputList.Enabled);

            Console.WriteLine("[Announce] サイトの準備完了\n[Announce] 設定を入力しています...");

            if(type == AnnounceType.Departing)
            {
                driver.ExecuteScript("InputClear()");
                string[] parts = [trackNum + "番線", "ドアが閉まります", "ご注意下さい"];
                var action = new Actions(driver);
                foreach (string part in parts)
                {
                    allList.SelectByText(part);
                    action.DoubleClick(allList.SelectedOption).Build().Perform();
                }
            }
            else
            {
                // 要素を設定して自動放送開始
                var postScript = "";
                //if(station.c)
                switch (type)
                {
                    case AnnounceType.ArrivalNotice:
                        postScript = "行です";
                        break;

                    case AnnounceType.Arrival:
                        postScript = "行が参ります";
                        break;
                }

                station.SelectByText(stationStr + postScript);
                hourList.SelectByIndex(hour);
                minList.SelectByIndex(minute);
                kindList.SelectByText(typeStr);
                carNumberList.SelectByText(carsNum + "両です");
                trackNumberList.SelectByText(trackNum + "番線");
                yellowLine[1].Click();
                greenCar[3].Click();
                if (kakekomiWarn.Selected)
                    kakekomiWarn.Click();
                if (
                #if DEBUG
                            customDate
                #else
                            DateTime.Now
                #endif
                .Hour <= 9)
                    greetings[1].Click();

                if (type != AnnounceType.ArrivalNotice)
                {
                    int typeNumber = 0;
                    switch (type)
                    {
                        case AnnounceType.Arrival:
                            typeNumber = 1; // 接近放送
                            break;

                        case AnnounceType.Departing:
                            typeNumber = 4; // 出発放送
                            break;
                    }
                    announceType[typeNumber].Click();
                }

                gene.Click();
            }

            Console.WriteLine("[Announce] 放送を開始");

            inputList.Click();

            // 放送が終わるまで待つ
            wait.Until(d => inputList.Enabled);
        }

        public static void ShowNextTrain(string type, DateTime nextTrainTime, string station)
        {
            Console.WriteLine(type + "\t" + nextTrainTime.ToString("t") + "\t" + station);
        }
    }
}
