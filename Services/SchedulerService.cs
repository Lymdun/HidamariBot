using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Disqord.Rest;
using HidamariBot.Models;
using Microsoft.Extensions.Logging;

namespace HidamariBot.Services;

public class SchedulerService : DiscordBotService {
    const ulong CHANNEL_ID = 1203397714935549962;
    readonly TimeZoneInfo frenchTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
    int lastQuestionDayExecuted;
    int lastMemeDayExecuted;

    protected override ValueTask OnReady(ReadyEventArgs e)
    {
        Logger.LogInformation("SchedulerService Ready fired!");
        return default;
    }

    // https://pgroene.wordpress.com/2018/05/31/run-scheduled-background-tasks-in-asp-net-core/
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            DateTime today = DateTime.UtcNow;
            TimeSpan waitEveningTime = GetEveningTime();
            TimeSpan waitMorningTime = GetMorningTime();

            if (Math.Abs(waitEveningTime.TotalSeconds) <= 5 && lastQuestionDayExecuted != today.DayOfYear) {
                await SendMessage(stoppingToken);
                lastQuestionDayExecuted = DateTime.UtcNow.DayOfYear;
                Logger.LogInformation("SendMessage fired!");
            } else if (Math.Abs(waitMorningTime.TotalSeconds) <= 5 && lastMemeDayExecuted != today.DayOfYear) {
                switch (today.DayOfWeek) {
                    case DayOfWeek.Monday:
                        await SendMeme(stoppingToken, Meme.MondayMeme());
                        lastMemeDayExecuted = DateTime.UtcNow.DayOfYear;
                        Logger.LogInformation("SendMorningMessage fired!");
                        break;
                    case DayOfWeek.Tuesday:
                        await SendMeme(stoppingToken, Meme.TuesdayMeme());
                        lastMemeDayExecuted = DateTime.UtcNow.DayOfYear;
                        Logger.LogInformation("SendMorningMessage fired!");
                        break;
                }
            }
        }

        if (stoppingToken.IsCancellationRequested)
            Logger.LogWarning("StoppingToken cancellation requested");
    }

    async Task SendMessage(CancellationToken stoppingToken) {
        QuestionItem? question = await QuizService.GetCurrentQuestionAsync();
        if (question != null) {
            LocalEmbed embed = new LocalEmbed()
                .WithColor(Color.Orange)
                .WithTitle(question.CharacterName)
                .WithDescription(question.QuestionText);

            string? characterImage = await QuizService.FetchRandomImage(question.CharacterName);
            if (!string.IsNullOrEmpty(characterImage))
                embed.WithImageUrl(characterImage);

            await Bot.SendMessageAsync(CHANNEL_ID, new LocalMessage().WithEmbeds(embed), cancellationToken: stoppingToken);
        }
    }

    async Task SendMeme(CancellationToken stoppingToken, Meme meme) {
        var message = new LocalMessage();
        await using (FileStream fs = File.OpenRead($"./resources/{meme.ImageUrl}")) {
            message.WithAttachments(LocalAttachment.File(fs));
            await Bot.SendMessageAsync(CHANNEL_ID, message, cancellationToken: stoppingToken);
        }
    }

    TimeSpan GetEveningTime() {
        DateTime frenchTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, frenchTimeZone);
        return new TimeSpan(22, 22, 30) - frenchTime.TimeOfDay;
    }

    TimeSpan GetMorningTime() {
        DateTime frenchTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, frenchTimeZone);
        return new TimeSpan(12, 00, 30) - frenchTime.TimeOfDay;
    }

    DateTime GetNextOccurrence() => TimeZoneInfo.ConvertTimeFromUtc(
        new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day + 1, 22, 23, 00), frenchTimeZone);
}
