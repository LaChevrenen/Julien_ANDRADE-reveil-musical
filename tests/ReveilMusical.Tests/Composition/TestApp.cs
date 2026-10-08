using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReveilMusical.Host;
using ReveilMusical.Tests.Fakes;

namespace ReveilMusical.Tests.Composition;

/// <summary>Construit le vrai conteneur DI avec une configuration en mémoire et un HTTP simulé.</summary>
internal static class TestApp
{
    public const string ITunesHost = "itunes.apple.com";
    public const string MusicBrainzHost = "musicbrainz.org";

    public const string ITunesNotFound = """{"resultCount":0,"results":[]}""";
    public const string MusicBrainzNotFound = """{"count":0,"recordings":[]}""";

    public static Dictionary<string, string?> BaseConfiguration() => new()
    {
        ["Music:Providers:0"] = "itunes",
        ["Music:Providers:1"] = "musicbrainz",
        ["Music:HttpTimeoutSeconds"] = "3",
        ["Music:CacheTtlMinutes"] = "60",
        ["Music:MusicBrainz:UserAgent"] = "ReveilMusical/1.0 (tests@exemple.fr)",
        ["Notification:DefaultChannel"] = "push",
        ["Notification:FallbackOrder:0"] = "push",
        ["Notification:FallbackOrder:1"] = "sms",
        ["Notification:FallbackOrder:2"] = "email",
        ["Defaults:FallbackTrack"] = "Here Comes the Sun",
    };

    public static ServiceProvider Build(
        StubHttpMessageHandler http,
        Action<Dictionary<string, string?>>? configure = null,
        Action<IServiceCollection>? customize = null)
    {
        var settings = BaseConfiguration();
        configure?.Invoke(settings);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddReveilMusical(configuration);
        services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => http));
        customize?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>HTTP simulé qui répond selon l'hôte appelé ; <c>null</c> = HTTP 500.</summary>
    public static StubHttpMessageHandler Http(string? itunesBody, string? musicBrainzBody) => new(request =>
    {
        var body = request.RequestUri!.Host == ITunesHost ? itunesBody : musicBrainzBody;
        return body is null
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : StubHttpMessageHandler.JsonResponse(body);
    });

    public static string ITunesFound(string title, string artist) =>
        $$"""{"resultCount":1,"results":[{"trackName":"{{title}}","artistName":"{{artist}}","trackViewUrl":"https://music.apple.com/x"}]}""";

    public static string MusicBrainzFound(string title, string artist) =>
        $$"""{"count":1,"recordings":[{"title":"{{title}}","artist-credit":[{"name":"{{artist}}"}]}]}""";
}
