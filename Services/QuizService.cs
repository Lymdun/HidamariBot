using HidamariBot.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace HidamariBot.Services;

public class QuizService {
    const string FILE_NAME = "questionsList.json";
    const string TENOR_API_KEY = "AIzaSyAzBSYQajJxyWO3TpeuVYwY-e9Gay7-xYE";

    public static async Task<QuestionItem?> GetCurrentQuestionAsync() {
        string json = await File.ReadAllTextAsync(FILE_NAME);
        Questions questions = JsonConvert.DeserializeObject<Questions>(json)!;
        ushort currentQuestionId = (ushort)(questions.LastQuestionItemId + 1);

        QuestionItem? questionItem = questions.Items.FirstOrDefault(x => x.Id.Equals(currentQuestionId));

        // save current question
        if (currentQuestionId < questions.Items.Count)
            questions.LastQuestionItemId = currentQuestionId;
        else
            questions.LastQuestionItemId = 0;

        await SerializeQuestionsAsync(questions);

        return questionItem;
    }

    public static async Task AddQuestionAsync(string characterName, string questionText) {
        string json = await File.ReadAllTextAsync(FILE_NAME);
        Questions questions = JsonConvert.DeserializeObject<Questions>(json)!;
        ushort questionId = (ushort)(questions.Items.Last().Id + 1);

        questions.Items.Add(new QuestionItem {
            Id = questionId,
            CharacterName = characterName,
            QuestionText = questionText
        });

        await SerializeQuestionsAsync(questions);
    }

    static async Task SerializeQuestionsAsync(Questions questions) {
        Log.Information("Saving last question item id: {QuestionsLastQuestionItemId}", questions.LastQuestionItemId);
        var serializer = new JsonSerializer { Formatting = Formatting.Indented };
        await using (var streamWriter = new StreamWriter(FILE_NAME)) {
            await using (var writer = new JsonTextWriter(streamWriter)) {
                serializer.Serialize(writer, questions);
            }
        }
    }

    public static async Task<string?> FetchRandomImage(string characterName) {
        string url = string.Format("https://tenor.googleapis.com/v2/search?q={0}&key={1}&limit=1", characterName, TENOR_API_KEY);

        try {
            using (var client = new HttpClient()) {
                using (HttpResponseMessage responseMessage = await client.GetAsync(url)) {
                    using (HttpContent content = responseMessage.Content) {
                        string data = await content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(data)) {
                            var node = JObject.Parse(data);
                            return node["results"]?[0]?["media_formats"]?["gif"]?["url"]?.ToString();
                        }
                    }
                }
            }
        } catch (Exception e) {
            Log.Error("Error during image fetch: {Exception}", e.ToString());
        }

        return string.Empty;
    }
}
