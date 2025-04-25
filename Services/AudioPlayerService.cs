using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Extensions.Voice;
using Disqord.Gateway;
using Disqord.Voice;
using HidamariBot.Audio;
using HidamariBot.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using Qmmands;

namespace HidamariBot.Services;

public class AudioPlayerService : DiscordBotService {
    readonly SemaphoreSlim _semaphore = new(1, 1);
    HttpClient? _httpClient;
    AudioPlayer? _audioPlayer;
    CancellationTokenSource? _cts;

    const string RADIO_URL = "https://stream.r-a-d.io/main.mp3";
    const string RADIO_API_URL = "https://r-a-d.io/api";

    const int RECONNECT_ATTEMPTS = 5;
    const int RECONNECT_DELAY_MS = 2500;

    public async Task<IResult> PlayRadio(Snowflake guildId, Snowflake channelId) {
        try {
            await _semaphore.WaitAsync();
            _cts = new CancellationTokenSource();

            VoiceExtension voiceExt = Bot.GetRequiredExtension<VoiceExtension>();
            IVoiceConnection voiceConn = await voiceExt.ConnectAsync(guildId, channelId, _cts.Token);

            _httpClient = new HttpClient();
            _audioPlayer = new AudioPlayer(voiceConn);

            _ = PlayRadioWithReconnectionAsync(guildId);

            return Results.Success;
        } catch (Exception ex) {
            Logger.LogError(ex, "Error while trying to start the radio");
            return Results.Failure("Une erreur est survenue !");
        } finally {
            _semaphore.Release();
        }
    }

    async Task PlayRadioWithReconnectionAsync(Snowflake guildId) {
        int attempts = 0;

        while (_cts != null
               && !_cts.IsCancellationRequested
               && attempts < RECONNECT_ATTEMPTS) {
            try {
                var audioSource = new FFmpegAudioSource(RADIO_URL);

                if (_audioPlayer!.TrySetSource(audioSource)) {
                    _audioPlayer.Start();

                    while (_audioPlayer.IsPlaying
                           && !_cts.Token.IsCancellationRequested) {
                        await Task.Delay(200, _cts.Token);
                    }
                }

                attempts = 0;
            } catch (OperationCanceledException) {
                Logger.LogInformation("Radio playback cancelled for guild {GuildId}", guildId);
                break;
            } catch (Exception ex) {
                attempts++;
                Logger.LogError(ex,
                    "Error during radio playback. Attempt {Attempt} of {MaxAttempts}",
                    attempts, RECONNECT_ATTEMPTS);

                if (attempts < RECONNECT_ATTEMPTS)
                    await Task.Delay(RECONNECT_DELAY_MS, _cts.Token);
            }
        }

        if (attempts >= RECONNECT_ATTEMPTS) {
            Logger.LogWarning(
                "Max reconnection attempts reached for guild {GuildId}. Stopping radio", guildId);
            await StopRadio(guildId);
        }
    }

    public struct RadioStatus {
        public string? NowPlaying { get; set; }
        public string? DjName { get; set; }
        public string? ThreadImage { get; set; }
        public string? DjImage { get; set; }
        public int? Listeners { get; set; }
        public TimeSpan? CurrentPosition { get; set; }
        public TimeSpan? TrackDuration { get; set; }
        public List<QueueTrack>? Queue { get; set; }
        public List<LastPlayedTrack>? LastPlayed { get; set; }
    }

    public async Task<RadioStatus> GetRadioStatusAsync() {
        try {
            string response = await _httpClient!.GetStringAsync(RADIO_API_URL);
            RadioInfo? radioInfo = JsonSerializer.Deserialize<RadioInfo>(response);

            long currentTimestamp = radioInfo?.Main?.Current ?? 0;
            long startTimestamp = radioInfo?.Main?.StartTime ?? 0;
            long endTimestamp = radioInfo?.Main?.EndTime ?? 0;

            long positionInSeconds = currentTimestamp - startTimestamp;
            long totalDurationInSeconds = endTimestamp - startTimestamp;

            TimeSpan? currentPosition = positionInSeconds > 0
                ? TimeSpan.FromSeconds(positionInSeconds)
                : null;

            TimeSpan? trackDuration = totalDurationInSeconds > 0
                ? TimeSpan.FromSeconds(totalDurationInSeconds)
                : null;

            return new RadioStatus {
                NowPlaying = radioInfo?.Main?.NowPlaying,
                DjName = radioInfo?.Main?.Dj?.Name,
                ThreadImage = ExtractThreadImageUrl(radioInfo?.Main?.Thread),
                DjImage = radioInfo?.Main?.Dj?.Image,
                Listeners = radioInfo?.Main?.Listeners,
                CurrentPosition = currentPosition,
                TrackDuration = trackDuration,
                Queue = radioInfo?.Main?.Queue,
                LastPlayed = radioInfo?.Main?.LastPlayed
            };
        } catch (Exception ex) {
            Logger.LogError(ex, "Error getting radio status");
            return new RadioStatus();
        }
    }

    /// <summary>
    /// Extracts the image URL from an HTML string representing a thread.
    /// </summary>
    /// <param name="threadHtml">The HTML string potentially containing an img tag with a src attribute.</param>
    /// <returns>The image URL extracted from the src attribute, or an empty string if no URL is found or the input string is null or empty.</returns>
    /// <example>
    /// Input example:
    /// "thread": "\"\u003E\u003C/a\u003E\u003Cimg src=\"https://shamiko.org/assets/images/src/55522b96f42010c97881e1bcf40cc1dc22081143.jpg\" style=\"max-width:500px;\" /\u003E\u003C!--",
    /// Expected result:
    /// "https://shamiko.org/assets/images/src/55522b96f42010c97881e1bcf40cc1dc22081143.jpg"
    /// </example>
    string ExtractThreadImageUrl(string? threadHtml) {
        if (string.IsNullOrWhiteSpace(threadHtml) || threadHtml.Length < 5)
            return string.Empty;

        if (threadHtml.StartsWith("image:")) {
            return threadHtml.Substring(6);
        }

        Match match = Regex.Match(threadHtml, @"src=""([^""]+)""");
        if (match.Success && match.Groups.Count > 1) {
            return match.Groups[1].Value;
        }

        return string.Empty;
    }

    public async Task<IResult> StopRadio(Snowflake guildId) {
        await _semaphore.WaitAsync();

        try {
            if (_cts != null) {
                await _cts.CancelAsync();
                _cts.Dispose();
                _cts = null;
            }

            if (_audioPlayer != null) {
                _audioPlayer.Stop();
                await _audioPlayer.DisposeAsync();
                _audioPlayer = null;
            }

            if (_httpClient != null) {
                _httpClient.Dispose();
                _httpClient = null;
            }

            VoiceExtension voiceExtension = Bot.GetRequiredExtension<VoiceExtension>();
            await voiceExtension.DisconnectAsync(guildId);

            return Results.Success;
        } catch (Exception ex) {
            Logger.LogError(ex, "Error while trying to stop the radio");
            return Results.Failure("Une erreur est survenue lors de la déconnexion");
        } finally {
            _semaphore.Release();
        }
    }

    public CachedVoiceState? GetBotVoiceState(Snowflake guildId) {
        return Bot.GetVoiceState(guildId, Bot.CurrentUser.Id);
    }

    public CachedVoiceState? GetMemberVoiceState(Snowflake guildId, Snowflake memberId) {
        return Bot.GetVoiceState(guildId, memberId);
    }

    bool IsVoiceChannelEmpty(Snowflake guildId, Snowflake channelId) {
        IReadOnlyDictionary<Snowflake, CachedVoiceState> voiceStates = Bot.GetVoiceStates(guildId);
        return !voiceStates.Any(vs => vs.Value.ChannelId == channelId && vs.Key != Bot.CurrentUser.Id);
    }

    protected override ValueTask OnReady(ReadyEventArgs e) {
        Logger.LogInformation("AudioPlayerService Ready fired!");
        return default;
    }

    protected override async ValueTask OnVoiceStateUpdated(VoiceStateUpdatedEventArgs e) {
        if (e.MemberId == Bot.CurrentUser.Id && e.NewVoiceState.ChannelId == null) {
            await StopRadio(e.GuildId);
        }

        CachedVoiceState? botVoiceState = GetBotVoiceState(e.GuildId);
        if (botVoiceState != null && botVoiceState.ChannelId.HasValue) {
            if (IsVoiceChannelEmpty(e.GuildId, botVoiceState.ChannelId.Value)) {
                Logger.LogInformation("Bot left voice channel in guild {GuildId} because it became empty", e.GuildId);
                await StopRadio(e.GuildId);
            }
        }
    }
}
