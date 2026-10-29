var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:8080");

var app = builder.Build();

var device = new ShellyDevice
{
    // Nazwę można zmienić parametrem --name, np. aby zademonstrować prompt injection.
    Name = builder.Configuration["name"] ?? "Lampka"
};

// Turn on / off
app.MapGet("/rpc/Switch.Set", (int id, bool on) =>
{
    if (id != 0)
        return Results.BadRequest("Invalid switch id.");

    device.Output = on;

    Console.WriteLine($"[Shelly] Light {(on ? "ON" : "OFF")}");

    return Results.Ok(new
    {
        id,
        output = device.Output,
        was_on = !on
    });
});

// Get status
app.MapGet("/rpc/Switch.GetStatus", (int id) =>
{
    if (id != 0)
        return Results.BadRequest("Invalid switch id.");

    return Results.Ok(device.GetStatus());
});

// Get config
app.MapGet("/rpc/Switch.GetConfig", (int id) =>
{
    if (id != 0)
        return Results.BadRequest("Invalid switch id.");

    return Results.Ok(device.GetConfig());
});

app.MapGet("/rpc/Switch.Toggle", (int id, string tag = null) =>
{
    if (id != 0)
        return Results.BadRequest("Invalid switch id.");

    bool wasOn = device.Output;

    device.Output = !device.Output;

    Console.WriteLine($"[Shelly] Light toggled to {(device.Output ? "ON" : "OFF")}");

    return Results.Ok(new
    {
        id,
        output = device.Output,
        was_on = wasOn
    });
});

/*
1. Co zmieni się w aplikacji, kiedy zamiast użytkownika wybierającego operację decyzję będzie podejmował model językowy?
-- Zamiast sztywnych instrukcji warunkowych (if/switch) i menu wyboru, użytkownik wydaje polecenia w języku naturalnym, a model sam dobiera odpowiednią funkcję na podstawie jej opisu (Tool Calling).

2. Dlaczego model nie powinien odpowiadać na pytania o aktualny stan urządzenia bez jego odczytania?
--Model językowy nie ma bezpośredniego połączenia ze światem fizycznym, więc odpowiedź bez odczytania danych z API byłaby halucynacją. 
  Ponadto stan urządzenia mógł zostać zmieniony z zewnątrz (np. fizycznym przyciskiem), o czym model nie wie bez ponownego wywołania funkcji sprawdzającej status.
    
3. Czy dodanie metody do API automatycznie udostępnia ją modelowi?
--Nie, dodanie endpointu w serwerze pozwala jedynie na obsługę zapytań HTTP. 
  Aby model mógł z niego korzystać, trzeba w kodzie klienta napisać funkcję wywołującą ten endpoint i jawnie zarejestrować ją na liście narzędzi przekazywanych do modelu (np. przez AIFunctionFactory.Create). 
 
 4. Czy model może zrealizować polecenie „Przełącz lampkę” bez dedykowanego narzędzia Switch.Toggle?
--Tak, model może wykonać to zadanie w dwóch krokach, korzystając z dotychczasowych narzędzi. Najpierw wywoła funkcję sprawdzającą aktualny stan urządzenia (check_status), 
  a po otrzymaniu wyniku dobierze odpowiednią funkcję zmieniającą ten stan na przeciwny (turn_on_light lub turn_off_light).   
*/

Console.WriteLine("Shelly Simulator running at http://localhost:8080");

app.Run();

class ShellyDevice
{
    public bool Output { get; set; }

    public string Name { get; set; } = "";

    public object GetConfig()
    {
        return new
        {
            id = 0,
            name = Name,
            initial_state = "match_input",
            auto_on = false,
            auto_off = false
        };
    }

    public object GetStatus()
    {
        return new
        {
            id = 0,
            output = Output,
            apower = Output ? 8.5 : 0.0,
            voltage = 230.0,
            current = Output ? 0.037 : 0.0,
            aenergy = new
            {
                total = 125.4
            },
            temperature = new
            {
                tC = Output ? 32.5 : 25.0
            }
        };
    }
}