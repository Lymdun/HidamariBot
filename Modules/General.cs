using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Rest;
using HidamariBot.Services;
using Qmmands;
using Serilog;

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

    /*[SlashCommand("everyone"), Description("[Oni] Pour everyone en passant par les pseudos."), RequireAuthorRole(481655718936707083)]
    public async Task<IResult> Everyone() {
        Snowflake guildId = this.Context.GuildId.GetValueOrDefault();
        IReadOnlyList<IMember> members = await this.Bot.FetchMembersAsync(guildId);

        IEnumerable<string> mentions = members.Select(x => x.Mention);
        string? message = string.Join(", ", mentions);

        Log.Information(message ?? "");

        return Response("Test (Kud sale fils de pute)");
    }*/
}
