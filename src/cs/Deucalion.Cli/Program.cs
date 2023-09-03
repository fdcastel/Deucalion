using Deucalion.Cli.Models;
using Microsoft.AspNetCore.SignalR.Client;

var url = "http://localhost:5000/api/monitors/hub";

var hubConnection = new HubConnectionBuilder()
    .WithUrl(url)
    .WithAutomaticReconnect()
    .Build();

hubConnection.On<MonitorCheckedDto>("MonitorChecked", Console.WriteLine);
hubConnection.On<MonitorStateChangedDto>("MonitorStateChanged", Console.WriteLine);

await hubConnection.StartAsync();
Console.WriteLine("Connected to hub. Press any key to close.");

Console.ReadKey(true);
