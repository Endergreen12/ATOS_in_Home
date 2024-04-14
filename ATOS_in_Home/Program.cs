using OpenQA.Selenium;
using static ATOS_in_Home.Functions;

// 準備
Console.Title = "ATOS in Home";
Console.WriteLine("ATOS in Home | made by Endergreen12");
string? station = "";
string? lineName = "";
string? direction = "";
int? announceInterval = 0;

// プログラム終了時にWeb Driverを終了する関数を登録
myHandlerDele = new HandlerRoutine(onExit);
SetConsoleCtrlHandler(myHandlerDele, true);

Console.WriteLine("[Main] 男声(津田氏)を選択する場合はYを、しない場合はそれ以外のキーを押してください");
if (Console.ReadKey().Key == ConsoleKey.Y)
{
    atosSimuUrl = atosSimuMaleUrl;
    maleVoice = true;
}
Console.WriteLine();

driver = GenerateDriver();
driver.Navigate().GoToUrl(atosSimuUrl);
atosWindow = driver.CurrentWindowHandle;

Console.WriteLine("[Main] 放送する駅名を入力してください(例:栃木、小山)、最後に駅をつける必要はないです(存在しない駅名を入力するとエラーになります)");
station = Console.ReadLine();
Console.WriteLine("[Main] 路線名を入力してください(例:宇都宮線)(存在しない路線を入力するとエラーになります)");
lineName = Console.ReadLine();
Console.WriteLine("[Main] 方面を入力してください。(例:上り、下り、外回り)");
direction = Console.ReadLine();
carsNum = ReadNumber("[Main] ネットから取得する方法がないので両数を入力してください。(例:4, 6, 15)");
trackNum = ReadNumber("[Main] 何番線から発車するか入力してください。(例:1, 10, 15)");
announceInterval = ReadNumber("[Main] 何秒間隔で予告放送を流すか入力してください。(例:60, 360)");

Console.WriteLine("自分で時間を指定したい場合はyを、現実の時間を利用する場合はほかのキーを押してください。");
// 時間の指定
if(Console.ReadKey().Key == ConsoleKey.Y)
{
    Console.WriteLine();
    while (true)
    {
        Console.WriteLine("[Main CustomDate] 時間を入力してください。(例: 18:57, 8:02:05)");
        if (DateTime.TryParse(Console.ReadLine(), out customDate))
            break;
        else
            Console.WriteLine("入力された文字列を時間に変換できませんでした。\nもう一度入力してください。");
    }
    Console.WriteLine("[Main CustomDate] 時間を表示しますか？する場合はYを、しない場合はそれ以外のキーを押してください");
    showTime = Console.ReadKey().Key == ConsoleKey.Y;

    useCustomDate = true;
}

Thread t = new Thread(new ThreadStart(TickCustomDate));
t.Start();

Console.WriteLine();
Console.WriteLine("[Main] 最初の出発予告放送を開始");

if (station == null || lineName == null || direction == null)
    Environment.Exit(0);

var nextTrain = GetNextTrain(station, lineName, direction);

Console.WriteLine("[Main] 時間の監視を開始 | 発車1分前に接近放送が流れます");

int time = 0;
bool departed = true;

while (true)
{
    Thread.Sleep(1000);

    AnnounceType announceType = AnnounceType.Invalid;
    bool doAnnounce = false;

    time += 1;

    if (nextTrain.type == null || nextTrain.dest == null)
    {
        Console.WriteLine("[Main] 不明なエラー(null)");
        Console.ReadKey();
        Environment.Exit(0);
    }

    if (nextTrain.departTime.Subtract(customDate).TotalMinutes <= 1 && departed) // 到着1分前に接近放送
    {
        announceType = AnnounceType.Arrival;
        departed = false;
        doAnnounce = true;
    }

    if (nextTrain.departTime.Subtract(customDate).TotalMinutes <= 0 && !departed && announceType != AnnounceType.Arrival) // 発車放送
    {
        announceType = AnnounceType.Departing;
        departed = true;
        doAnnounce = true;
    }

    if (announceType == AnnounceType.Invalid && departed && driver.FindElement(By.Id("inputList")).Enabled && time % announceInterval == 0) // 定期的な予告放送
    {
        announceType = AnnounceType.ArrivalNotice;
        doAnnounce = true;
    }

    if(doAnnounce && nextTrain.type != null && nextTrain.dest != null)
        Announce(announceType, nextTrain, announceType == AnnounceType.Departing); // 出発放送は次発放送により止められないように放送が終わるまで待つ

    if(announceType == AnnounceType.Departing)
        nextTrain = GetNextTrain(station, lineName, direction);
}