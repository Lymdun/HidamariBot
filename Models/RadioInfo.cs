using System.Text.Json.Serialization;

namespace HidamariBot.Models;

public class RadioInfo {
    [JsonPropertyName("main")] public MainInfo? Main { get; set; }
}

public class MainInfo {
    [JsonPropertyName("np")] public string? NowPlaying { get; set; }

    [JsonPropertyName("dj")] public DjInfo? Dj { get; set; }

    [JsonPropertyName("thread")] public string? Thread { get; set; }

    [JsonPropertyName("listeners")] public int? Listeners { get; set; }

    [JsonPropertyName("current")] public long? Current { get; set; }

    [JsonPropertyName("start_time")] public long? StartTime { get; set; }

    [JsonPropertyName("end_time")] public long? EndTime { get; set; }
}

public class DjInfo {
    [JsonPropertyName("djname")] public string? Name { get; set; }

    [JsonPropertyName("djimage")] public string? Image { get; set; }
}
