using Disqord;
using Disqord.Bot;
using Disqord.Bot.Hosting;
using Disqord.Extensions.Voice;
using Disqord.Gateway;
using Disqord.Voice;
using HidamariBot.Audio;
using Microsoft.Extensions.Logging;
using Qmmands;

namespace HidamariBot.Services;

public class AudioPlayerService : DiscordBotService {
    readonly ILogger<AudioPlayerService> _logger;
    readonly DiscordBot _bot;
    readonly SemaphoreSlim _semaphore = new(1, 1);
    HttpClient? _httpClient;
    AudioPlayer? _audioPlayer;
    IVoiceConnection? _voiceConnection;

    const string RADIO_URL = "https://stream.r-a-d.io/main.mp3";

    public AudioPlayerService(DiscordBot bot, ILogger<AudioPlayerService> logger) {
        _bot = bot;
        _logger = logger;
    }

    public async Task<IResult> PlayRadio(Snowflake guildId, Snowflake channelId, CancellationToken cancellationToken = default) {
        try {
            VoiceExtension voiceExtension = _bot.GetRequiredExtension<VoiceExtension>();
            _voiceConnection = await voiceExtension.ConnectAsync(guildId, channelId);

            _httpClient = new HttpClient();
            Stream stream = await _httpClient.GetStreamAsync(RADIO_URL);
            var audioSource = new FFmpegAudioSource(stream);
            _audioPlayer = new AudioPlayer(_voiceConnection);

            if (_audioPlayer.TrySetSource(audioSource)) {
                _audioPlayer.Start();
                return Results.Success;
            }

            return Results.Failure("Impossible de diffuser la radio.");
        } catch (Exception ex) {
            _logger.LogError(ex, "Error while trying to start the radio");
            return Results.Failure("Une erreur est survenue !");
        }
    }

    public async Task<IResult> StopRadio() {
        await _semaphore.WaitAsync();

        try {
            if (_audioPlayer != null) {
                _audioPlayer.Stop();
                await _audioPlayer.DisposeAsync();
                _audioPlayer = null;
            }

            if (_httpClient != null) {
                _httpClient.Dispose();
                _httpClient = null;
            }

            if (_voiceConnection != null) {
                await _voiceConnection.DisposeAsync();
                _voiceConnection = null;
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "Error while trying to stop the radio");
            return Results.Failure("Une erreur est survenue lors de la déconnexion");
        } finally {
            _semaphore.Release();
        }

        return Results.Success;
    }

    public CachedVoiceState? GetBotVoiceState(Snowflake guildId) {
        return _bot.GetVoiceState(guildId, _bot.CurrentUser.Id);
    }

    public CachedVoiceState? GetMemberVoiceState(Snowflake guildId, Snowflake memberId) {
        return _bot.GetVoiceState(guildId, memberId);
    }

    bool IsVoiceChannelEmpty(Snowflake guildId, Snowflake channelId) {
        IReadOnlyDictionary<Snowflake, CachedVoiceState> voiceStates = _bot.GetVoiceStates(guildId);
        return !voiceStates.Any(vs => vs.Value.ChannelId == channelId && vs.Key != _bot.CurrentUser.Id);
    }

    protected override ValueTask OnReady(ReadyEventArgs e) {
        _logger.LogInformation("AudioPlayerService Ready fired!");
        return default;
    }

    protected override async ValueTask OnVoiceStateUpdated(VoiceStateUpdatedEventArgs e) {
        if (e.MemberId == Bot.CurrentUser.Id && e.NewVoiceState.ChannelId == null) {
            await StopRadio();
        }

        CachedVoiceState? botVoiceState = GetBotVoiceState(e.GuildId);
        if (botVoiceState != null && botVoiceState.ChannelId.HasValue) {
            if (IsVoiceChannelEmpty(e.GuildId, botVoiceState.ChannelId.Value)) {
                _logger.LogInformation("Bot left voice channel in guild {GuildId} because it became empty", e.GuildId);
                await StopRadio();
            }
        }
    }
}
