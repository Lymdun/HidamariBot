using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Extensions.Voice;
using Disqord.Gateway;
using Disqord.Voice;
using HidamariBot.Audio;
using HidamariBot.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Qmmands;

namespace HidamariBot.Services;

public class AudioPlayerService : DiscordBotService {
    readonly SemaphoreSlim _semaphore = new(1, 1);
    HttpClient? _httpClient;
    AudioPlayer? _audioPlayer;
    CancellationTokenSource? _cts;

    const string RADIO_URL = "https://stream.r-a-d.io/main.mp3";
    const string RADIO_API_URL = "https://r-a-d.io/api";

    const int RECONNECT_ATTEMPTS = 3;
    const int RECONNECT_DELAY_MS = 5000;
    const int BUFFER_SIZE = 1024 * 1024; // 1MB buffer

    public async Task<IResult> PlayRadio(Snowflake guildId, Snowflake channelId) {
        try {
            await _semaphore.WaitAsync();

            _cts = new CancellationTokenSource();

            VoiceExtension voiceExtension = Bot.GetRequiredExtension<VoiceExtension>();
            IVoiceConnection voiceConnection = await voiceExtension.ConnectAsync(guildId, channelId, _cts.Token);

            _httpClient = new HttpClient();
            _audioPlayer = new AudioPlayer(voiceConnection);

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
        while (_cts != null && !_cts.IsCancellationRequested && attempts < RECONNECT_ATTEMPTS) {
            try {
                using HttpResponseMessage response =
                    await _httpClient!.GetAsync(RADIO_URL, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                response.EnsureSuccessStatusCode();

                await using Stream stream = await response.Content.ReadAsStreamAsync(_cts.Token);
                await using var bufferedStream = new BufferedStream(stream, BUFFER_SIZE);

                var audioSource = new FFmpegAudioSource(bufferedStream);

                if (_audioPlayer!.TrySetSource(audioSource)) {
                    _audioPlayer.Start();
                    await Task.Delay(-1, _cts.Token); // wait indefinitely
                }

                attempts = 0; // reset attempts on successful playback
            } catch (OperationCanceledException) {
                break; // exit if cancellation was requested
            } catch (Exception ex) {
                attempts++;
                Logger.LogError(ex, "Error during radio playback. Attempt {Attempt} of {MaxAttempts}", attempts,
                    RECONNECT_ATTEMPTS);

                if (attempts < RECONNECT_ATTEMPTS) {
                    await Task.Delay(RECONNECT_DELAY_MS, _cts.Token);
                }
            }
        }

        if (attempts >= RECONNECT_ATTEMPTS) {
            Logger.LogWarning("Max reconnection attempts reached for guild {GuildId}. Stopping radio", guildId);
            await StopRadio(guildId);
        }
    }

    public async Task<string> GetCurrentSongTitleAsync() {
        try {
            string response = await _httpClient.GetStringAsync(RADIO_API_URL);
            RadioInfo? radioInfo = JsonSerializer.Deserialize<RadioInfo>(response);

            if (radioInfo?.Main.NowPlaying != null) {
                 return radioInfo.Main.NowPlaying;
            }
        } catch (Exception ex) {
            Logger.LogError(ex, "Error getting current song title");
        }

        return string.Empty;
    }

    public async Task<IResult> StopRadio(Snowflake guildId) {
        await _semaphore.WaitAsync();

        try {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

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
