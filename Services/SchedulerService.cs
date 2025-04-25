using Cronos;
using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Disqord.Rest;
using HidamariBot.Models;
using Microsoft.Extensions.Logging;

namespace HidamariBot.Services;

public class SchedulerService : DiscordBotService {
    const ulong CHANNEL_ID = 1363662419628654682;
    static readonly TimeZoneInfo frenchTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

    const string CRON_EXPRESSION = "0 12 * * MON,TUE";
    const string DAILY_WEEK_CRON_EXPRESSION = "59 5 * * MON-FRI";

    protected override ValueTask OnReady(ReadyEventArgs e) {
        Logger.LogInformation("SchedulerService Ready fired!");
        return default;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var cronExpression = CronExpression.Parse(CRON_EXPRESSION);
        var dailyWeekCronExpression = CronExpression.Parse(DAILY_WEEK_CRON_EXPRESSION);

        while (!stoppingToken.IsCancellationRequested) {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            DateTimeOffset? nextMemeTime  = cronExpression.GetNextOccurrence(now, frenchTimeZone);
            DateTimeOffset? nextDailyWeekOccurrence = dailyWeekCronExpression.GetNextOccurrence(now, frenchTimeZone);

            (DateTimeOffset? nextOccurrence, bool isDailyWeekMeme) = GetNextOccurrence(nextMemeTime, nextDailyWeekOccurrence);

            if (nextOccurrence.HasValue) {
                TimeSpan delay = nextOccurrence.Value - now;

                if (delay > TimeSpan.Zero) {
                    await Task.Delay(delay, stoppingToken);
                }

                try {
                    if (isDailyWeekMeme)
                        await SendDailyWeekMeme(stoppingToken);
                    else
                        await SendDailyMemeIfApplicable(nextOccurrence.Value.DayOfWeek, stoppingToken);
                } catch (Exception ex) {
                    Logger.LogError(ex, "Failure while sending meme");
                }
            } else {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        Logger.LogWarning("SchedulerService has stopped");
    }

    async Task SendDailyMemeIfApplicable(DayOfWeek dayOfWeek, CancellationToken stoppingToken) {
        switch (dayOfWeek) {
            case DayOfWeek.Monday:
                await SendMeme(stoppingToken, Meme.MondayMeme());
                Logger.LogInformation("SendMorningMessage fired!");
                break;
            case DayOfWeek.Tuesday:
                await SendMeme(stoppingToken, Meme.TuesdayMeme());
                Logger.LogInformation("SendMorningMessage fired!");
                break;
            default:
                Logger.LogInformation("No meme to send today");
                break;
        }
    }

    async Task SendDailyWeekMeme(CancellationToken stoppingToken) {
        await SendMeme(stoppingToken, Meme.DailyWeekMeme());
        Logger.LogInformation("SendDailyWeekMeme fired!");
    }

    async Task SendMeme(CancellationToken stoppingToken, Meme meme) {
        string filePath = Path.Combine("./resources", meme.ImageUrl);

        if (!File.Exists(filePath)) {
            Logger.LogError("This meme does not exist: {FilePath}", filePath);
            return;
        }

        var message = new LocalMessage();
        await using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read)) {
            message.WithAttachments(LocalAttachment.File(fs));
            await Bot.SendMessageAsync(CHANNEL_ID, message, cancellationToken: stoppingToken);
        }
    }

    static (DateTimeOffset? occurrence, bool isDailyWeekMeme) GetNextOccurrence(
        DateTimeOffset? nextMeme,
        DateTimeOffset? nextDailyWeek) {
        if (!nextMeme.HasValue && !nextDailyWeek.HasValue) return (null, false);
        if (!nextMeme.HasValue) return (nextDailyWeek, true);
        if (!nextDailyWeek.HasValue) return (nextMeme, false);

        return nextDailyWeek.Value < nextMeme.Value
            ? (nextDailyWeek, true)
            : (nextMeme, false);
    }

    public static TimeZoneInfo GetTimeZoneInfo() => frenchTimeZone;
}
