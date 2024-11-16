using Newtonsoft.Json;
using StackExchange.Redis;
using System.Net;
using System.Text;

namespace Server.RedisReverseProxy;
public class RedisReverseProxy
{
	private readonly HttpListener _listener;
	private readonly IDatabase _db;

	public RedisReverseProxy(string listeningUrl, ConnectionMultiplexer redis)
	{
		_listener = new HttpListener();
		_listener.Prefixes.Add(listeningUrl);
		_db = redis.GetDatabase();
	}

	public async Task StartListeningAsync()
	{
		_listener.Start();
		Console.WriteLine("Reverse proxy started. Listening for incoming requests...");

		while (true)
		{
			var context = await _listener.GetContextAsync();
			_ = Task.Run(async () => await StoreRequestInRedis(context));
		}
	}

	private async Task StoreRequestInRedis(HttpListenerContext context)
	{
		RequestCounter.RequestCount++;

		await _db.ListLeftPushAsync("beforeRequestQueue", RequestCounter.RequestCount);
		var request = context.Request;
		request.Headers.Add("HeaderRequestId",RequestCounter.RequestCount.ToString());
		var requestData = new
		{
			Method = request.HttpMethod,
			Url = request.RawUrl,
			Headers = request.Headers.AllKeys.ToDictionary(k => k, k => request.Headers[k]),
			Body = await new StreamReader(request.InputStream).ReadToEndAsync()
		};

		var requestJson = JsonConvert.SerializeObject(requestData);
		await _db.ListLeftPushAsync("requestQueue", requestJson);

		var response = context.Response;
		response.StatusCode = (int)HttpStatusCode.Accepted;
		var responseMessage = Encoding.UTF8.GetBytes("Request received");
		await response.OutputStream.WriteAsync(responseMessage, 0, responseMessage.Length);
		response.Close();
	}
}
