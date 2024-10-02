using System.Text.Json.Serialization;

namespace HidamariBot.Models;

public class RadioInfo {
    [JsonPropertyName("main")] public MainInfo Main { get; set; }
}

public class MainInfo {
    [JsonPropertyName("np")] public string NowPlaying { get; set; }
}
