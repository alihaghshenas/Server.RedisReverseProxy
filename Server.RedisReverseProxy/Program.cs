using Server.RedisReverseProxy;
using StackExchange.Redis;

string listeningUrl = $"http://localhost:8080/";
string targetServerUrl = "http://192.168.10.151:4040/identity/api/Account/Login";
string redisConnection = "localhost:3303,abortConnect=false";

var redis = ConnectionMultiplexer.Connect(redisConnection);

var proxy = new RedisReverseProxy(listeningUrl, redis);
proxy.StartListeningAsync();

Console.ReadLine();

var worker = new RequestWorker(redis.GetDatabase(), targetServerUrl);
await worker.ProcessRequestsAsync(batchSize: 500, delay: 2000, parallelThreadCount: 5);