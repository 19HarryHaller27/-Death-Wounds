namespace DeathWounds;

/// <summary>Maps a watched-attribute int tier key to a body part and a short id for language keys.</summary>
public readonly struct WoundLine
{
    public WoundLine(string attrKey, WoundBodyPart part, string langId)
    {
        AttrKey = attrKey;
        Part = part;
        LangId = langId;
    }

    public string AttrKey { get; }
    public WoundBodyPart Part { get; }
    public string LangId { get; }
}
