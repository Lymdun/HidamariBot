using System.Text.Json.Serialization;

namespace HidamariBot.Models;

public class RadioInfo {
    [JsonPropertyName("main")] public MainInfo? Main { get; set; }
}

public class MainInfo {
    [JsonPropertyName("np")] public string? NowPlaying { get; set; }

    [JsonPropertyName("dj")] public DjInfo? Dj { get; set; }

    [JsonPropertyName("thread")] public string? Thread { get; set; }
}

public class DjInfo {
    [JsonPropertyName("djname")] public string? Name { get; set; }

    [JsonPropertyName("djimage")] public string? Image { get; set; }
}
