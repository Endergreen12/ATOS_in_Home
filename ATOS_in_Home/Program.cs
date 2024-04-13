using static ATOS_in_Home.Functions;

// 準備
Console.WriteLine("ATOS in Home | made by Endergreen12\nこのアプリを終了するときはバツボタンではなく Ctrl + C を押してください");
string? station = "";
string? lineName = "";
string? direction = "";

Console.CancelKeyPress += new ConsoleCancelEventHandler(onExit);

driver = GenerateDriver();

Console.WriteLine("[Main] 放送する駅名を入力してください(例:栃木、小山)、最後に駅をつける必要はないです(存在しない駅名を入力するとエラーになります)");
station = Console.ReadLine();
Console.WriteLine("[Main] 路線名を入力してください(例:宇都宮線)(存在しない路線を入力するとエラーになります)");
lineName = Console.ReadLine();
Console.WriteLine("[Main] 方面を入力してください。(例:上り、下り、外回り)");
direction = Console.ReadLine();
Console.WriteLine("[Main] ネットから取得する方法がないので両数を入力してください。");
carsNum = int.Parse(Console.ReadLine());
Console.WriteLine("[Main] 何番線から発車するか入力してください。");
trackNum = int.Parse(Console.ReadLine());

Console.WriteLine("自分で時間を指定したい場合はyを、現実の時間を利用する場合はほかのキーを押してください。");
if(Console.ReadKey().Key == ConsoleKey.Y)
{
    Console.WriteLine();
    Console.WriteLine("[Main CustomDate] 時間を入力してください。(例: 18:57, 8:02:05)");
    customDate = DateTime.Parse(Console.ReadLine());
    Console.WriteLine("[Main CustomDate] 時間を表示しますか？する場合はYを、しない場合はそれ以外のキーを押してください");
    showTime = Console.ReadKey().Key == ConsoleKey.Y;
    Thread t = new Thread(new ThreadStart(TickCustomDate));
    t.Start();
}
else
    customDate = DateTime.Now;

Console.WriteLine();
Console.WriteLine("[Main] ソフトが起動されたので最初の出発予告放送を開始");

if (station == null || lineName == null || direction == null)
    Environment.Exit(0);

var nextTrain = GetNextTrain(station, lineName, direction);

Console.WriteLine("[Main] 時間の監視を開始 | 5分おきに到着予告放送が流れ、発車1分前に接近放送が流れます");

int time = 0;
bool departed = true;

while (true)
{
    Thread.Sleep(1000);

    time += 5;

    if (time % 300 == 0) // 5分おきに到着予告放送
        Announce(AnnounceType.ArrivalNotice, nextTrain.departTime.Hour, nextTrain.departTime.Minute, nextTrain.dest, nextTrain.type, carsNum, trackNum);

    if (nextTrain.departTime.Subtract(customDate).TotalMinutes <= 1 && departed) // 到着1分前に接近放送
    {
        Announce(AnnounceType.Arrival, nextTrain.departTime.Hour, nextTrain.departTime.Minute, nextTrain.dest, nextTrain.type, carsNum, trackNum);
        departed = false;
    }

    if (nextTrain.departTime.Subtract(customDate).TotalMinutes <= 0 && !departed) // 発車放送
    {
        Announce(AnnounceType.Departing, nextTrain.departTime.Hour, nextTrain.departTime.Minute, nextTrain.dest, nextTrain.type, carsNum, trackNum);
        nextTrain = GetNextTrain(station, lineName, direction);
        departed = true;
    }
}