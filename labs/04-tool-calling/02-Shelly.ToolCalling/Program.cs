using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using System.ClientModel;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

const string DefaultModel = "qwen3:8b";

string? shellyUrl = null;
string? endpoint = null;
var model = DefaultModel;

for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--model" && i + 1 < args.Length)
        model = args[++i];
    else if (args[i].StartsWith("--model="))
        model = args[i]["--model=".Length..];
    else if (args[i] == "--endpoint" && i + 1 < args.Length)
        endpoint = args[++i];
    else if (args[i].StartsWith("--endpoint="))
        endpoint = args[i]["--endpoint=".Length..];
    else
        shellyUrl ??= args[i];
}

if (shellyUrl is null)
{
    Console.WriteLine($"Usage: Shelly.ToolCalling <url> [--model <name>] [--endpoint <url>] (default model: {DefaultModel})");
    return;
}

// Klucz API dla zdalnego endpointu czytamy ze zmiennej środowiskowej, aby nie trafił do historii poleceń.
var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY");

if (endpoint is not null && string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Set the LLM_API_KEY environment variable to use --endpoint.");
    return;
}

Console.WriteLine($"Connecting to Shelly at {shellyUrl}");
Console.WriteLine($"Using model {model}" + (endpoint is null ? "" : $" at {endpoint}"));

using var http = new HttpClient
{
    BaseAddress = new Uri(shellyUrl)
};

async Task TurnOnLight()
{
    Console.WriteLine("[Tool] Turning light on...");

    var response = await http.GetAsync("/rpc/Switch.Set?id=0&on=true");
    response.EnsureSuccessStatusCode();
}

async Task TurnOffLight()
{
    Console.WriteLine("[Tool] Turning light off...");

    var response = await http.GetAsync("/rpc/Switch.Set?id=0&on=false");
    response.EnsureSuccessStatusCode();
}

async Task<String> CheckIsOn()
{
    Console.WriteLine("[Tool] Checking if turned on...");
    
    var response = await http.GetAsync("/rpc/Switch.GetStatus?id=0");
    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
    
    return json.GetProperty("output").GetBoolean().ToString();
}

async Task<String> CheckPower()
{
    Console.WriteLine("[Tool] Checking power usage...");
    
    var response = await http.GetAsync("/rpc/Switch.GetStatus?id=0");
    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
    
    return $"{json.GetProperty("apower").GetDouble().ToString(CultureInfo.InvariantCulture)} W";
}

async Task<string> ToggleLight()
{
    Console.WriteLine("[Tool] Toggling light state...");
    
    var response = await http.GetAsync("/rpc/Switch.Toggle?id=0");
    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
    
    bool isNowOn = json.GetProperty("output").GetBoolean();
    return isNowOn ? "lampka jest teraz włączona" : "lampka jest teraz wyłączona";
}

// Tools
// Udostępniamy modelowi dwie funkcje wraz z opisami.
var tools = new[]
{
    AIFunctionFactory.Create(
        TurnOnLight,
        name: "turn_on_light",
        description: "Turns on the light"), 

    AIFunctionFactory.Create(
        TurnOffLight,
        name: "turn_off_light",
        description: "Turns off the light"),
    
    AIFunctionFactory.Create(
        CheckIsOn,
        name: "check_status",
        description: "Checks if light turned on"),
    
    AIFunctionFactory.Create(
        CheckPower,
        name: "check_power",
        description: "Checks power usage"),
    
    AIFunctionFactory.Create(
        ToggleLight,
        name: "toggle_light",
        description: "Toggles the light state and returns the new current state")
};

// LLM
// Bez --endpoint używamy lokalnej Ollamy, w przeciwnym razie dowolnego API zgodnego z OpenAI (np. https://llm.hpc.psnc.pl/v1).
IChatClient client = endpoint is null
    ? new OllamaApiClient(
        new Uri("http://localhost:11434"),
        model)
    : new OpenAIClient(
        new ApiKeyCredential(apiKey!),
        new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
        .GetChatClient(model)
        .AsIChatClient();

// Automatic tool calling
IChatClient chatClient = new ChatClientBuilder(client)
    .UseFunctionInvocation() // Automatycznie obsługuje cykl Tool Calling: model wybiera narzędzie, aplikacja wykonuje funkcję i przekazuje wynik modelowi.
    .Build();

var options = new ChatOptions
{
    Tools = tools
};

Console.WriteLine("Enter a command (or 'exit' to quit):");

while (true)
{
    Console.Write("> ");

    var input = Console.ReadLine();

    if (input is null || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    var response = await chatClient.GetResponseAsync(input, options);

    Console.WriteLine($"Assistant: {response.Text}");
}