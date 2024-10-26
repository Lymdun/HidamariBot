using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Microsoft.Extensions.Logging;
using Disqord.Rest;

namespace HidamariBot.Services;

public class MemberLeaveService : DiscordBotService {
    const ulong NOTIFICATION_CHANNEL_ID = 830851922889539644;
    const string IMAGE_PATH = "./resources/leave.jpg";

    protected override ValueTask OnReady(ReadyEventArgs e) {
        Logger.LogInformation("MemberLeaveService Ready fired!");
        return default;
    }

    protected override ValueTask OnMemberLeft(MemberLeftEventArgs e) {
        _ = SendLeaveNotificationAsync(e.GuildId, e.User);
        return default;
    }

    async Task SendLeaveNotificationAsync(Snowflake guildId, IUser user) {
        try {
            var message = new LocalMessage()
                .WithContent($"{Mention.User(user.Id)} dans le wagon le non-KJ");

            if (File.Exists(IMAGE_PATH)) {
                await using (var fs = new FileStream(IMAGE_PATH, FileMode.Open, FileAccess.Read)) {
                    message.WithAttachments(LocalAttachment.File(fs));

                    await Bot.SendMessageAsync(NOTIFICATION_CHANNEL_ID, message);
                    Logger.LogInformation("Sent leave notification for user {UserId} in guild {GuildId}", user.Id, guildId);
                }
            } else {
                Logger.LogWarning("Image file not found at {ImagePath}", IMAGE_PATH);
            }
        } catch (Exception ex) {
            Logger.LogError(ex, "Failed to send leave notification for user {UserId} in guild {GuildId}", user.Id,
                guildId);
        }
    }
}
