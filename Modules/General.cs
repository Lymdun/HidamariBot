using Disqord;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
using HidamariBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace HidamariBot.Modules;

public class General : HidamariBotModuleBase {
    [SlashCommand("ping"), Description("Vérifiez si je suis bien là !")]
    public IResult Ping() => Response("Pong !");

    [SlashCommand("radio"), Description("Rejoint le salon vocal et lance la radio KJ. Queue disponible sur r-a-d.io")]
    public async Task<IResult> PlayRadio() {
        Snowflake guildId = Context.GuildId.GetValueOrDefault();
        var audioService = Context.Bot.Services.GetRequiredService(typeof(AudioPlayerService)) as AudioPlayerService;

        await Deferral();

        CachedVoiceState? memberVoiceState = audioService.GetMemberVoiceState(guildId, Context.Author.Id);
        if (memberVoiceState == null) {
            return Response("Vous devez être dans un salon vocal pour utiliser cette commande.");
        }

        CachedVoiceState? currentVoiceState = audioService.GetBotVoiceState(guildId);
        if (currentVoiceState != null) {
            return Response("Je suis déjà en train de diffuser dans un autre salon.");
        }

        IResult result = await audioService.PlayRadio(guildId, memberVoiceState.ChannelId.GetValueOrDefault());

        if (!result.IsSuccessful) {
            return Response(result.FailureReason ?? "Une erreur inconnue est survenue.");
        }

        AudioPlayerService.RadioStatus radioStatus = await audioService.GetRadioStatusAsync();

        var embed = new LocalEmbed()
            .WithTitle("C'est parti pour s'enjailler sur de la musique KJ !")
            .WithDescription($"**DJ :** {radioStatus.DjName}\n**En ce moment :** {radioStatus.NowPlaying}")
            .WithColor(Color.Orange);

        if (!string.IsNullOrEmpty(radioStatus.DjImage)) {
            embed.WithThumbnailUrl($"https://r-a-d.io/api/dj-image/{radioStatus.DjImage}");
        }

        if (!string.IsNullOrEmpty(radioStatus.ThreadImage)) {
            embed.WithImageUrl(radioStatus.ThreadImage);
        }

        return Response(embed);
    }

    [SlashCommand("radio-stop"), Description("Arrête la radio et me déconnecte du salon vocal")]
    public async Task<IResult> StopRadio() {
        Snowflake guildId = Context.GuildId.GetValueOrDefault();
        var audioService = Context.Bot.Services.GetRequiredService(typeof(AudioPlayerService)) as AudioPlayerService;

        await Deferral();

        CachedVoiceState? botVoiceState = audioService.GetBotVoiceState(guildId);
        if (botVoiceState == null) {
            return Response("Je ne suis pas connecté à un salon vocal.");
        }

        IResult result = await audioService.StopRadio(guildId);

        return result.IsSuccessful
            ? Response("J'ai arrêté la radio et quitté le salon vocal.")
            : Response(result.FailureReason ?? "Une erreur inconnue est survenue.");
    }

    [SlashCommand("radio-title"), Description("Affiche le titre de la musique jouant sur la radio")]
    public async Task<IResult> ShowRadioTitle() {
        var audioService = Context.Bot.Services.GetRequiredService(typeof(AudioPlayerService)) as AudioPlayerService;
        AudioPlayerService.RadioStatus radioStatus = await audioService.GetRadioStatusAsync();

        var embed = new LocalEmbed()
            .WithTitle($"Le titre de la musique est : {radioStatus.NowPlaying}")
            .WithDescription($"**DJ :** {radioStatus.DjName}")
            .WithColor(Color.Orange);

        if (!string.IsNullOrEmpty(radioStatus.DjImage)) {
            embed.WithThumbnailUrl($"https://r-a-d.io/api/dj-image/{radioStatus.DjImage}");
        }

        if (!string.IsNullOrEmpty(radioStatus.ThreadImage)) {
            embed.WithImageUrl(radioStatus.ThreadImage);
        }

        return Response(embed);
    }
}
