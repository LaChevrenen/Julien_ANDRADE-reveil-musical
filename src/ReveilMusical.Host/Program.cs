using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReveilMusical.Application;
using ReveilMusical.Host;

// Démonstration : dotnet run --project src/ReveilMusical.Host -- <userId> <jour> <météo>
if (!WakeUpArguments.TryParse(args, out var request, out var error))
{
    Console.Error.WriteLine(error);
    return 2;
}

// appsettings.json est lu à côté de l'exécutable, quel que soit le répertoire courant.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Services.AddReveilMusical(builder.Configuration);

using var host = builder.Build();
var wakeUp = host.Services.GetRequiredService<IWakeUpService>();

var result = await wakeUp.SendWakeUpAsync(request);

Console.WriteLine($"Demande  : {request.UserId}, {request.Day}, {request.Weather}");
Console.WriteLine($"Morceau  : {result.Track.Title} — {result.Track.Artist} (source : {result.Track.Source})");
Console.WriteLine($"Canal    : {result.ChannelUsed?.Value ?? "aucun (tous les canaux en échec)"}");
Console.WriteLine($"Dégradé  : {(result.Degraded ? "oui" : "non")}");

return result.ChannelUsed is null ? 1 : 0;
