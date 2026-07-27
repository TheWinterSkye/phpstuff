namespace NightVoice;

internal sealed record VoiceSettings
{
    public float Mix { get; init; } = 1f;
    public float PitchSemitones { get; init; }
    public float FormantSemitones { get; init; }
    public bool PreserveFormants { get; init; } = true;
    public int Quality { get; init; } = 1;
    public float Drive { get; init; }
    public float Echo { get; init; }
    public float Chorus { get; init; }
    public float Reverb { get; init; }
    public float GateDb { get; init; } = -55f;
    public float BassDb { get; init; }
    public float TrebleDb { get; init; }
    public float FormantBaseHz { get; init; } = 180f;
    public string Mode { get; init; } = "Clean Studio";
}

internal sealed record VoicePreset(
    int Mix,
    int Pitch,
    int Formant,
    bool Preserve,
    int Quality,
    int Drive,
    int Echo,
    int Chorus,
    int Reverb,
    int Gate,
    int Bass,
    int Treble)
{
    public static VoicePreset Get(string name) => name switch
    {
        "Warm Broadcast" => new(100, 0, 0, true, 1, 5, 0, 0, 3, -52, 4, -1),
        "Bright Natural" => new(100, 1, 1, true, 1, 1, 0, 0, 3, -55, -2, 4),
        "Natural Higher" => new(100, 2, 1, true, 2, 0, 0, 0, 2, -56, -2, 3),
        "Natural Lower" => new(100, -2, -1, true, 2, 1, 0, 0, 2, -54, 3, -2),
        "Soft Feminine" => new(100, 2, 2, true, 2, 1, 0, 0, 3, -56, -2, 4),
        "Deep Voice" => new(100, -3, -2, true, 2, 4, 0, 0, 4, -52, 5, -3),
        "Telephone" => new(100, 0, 0, true, 0, 16, 2, 0, 0, -44, -12, 10),
        "Robot" => new(100, 0, 0, true, 0, 24, 4, 0, 0, -46, -4, 5),
        "Android" => new(100, -1, 0, true, 1, 14, 3, 12, 2, -48, 0, 5),
        "Demon" => new(100, -7, -4, false, 1, 34, 14, 6, 26, -45, 8, -7),
        "Ghost" => new(100, 4, 2, false, 1, 3, 30, 24, 45, -56, -5, 4),
        "Goblin" => new(100, 6, 4, false, 1, 18, 4, 4, 5, -48, -7, 7),
        "Space Radio" => new(100, -1, 0, true, 0, 20, 18, 10, 10, -46, -6, 8),
        "Dream Chorus" => new(100, 2, 1, true, 2, 1, 12, 45, 30, -56, 0, 3),
        "Dark Whisper" => new(100, -3, -2, false, 1, 7, 16, 18, 34, -58, 4, -5),
        _ => new(100, 0, 0, true, 1, 0, 0, 0, 2, -56, 0, 1)
    };
}
