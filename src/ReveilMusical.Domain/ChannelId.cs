namespace ReveilMusical.Domain;

/// <summary>
/// Identifiant de canal : chaîne normalisée (trim + minuscules), pas un enum,
/// pour qu'un nouveau canal ne modifie aucun code existant.
/// </summary>
public sealed record ChannelId
{
    public ChannelId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim().ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
