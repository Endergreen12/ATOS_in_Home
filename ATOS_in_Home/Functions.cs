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
using System.Runtime.InteropServices;
using OpenQA.Selenium.Chrome;

namespace ATOS_in_Home
{
    public class Train
    {
        public string type = "";                // 種別
        public string dest = "";                // 行先
        public string nextSt = "";              // 次駅
        public string lineName = "";            // 路線
        public DateTime departTime;             // 出発時間
        public bool hasGreenCar = false;        // グリーン車がついているか
        public bool isFirst = false;            // 始発かどうか
    }

    internal class Functions
    {
        public enum AnnounceType
        {
            Invalid = -1,
            ArrivalNotice,
            Arrival,
            Departing = 4
        }

        public enum GreenCarType
        {
            HasGreenCar,
            HasGreenCarOld,
            NoGreenCar,
            NoAnnounce
        }

        public enum GreetingsType
        {
            Normal,
            GoodMorning,
            Old
        }

        public static EdgeDriver? driver;
        public static int carsNum = 1;
        public static int trackNum = 1;
        public static string jrUrl = "https://www.jreast-timetable.jp/cgi-bin/st_search.cgi?mode=0&ekimei=";
        public static string atosSimuUrl = "https://sound-ome201.elffy.net/simulator/simulator_atos/";
        public static string atosSimuMaleUrl = "https://sound-ome201.elffy.net/simulator/simulator_atos_tsuda/";
        public static string firstMark = "●";
        public static string atosWindow = "";
        public static string[] lineWithGreenCar = ["東海道線", "横須賀線", "総武快速線", "宇都宮線", "高崎線", "湘南新宿ライン", "上野東京ライン", "常磐線"];

        public static DateTime customDate;
        public static bool showTime = false;
        public static bool useCustomDate = false;
        public static bool maleVoice = false;

        static public void TickCustomDate()
        {
            while (true)
            {
                customDate = useCustomDate ? customDate.AddSeconds(1) : DateTime.Now;

                if(showTime)
                    Console.WriteLine("[Time] " + customDate.ToString("T"));

                Thread.Sleep(1000);
            }
        }

        public static int ReadNumber(string line)
        {
            while (true)
            {
                int answer;
                Console.WriteLine(line);
                if (int.TryParse(Console.ReadLine(), out answer))
                    return answer;
                else
                    Console.WriteLine("数字に変換できませんでした。もう一度入力してください。");
            }
        }


        static public EdgeDriver GenerateDriver()
        {
            // Web Driverからのログ出力を無効化する
            var service = EdgeDriverService.CreateDefaultService();
#if !DEBUG
            service.HideCommandPromptWindow = true;
#endif

            // ブラウザのウィンドウを非表示
            var options = new EdgeOptions();
#if !DEBUG
            options.AddArgument("--headless=new");

            options.AddArgument("--no-sandbox");
#endif

            return new EdgeDriver(service, options);
        }

        static public Train GetNextTrain(string station, string lineName, string direction)
        {
            // JR東日本で次発の時間を取得
            Console.WriteLine("[GetNextTrain] JR東日本から次の電車の時間を取得中");

            if (driver == null)
            {
                Console.WriteLine("[GetNextTrain] 不明なエラー(null)");
                Console.ReadKey();
                Environment.Exit(0);
            }

            driver.SwitchTo().NewWindow(WindowType.Tab);

            // 駅名を検索してその駅の指定された路線の時刻表に移動
            driver.Navigate().GoToUrl(jrUrl + station.Remove(station.IndexOf("(")));

            if(station == "" || driver.FindElements(By.PartialLinkText(station)).Count == 0)
            {
                Console.WriteLine("指定された駅が存在しませんでした。何かキーを押してプログラムを終了します。");
                Console.ReadKey();
                Environment.Exit(0);
            }

            driver.FindElement(By.LinkText(station)).Click();
            var resList = driver.FindElements(By.ClassName("result_02"));

            bool found = false;
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
                        found = true;
                        break;
                    }
                }
            }

            if(!found)
            {
                Console.WriteLine("指定された方面が見つかりませんでした。何かキーを押して終了します。");
                Console.ReadKey();
                Environment.Exit(0);
            }

            // 時刻表のテーブルからその時間の電車を取得
            var diaTable = driver.FindElement(By.ClassName("result_03"));
            DateTime dateTime = customDate;
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
                    <= dateTime.Minute && dateTime.Hour == hour)
                    hour++;
                else
                    break;
            }

            // 取得した時間の電車がそれぞれ既に発車している電車でないかを分を比較して確認
            foreach(var train in trains)
            {
                int trainMinute = int.Parse(train.FindElement(By.ClassName("minute")).Text);
                // 次発の電車を見つけたらnextTrainにその電車を登録する
                if (trainMinute > dateTime.Minute || hour != dateTime.Hour) // その時間より1時間上だった場合は分を確認する必要がないのでそのまま登録する
                {
                    if(train.FindElements(By.ClassName("mark_etc")).Count != 0)
                        nextTrain.isFirst = train.FindElement(By.ClassName("mark_etc")).Text.Contains(firstMark);

                    nextTrain.departTime = dateTime.AddHours(hour - dateTime.Hour).AddMinutes(trainMinute - dateTime.Minute).AddSeconds(-dateTime.Second); // DateTimeは後からいじれないというクソ仕様なので無理やり新しく設定する

                    while (true) // まれにarrow_boxが出ないことがあるので何回も試行する
                    {
                        // 時間の上でホバーすると追加の情報が出るのでホバーさせる
                        new Actions(driver).MoveToElement(train).Perform();

                        if (train.FindElements(By.ClassName("arrow_box")).Count != 0)
                            break;

                        Console.WriteLine("[GetNextTrain] arrow_box 再試行中");
                        Thread.Sleep(100);
                    }

                    var arrowBox = train.FindElement(By.ClassName("arrow_box"));
                    var rawType = arrowBox.FindElement(By.ClassName("arrowbox_train")).Text;
                    var typeSubstring = rawType.Substring(rawType.IndexOf("：") + 1);
                    if (typeSubstring.Contains("\r\n"))
                        typeSubstring = typeSubstring.Remove(typeSubstring.IndexOf("\r\n"));
                    nextTrain.type = typeSubstring; // "無印:普通"となっていたりするので種別以外の文字を消す

                    var rawDest = arrowBox.FindElement(By.ClassName("arrowbox_dest")).Text;
                    var destSubstring = rawDest.Substring(rawDest.IndexOf("：") + 1);
                    if (destSubstring.Contains("\r\n"))
                        destSubstring = destSubstring.Remove(destSubstring.IndexOf("\r\n"));
                    nextTrain.dest = destSubstring;

                    train.Click();

                    nextTrain.hasGreenCar = driver.FindElement(By.Id("tbl_train")).Text.Contains("グリーン車");
                    driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
                    var stations = driver.FindElement(By.Id("tbl_train_label1")).FindElements(By.TagName("th"));
                    IWebElement? nextStationElem = null;

                    station = station.Substring(0, station.IndexOf("("));
                    foreach (var stationName in stations) // 次駅を探す
                        if(stationName.Text == station)
                        {
                            nextStationElem = stations.ElementAt(stations.IndexOf(stationName) + 1);
                            break;
                        }

                    if(nextStationElem == null)
                    {
                        Console.WriteLine("[GetNextTrain] 不明なエラーが発生しました(null)");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }

                    nextTrain.nextSt = nextStationElem.Text;

                    nextTrain.lineName = lineName;
                    break;
                }
            }

            driver.Close();
            driver.SwitchTo().Window(atosWindow);

            ShowNextTrain(nextTrain);
            Announce(AnnounceType.ArrivalNotice, nextTrain);

            return nextTrain;
        }

        static public void Announce(AnnounceType type, Train train, bool waitRequired = false)
        {
            if (type == AnnounceType.Invalid)
                return;

            Console.WriteLine("[Announce] 放送の準備中");

            if (driver == null)
            {
                Console.WriteLine("[Announce] 不明なエラー(null)");
                Console.ReadKey();
                Environment.Exit(0);
            }

            if (driver.Url != atosSimuUrl)
                driver.Navigate().GoToUrl(atosSimuUrl);

            // 変数の初期化
            var allList = new SelectElement(driver.FindElement(By.Id("all")));                              // すべてのパーツ
            var station = new SelectElement(driver.FindElement(By.Id("station")));                          // 行先
            var hourList = new SelectElement(driver.FindElement(By.Id("hour")));                            // 時刻(時)
            var minList = new SelectElement(driver.FindElement(By.Id("min")));                              // 時刻(分)
            var kindList = new SelectElement(driver.FindElement(By.Id("kind")));                            // 種別
            var carNumberList = new SelectElement(driver.FindElement(By.Id("carnumber")));                  // 両数
            var trackNumberList = new SelectElement(driver.FindElement(By.Id("tracknumber")));              // 番線
            var nextStList = new SelectElement(driver.FindElement(By.Id("nextst")));                        // 次駅
 
            var greetings = driver.FindElements(By.Name("morning"));                                        // 予告放送言い回し
            var greenCar = driver.FindElements(By.Name("green-car"));                                       // グリーン車
            var announceType = driver.FindElements(By.Name("bro_set"));                                     // 放送種類の選択
            var kakekomiWarn = driver.FindElement(By.Name("kakekomi"));                                     // 駆け込み注意喚起
            var firstTrain = driver.FindElement(By.Name("dep"));                                            // 当駅始発
            var nextSt = driver.FindElement(By.Name("nextst"));                                             // 次駅(チェックボックス)
            var stationElem = driver.FindElement(By.Id("station"));                                         // 行先(要素)
            var nextStElem = driver.FindElement(By.Id("nextst"));                                           // 次駅(要素)
            var gene = driver.FindElement(By.Id("gene"));                                                   // 放送生成
            var inputList = driver.FindElement(By.Id("inputList"));                                         // ★ 自動放送開始 ★

            // 津田氏に存在しないパーツ
            ReadOnlyCollection<IWebElement>? yellowLine = null;                                             // 接近放送言い回し
            IWebElement? nextSt2 = null;                                                                    // 連動タイプ
            if (!maleVoice)
            {
                yellowLine = driver.FindElements(By.Name("yelowline"));                                 
                nextSt2 = driver.FindElement(By.Name("nextst2"));                                       
            }

            Console.WriteLine("[Announce] サイトの準備を待っています...");

            driver.ExecuteScript("SoundPause()");
            // 準備ができるまで待つ
            new WebDriverWait(driver, TimeSpan.FromSeconds(60)).Until(d => inputList.Enabled);

            Console.WriteLine("[Announce] サイトの準備完了");
            Console.WriteLine("[Announce] 設定の準備中");

            // 発車放送
            if(type == AnnounceType.Departing) // 音鉄さんのATOSシミュは出発放送が仙台式なので汎用のやつを再現する
            {
                driver.ExecuteScript("InputClear()");
                string[] parts = [trackNum + "番線", "ドアが閉まります", "ご注意下さい"];

                Console.WriteLine("[Announce] 設定を入力しています...");

                var action = new Actions(driver);
                foreach (string part in parts)
                {
                    allList.SelectByText(part);
                    action.DoubleClick(allList.SelectedOption).Build().Perform(); // ダブルクリックでパーツが追加される
                }
            }
            // 到着予告放送・到着放送
            else
            {
                // 要素を設定して自動放送開始
                var postScript = "";
                switch (type)
                {
                    case AnnounceType.ArrivalNotice:
                        postScript = "行です";
                        break;

                    case AnnounceType.Arrival:
                        postScript = "行が参ります";
                        break;
                }

                Console.WriteLine("[Announce] 設定を入力しています...");

                var stationArray = stationElem.Text.Split("\r\n");
                if (stationArray.Contains(train.dest)) // まず単体パーツがあるか確認
                    station.SelectByText(stationArray.Contains(train.dest + postScript) ? train.dest + postScript : train.dest); // 行が参りますなど連動パーツの存在を確認しあれば連動で、なければ単体

                hourList.SelectByIndex(train.departTime.Hour);
                minList.SelectByIndex(train.departTime.Minute);
                kindList.SelectByText(train.type);
                carNumberList.SelectByText(carsNum + "両です");
                trackNumberList.SelectByText(trackNum + "番線");

                if(yellowLine != null && !maleVoice)
                    yellowLine[1].Click(); // 黄色い点字ブロック固定

                // 次駅放送
                bool doNextStAnnounce = false;

                if (nextSt2 != null && !maleVoice && !nextSt2.Selected) // 次駅のパーツは連動に固定
                    nextSt2.Click();

                var nextStArray = nextStElem.Text.Split("\r\n");
                if (nextStArray.Contains(train.nextSt + "(停)"))
                {
                    nextStList.SelectByText(train.nextSt + "(停)");
                    doNextStAnnounce = true;
                }

                if (doNextStAnnounce && !nextSt.Selected || !doNextStAnnounce && nextSt.Selected || type == AnnounceType.Arrival && nextSt.Selected)
                    nextSt.Click();

                // グリーン車
                GreenCarType greenType = GreenCarType.NoAnnounce;

                if (lineWithGreenCar.Contains(train.lineName)) // グリーン車がそもそもない路線の場合は案内をしない
                    greenType = train.hasGreenCar ? GreenCarType.HasGreenCar : GreenCarType.NoGreenCar;

                greenCar[(int)greenType].Click();

                // 駆け込み注意喚起を外す
                if (kakekomiWarn.Selected)
                    kakekomiWarn.Click();

                // 始発
                if (train.isFirst && !firstTrain.Selected || !train.isFirst && firstTrain.Selected)
                    firstTrain.Click();

                // 9時以前はおはようございますにする
                greetings[customDate.Hour < 9 ? (int)GreetingsType.GoodMorning : (int)GreetingsType.Normal].Click();

                // 放送の種類
                announceType[(int)type].Click();

                // 放送を生成
                gene.Click();
            }

            Console.WriteLine("[Announce] 放送を開始");

            // 放送開始
            inputList.Click();

            Thread.Sleep(100);

            // 放送が終わるまで待つ
            if(waitRequired)
                new WebDriverWait(driver, TimeSpan.FromSeconds(60)).Until(d => inputList.Enabled);
        }

        public static void ShowNextTrain(Train train)
        {
            Console.Write(train.type + "\t" + train.departTime.ToString("t") + "\t" + train.dest);
            if(train.hasGreenCar)
                Console.Write("\tグリーン車あり");

            Console.WriteLine();
        }
    }
}
