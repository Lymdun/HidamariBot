using Newtonsoft.Json;

namespace HidamariBot.Models;

public class Questions {
    [JsonProperty("lastQuestionItemId")]
    public ushort LastQuestionItemId { get; set; }
    [JsonProperty("items")]
    public List<QuestionItem> Items { get; set; }
}

public class QuestionItem {
    [JsonProperty("id")]
    public ushort Id { get; set; }
    [JsonProperty("characterName")]
    public string CharacterName { get; set; }
    [JsonProperty("questionText")]
    public string QuestionText { get; set; }
}
