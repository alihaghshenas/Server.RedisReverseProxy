using Newtonsoft.Json;
using StackExchange.Redis;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Server.RedisReverseProxy;
public class RequestWorker
{
	private readonly IDatabase _db;
	private readonly string _targetServerUrl;

	public RequestWorker(IDatabase db, string targetServerUrl)
	{
		_db = db;
		_targetServerUrl = targetServerUrl;
	}

	public async Task ProcessRequestsAsync(int batchSize, int delay, int parallelThreadCount)
	{
		while (true)
		{
			Console.WriteLine($"TotalRequest:{RequestCounter.RequestCount} Failed:{RequestCounter.FailedCount} Succeed: {RequestCounter.SuccessCount}");
			var requests = await _db.ListRangeAsync("requestQueue", 0, batchSize - 1);
			if (requests.Length == 0)
			{
				await Task.Delay(delay);
				continue;
			}

			var tasks = new List<Task>();

			foreach (var requestJson in requests)
			{
				tasks.Add(Task.Run(async () =>
				{
					await _db.ListLeftPushAsync("inProccessRequestQueue", requestJson);

					var request = JsonConvert.DeserializeObject<JObject>(requestJson);
					bool success = await ForwardRequestToServer(request);

					await _db.ListRemoveAsync("inProccessRequestQueue", requestJson);
					await _db.ListLeftPushAsync("sendedRequestQueue", requestJson);

					if (success)
					{
						await _db.ListRemoveAsync("sendedRequestQueue", requestJson);
						await _db.ListLeftPushAsync("deliveredRequestQueue", requestJson);
						await _db.ListRemoveAsync("requestQueue", requestJson);
					}
				}));

				if (tasks.Count >= parallelThreadCount)
				{
					await Task.WhenAll(tasks);
					tasks.Clear();
				}
			}

			if (tasks.Count > 0)
			{
				await Task.WhenAll(tasks);
			}

			await Task.Delay(delay);
		}
	}

	private async Task<bool> ForwardRequestToServer(JObject request)
	{
		HttpClient httpClient = new HttpClient();

		var targetUrl = _targetServerUrl + (string)request["Url"];
		var method = new HttpMethod((string)request["Method"]);

		var message = new HttpRequestMessage(method, targetUrl);

		foreach (var header in request["Headers"].ToObject<Dictionary<string, string>>())
		{
			message.Headers.TryAddWithoutValidation(header.Key, header.Value);
		}

		if (request["Body"] != null && method != HttpMethod.Get)
		{
			message.Content = new StringContent((string)request["Body"], Encoding.UTF8, "application/json");
		}

		try
		{
			var response = await httpClient.SendAsync(message);


			if (response.IsSuccessStatusCode)
			{
				RequestCounter.SuccessCount++;
			}
			else
			{
				var errorResponse = await response.Content.ReadAsStringAsync();
				await _db.ListLeftPushAsync("errorResponses", errorResponse);
				RequestCounter.FailedCount++;
			}

			return response.IsSuccessStatusCode;
		}
		catch (HttpRequestException ex)
		{
			Console.WriteLine($"Request failed: {ex.Message}");
			RequestCounter.FailedCount++;
			return false;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Unexpected error: {ex.Message}");
			RequestCounter.FailedCount++;
			return false;
		}
	}
}