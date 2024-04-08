using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using HidamariBot.Services;
using Qmmands;

namespace HidamariBot.Modules;

public class General : HidamariBotModuleBase {
    [SlashCommand("ping"), Description("Vérifiez si je suis bien là !")]
    public IResult Ping() => Response("Pong !");

    [SlashCommand("add"), Description("[Oni] Pour ajouter une question au quiz."), RequireAuthorRole(481655718936707083)]
    public async Task<IResult> Add(string characterName, string questionText) {
        if (characterName == String.Empty || questionText == string.Empty)
            return Response("Erreur : Données invalides.");

        await QuizService.AddQuestionAsync(characterName, questionText);
        return Response("Question enregistrée avec succès.");
    }
}
