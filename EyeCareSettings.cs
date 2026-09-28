using System.Text.Json;

namespace EyesRest;

internal sealed class EyeCareSettings
{
    private const int RecommendedLongBreakMinutes = 15;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public int MicroBreakWorkMinutes { get; set; } = 20;
    public int MicroBreakSeconds { get; set; } = 20;
    public bool LongBreaksEnabled { get; set; } = true;
    public int LongBreakWorkMinutes { get; set; } = 120;
    public int LongBreakMinutes { get; set; } = RecommendedLongBreakMinutes;
    public int IdleResetMinutes { get; set; } = 2;
    public bool StartupPreferenceConfigured { get; set; }

    public TimeSpan MicroBreakWorkInterval => TimeSpan.FromMinutes(MicroBreakWorkMinutes);

    public TimeSpan MicroBreakDuration => TimeSpan.FromSeconds(MicroBreakSeconds);

    public TimeSpan LongBreakWorkInterval => TimeSpan.FromMinutes(LongBreakWorkMinutes);

    public TimeSpan LongBreakDuration => TimeSpan.FromMinutes(LongBreakMinutes);

    public TimeSpan IdleResetThreshold => TimeSpan.FromMinutes(IdleResetMinutes);

    public static string DirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EyesRest");

    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public static EyeCareSettings Load()
    {
        Directory.CreateDirectory(DirectoryPath);

        if (!File.Exists(FilePath))
        {
            var defaults = CreateDefault();
            defaults.Save();
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<EyeCareSettings>(json, JsonOptions) ?? CreateDefault();
            settings.MigrateRecommendedDefaults();
            settings.Normalize();
            settings.Save();
            return settings;
        }
        catch
        {
            return CreateDefault();
        }
    }

    public static EyeCareSettings CreateDefault()
    {
        var settings = new EyeCareSettings();
        settings.Normalize();
        return settings;
    }

    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        Normalize();
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    private void Normalize()
    {
        MicroBreakWorkMinutes = Math.Clamp(MicroBreakWorkMinutes, 5, 60);
        MicroBreakSeconds = Math.Clamp(MicroBreakSeconds, 10, 120);
        LongBreakWorkMinutes = Math.Clamp(LongBreakWorkMinutes, 60, 240);
        LongBreakMinutes = Math.Clamp(LongBreakMinutes, 2, 20);
        IdleResetMinutes = Math.Clamp(IdleResetMinutes, 1, 15);
    }

    private void MigrateRecommendedDefaults()
    {
        if (LongBreakMinutes == 5)
        {
            LongBreakMinutes = RecommendedLongBreakMinutes;
        }
    }
}
