using static ATOS_in_Home.Functions;

// 準備
Console.WriteLine("ATOS in Home | made by Endergreen12");
string? station = "";
string? lineName = "";
string? direction = "";

driver = GenerateDriver();

Console.WriteLine("[Main] 放送する駅名を入力してください(例:栃木、小山)、最後に駅をつける必要はないです(存在しない駅名を入力するとエラーになります)");
station = Console.ReadLine();
Console.WriteLine("[Main] 路線名を入力してください(例:宇都宮線)(存在しない路線を入力するとエラーになります)");
lineName = Console.ReadLine();
Console.WriteLine("[Main] 上りか下りかを入力してください。それ以外はエラーになります");
direction = Console.ReadLine();
Console.WriteLine("[Main] ネットから取得する方法がないので両数を入力してください。");
carsNum = int.Parse(Console.ReadLine());
Console.WriteLine("[Main] 何番線から発車するか入力してください。");
trackNum = int.Parse(Console.ReadLine());

#if DEBUG
Console.WriteLine("[Main DEBUG] 時間を入力");
customDate = DateTime.Parse(Console.ReadLine());
Thread t = new Thread(new ThreadStart(TickCustomDate));
t.Start();
#endif


Console.WriteLine("[Main] ソフトが起動されたので最初の出発予告放送を開始");

if (station == null || lineName == null || direction == null)
    Environment.Exit(0);

var nextTrain = GetNextTrain(station, lineName, direction);

Console.WriteLine("[Main] 時間の監視を開始 | 5分おきに到着予告放送が流れ、発車1分前に接近放送が流れます");

int time = 0;
bool departed = true;

while (true)
{
    Thread.Sleep(5000);

    time += 5;

    if (time % 300 == 0) // 5分おきに到着予告放送
        Announce(AnnounceType.ArrivalNotice, nextTrain.departTime.Hour, nextTrain.departTime.Minute, nextTrain.dest, nextTrain.type, carsNum, trackNum);

    if (nextTrain.departTime.Subtract(
        #if DEBUG
                customDate
        #else
                DateTime.Now
        #endif
        ).Minutes <= 1 && departed) // 到着1分前に接近放送
    {
        Announce(AnnounceType.Arrival, nextTrain.departTime.Hour, nextTrain.departTime.Minute, nextTrain.dest, nextTrain.type, carsNum, trackNum);
        departed = false;
    }

    if (nextTrain.departTime.Subtract(
        #if DEBUG
                customDate
        #else
                DateTime.Now
        #endif
            ).Minutes <= 0 && !departed) // 発車放送
    {
        Announce(AnnounceType.Departing, nextTrain.departTime.Hour, nextTrain.departTime.Minute, nextTrain.dest, nextTrain.type, carsNum, trackNum);
        nextTrain = GetNextTrain(station, lineName, direction);
        departed = true;
    }
}