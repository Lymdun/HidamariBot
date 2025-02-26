using System.Text.RegularExpressions;
using Disqord;
using Disqord.Bot.Hosting;
using Microsoft.Extensions.Logging;
using Disqord.Gateway;
using Disqord.Rest;
using Qmmands;

namespace HidamariBot.Services;

public class StreamerNotificationService : DiscordBotService {
    readonly SemaphoreSlim _semaphore = new(1, 1);
    HttpClient? _httpClient;
    CancellationTokenSource? _cts;
    string _lastDjName = string.Empty;

    const ulong CHANNEL_ID = 481723797498757120;

    const string BASE_URL = "https://r-a-d.io";
    const string SSE_URL = "https://r-a-d.io/v1/sse";
    const int RECONNECT_ATTEMPTS = 5;
    const int RECONNECT_DELAY_MS = 1500;

    public async Task<IResult> StartListening() {
        try {
            await _semaphore.WaitAsync();

            if (_cts != null) {
                Logger.LogWarning("SSE listener already running");
                return Results.Failure("Un écouteur SSE est déjà en cours pour ce serveur.");
            }

            _cts = new CancellationTokenSource();
            _httpClient = new HttpClient();

            _ = ListenForEventsWithReconnectionAsync();

            return Results.Success;
        } catch (Exception ex) {
            Logger.LogError(ex, "Error while trying to start SSE listener");
            return Results.Failure("Une erreur est survenue lors du démarrage de l'écouteur SSE.");
        } finally {
            _semaphore.Release();
        }
    }

    async Task ListenForEventsWithReconnectionAsync() {
        int attempts = 0;
        while (_cts != null && !_cts.IsCancellationRequested && attempts < RECONNECT_ATTEMPTS) {
            try {
                using HttpResponseMessage response =
                    await _httpClient!.GetAsync(SSE_URL, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                response.EnsureSuccessStatusCode();

                await using Stream stream = await response.Content.ReadAsStreamAsync(_cts.Token);
                using var reader = new StreamReader(stream);

                string eventData = string.Empty;
                string eventName = string.Empty;

                while (!reader.EndOfStream && !_cts.IsCancellationRequested) {
                    string? line = await reader.ReadLineAsync();
                    if (line != null) {
                        if (line.StartsWith("event:")) {
                            eventName = line.Substring(6).Trim();
                            eventData = string.Empty; // reset for next event
                        } else if (line.StartsWith("data:")) {
                            eventData += line.Substring(5).Trim() + "\n"; // save all lines
                        } else if (string.IsNullOrWhiteSpace(line) && !string.IsNullOrEmpty(eventData)) {
                            // empty line means end of event
                            await HandleEvent(eventName, eventData.Trim());
                            eventData = string.Empty;
                        }
                    }
                }

                attempts = 0; // Reset attempts on successful connection
            } catch (OperationCanceledException) {
                Logger.LogInformation("SSE listener cancelled");
                break;
            } catch (Exception ex) {
                attempts++;
                Logger.LogError(ex, "Error during SSE listener Attempt {Attempt} of {MaxAttempts}", attempts,
                    RECONNECT_ATTEMPTS);

                if (attempts < RECONNECT_ATTEMPTS) {
                    await Task.Delay(RECONNECT_DELAY_MS, _cts.Token);
                }
            }
        }

        if (attempts >= RECONNECT_ATTEMPTS) {
            Logger.LogWarning(
                "Max reconnection attempts reached for SSE listener. Stopping listener");
            await StopListening();
        }
    }

    async Task HandleEvent(string eventName, string data) {
        if (eventName == "streamer") {
            Logger.LogInformation("New streamer detected: {StreamerData}", data);

            string djName = ExtractDjName(data);
            if (djName != _lastDjName) {
                _lastDjName = djName;

                string? imageUrl = ExtractImageUrl(data);
                await SendDiscordMessage(djName, imageUrl);
            }
        }
    }

    async Task SendDiscordMessage(string djName, string? imageUrl) {
        try {
            var embed = new LocalEmbed()
                .WithTitle("Évènement détecté")
                .WithDescription($"Nouveau streamer en direct : {djName}")
                .WithColor(Color.Orange);

            if (!string.IsNullOrWhiteSpace(imageUrl)) {
                embed.WithImageUrl(imageUrl);
            }

            await Bot.SendMessageAsync(CHANNEL_ID, new LocalMessage().WithEmbeds(embed));
        } catch (Exception ex) {
            Logger.LogError(ex, "Error sending streamer notification to Discord");
        }
    }

    string ExtractDjName(string data) {
        Match djNameMatch = Regex.Match(data, @"<div id=""dj-name""[^>]*>(.+?)</div>");
        return djNameMatch.Success ? djNameMatch.Groups[1].Value : "DJ inconnu";
    }

    string? ExtractImageUrl(string data) {
        Match imageMatch = Regex.Match(data, @"<img src=""(/api/dj-image/[^""]+)""");
        if (imageMatch.Success) {
            return $"{BASE_URL}{imageMatch.Groups[1].Value}";
        }

        return null;
    }

    async Task StopListening() {
        await _semaphore.WaitAsync();

        try {
            if (_cts != null) {
                await _cts.CancelAsync();
                _cts.Dispose();
                _cts = null;
            }

            if (_httpClient != null) {
                _httpClient.Dispose();
                _httpClient = null;
            }

            Logger.LogInformation("SSE listener stopped");
        } catch (Exception ex) {
            Logger.LogError(ex, "Error while trying to stop SSE listener");
            Results.Failure("Une erreur est survenue lors de l'arrêt de l'écouteur SSE.");
        } finally {
            _semaphore.Release();
        }
    }

    protected override ValueTask OnReady(ReadyEventArgs e) {
        Logger.LogInformation("StreamerNotificationService Ready fired!");
        return default;
    }
}
