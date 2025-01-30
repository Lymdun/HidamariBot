using Disqord;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
using HidamariBot.Models;
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

        string positionString = radioStatus.CurrentPosition.HasValue
            ? radioStatus.CurrentPosition.Value.ToString(@"mm\:ss")
            : "Inconnu";

        string durationString = radioStatus.TrackDuration.HasValue
            ? radioStatus.TrackDuration.Value.ToString(@"mm\:ss")
            : "Inconnu";

        string progressBar = "";
        if (radioStatus.CurrentPosition.HasValue && radioStatus.TrackDuration.HasValue) {
            double elapsedSeconds = radioStatus.CurrentPosition.Value.TotalSeconds;
            double totalSeconds = radioStatus.TrackDuration.Value.TotalSeconds;

            if (totalSeconds > 0) {
                double fraction = elapsedSeconds / totalSeconds;
                fraction = Math.Clamp(fraction, 0.0, 1.0);

                int totalBlocks = 20;
                int filledBlocks = (int)(fraction * totalBlocks);
                int emptyBlocks = totalBlocks - filledBlocks;

                progressBar = $"[{new string('█', filledBlocks)}{new string('░', emptyBlocks)}]";
            }
        }

        var embed = new LocalEmbed()
            .WithTitle(radioStatus.NowPlaying ?? "Inconnu")
            .WithDescription($"**DJ :** {radioStatus.DjName}")
            .AddField("Nombre d'auditeurs", radioStatus.Listeners.ToString() ?? "?")
            .WithColor(Color.Orange);

        if (!string.IsNullOrEmpty(progressBar)) {
            // example: [██████░░░░░░░░] 01:23 / 03:45
            embed.AddField("Progression",
                $"{progressBar} {positionString}/{durationString}");
        }

        if (!string.IsNullOrEmpty(radioStatus.DjImage)) {
            embed.WithThumbnailUrl($"https://r-a-d.io/api/dj-image/{radioStatus.DjImage}");
        }

        if (!string.IsNullOrEmpty(radioStatus.ThreadImage)) {
            embed.WithImageUrl(radioStatus.ThreadImage);
        }

        return Response(embed);
    }

    [SlashCommand("radio-queue"), Description("Affiche les musiques à venir")]
    public async Task<IResult> ShowRadioQueue() {
        var audioService = Context.Bot.Services.GetRequiredService(typeof(AudioPlayerService)) as AudioPlayerService;
        AudioPlayerService.RadioStatus radioStatus = await audioService.GetRadioStatusAsync();

        var embed = new LocalEmbed()
            .WithTitle("Musiques à venir (Queue)")
            .WithColor(Color.Orange);

        if (radioStatus.Queue?.Count > 0) {
            foreach (QueueTrack track in radioStatus.Queue) {
                var trackTimestamp = DateTimeOffset.FromUnixTimeSeconds(track.Timestamp);
                TimeSpan timeUntilPlay = trackTimestamp - DateTimeOffset.Now;
                string timeUntilPlayText = timeUntilPlay.TotalMinutes < 1 ? "dans moins d'une minute" : $"dans {Math.Round(timeUntilPlay.TotalMinutes)} minutes";

                embed.AddField($"#{radioStatus.Queue.IndexOf(track) + 1} - {track.Title}", timeUntilPlayText);
            }
        } else {
            embed.WithDescription("La queue est vide.");
        }

        return Response(embed);
    }

    [SlashCommand("radio-history"), Description("Affiche l'historique des musiques jouées")]
    public async Task<IResult> ShowRadioHistory() {
        var audioService = Context.Bot.Services.GetRequiredService(typeof(AudioPlayerService)) as AudioPlayerService;
        AudioPlayerService.RadioStatus radioStatus = await audioService.GetRadioStatusAsync();

        var embed = new LocalEmbed()
            .WithTitle("Historique des musiques jouées")
            .WithColor(Color.Orange);

        if (radioStatus.LastPlayed?.Count > 0) {
            foreach (LastPlayedTrack track in radioStatus.LastPlayed) {
                var trackTimestamp = DateTimeOffset.FromUnixTimeSeconds(track.Timestamp);
                TimeSpan timeDiff = DateTimeOffset.Now - trackTimestamp;
                string timeSincePlayText = timeDiff.TotalMinutes < 1 ? "il y a moins d'une minute" : $"il y a {Math.Round(timeDiff.TotalMinutes)} minutes";

                embed.AddField($"#{radioStatus.LastPlayed.IndexOf(track) + 1} - {track.Title}", timeSincePlayText);
            }
        } else {
            embed.WithDescription("L'historique est vide.");
        }

        return Response(embed);
    }
}
