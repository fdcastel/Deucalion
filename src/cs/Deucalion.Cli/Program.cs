using System.Net.Http;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using Deucalion.Cli.Models;

var url = "http://localhost:5000/api/monitors/events";

using var client = new HttpClient();
using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
response.EnsureSuccessStatusCode();

using var stream = await response.Content.ReadAsStreamAsync();
var parser = SseParser.Create(stream, (_, data) => Encoding.UTF8.GetString(data));

Console.WriteLine("Connected to SSE stream. Press Ctrl+C to close.");

await foreach (var item in parser.EnumerateAsync())
{
    switch (item.EventType)
    {
        case "MonitorChecked":
            var mc = JsonSerializer.Deserialize<MonitorCheckedDto>(item.Data);
            Console.WriteLine(mc);
            break;
        case "MonitorStateChanged":
            var msc = JsonSerializer.Deserialize<MonitorStateChangedDto>(item.Data);
            Console.WriteLine(msc);
            break;
    }
}
