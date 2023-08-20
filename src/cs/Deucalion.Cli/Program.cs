using Deucalion.Monitors.Events;
using Microsoft.AspNetCore.SignalR.Client;

var url = "http://localhost:5000/api/monitors/hub";

var hubConnection = new HubConnectionBuilder()
    .WithUrl(url)
    .WithAutomaticReconnect()
    .Build();

hubConnection.On<StateChanged>("StateChanged", Console.WriteLine);
hubConnection.On<MonitorChecked>("MonitorChecked", Console.WriteLine);

await hubConnection.StartAsync();
Console.WriteLine("Connected to hub. Press any key to close.");

Console.ReadKey(true);
