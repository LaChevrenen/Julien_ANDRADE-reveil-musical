using ReveilMusical.Application;
using ReveilMusical.Domain;

namespace ReveilMusical.Tests.Fakes;

/// <summary>Catalogue programmable : renvoie un morceau, <c>null</c>, ou lève. Trace ses appels.</summary>
public sealed class FakeMusicCatalog : IMusicCatalog
{
    private readonly Func<string, Track?> _behavior;
    private readonly List<string>? _journal;

    private FakeMusicCatalog(string name, Func<string, Track?> behavior, List<string>? journal)
    {
        Name = name;
        _behavior = behavior;
        _journal = journal;
    }

    public string Name { get; }

    public List<string> Queries { get; } = [];

    public int Calls => Queries.Count;

    public static FakeMusicCatalog Returning(string name, List<string>? journal = null) =>
        new(name, q => new Track(q, $"Artiste {name}", name), journal);

    public static FakeMusicCatalog ReturningNull(string name, List<string>? journal = null) =>
        new(name, _ => null, journal);

    public static FakeMusicCatalog Throwing(string name, Exception exception, List<string>? journal = null) =>
        new(name, _ => throw exception, journal);

    public static FakeMusicCatalog With(string name, Func<string, Track?> behavior) =>
        new(name, behavior, null);

    public Task<Track?> FindAsync(string query, CancellationToken ct)
    {
        Queries.Add(query);
        _journal?.Add(Name);
        return Task.FromResult(_behavior(query));
    }
}
