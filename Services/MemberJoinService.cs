using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Microsoft.Extensions.Logging;
using Disqord.Rest;

namespace HidamariBot.Services;

public class MemberJoinService : DiscordBotService {
    const ulong NOTIFICATION_CHANNEL_ID = 830851922889539644;
    const string IMAGE_PATH = "./resources/chezlymdun.png";

    protected override ValueTask OnReady(ReadyEventArgs e) {
        Logger.LogInformation("MemberJoinService Ready fired!");
        return default;
    }

    protected override ValueTask OnMemberJoined (MemberJoinedEventArgs e) {
        _ = SendJoinNotificationAsync(e.GuildId, e.Member);
        return default;
    }

    async Task SendJoinNotificationAsync (Snowflake guildId, IMember newMember) {
        try {
            LocalMessage message = new LocalMessage()
                .WithContent($"{Mention.User(newMember.Id)} Bienvenue dans l'antre de lymdzoune");

            if (File.Exists(IMAGE_PATH)) {
                await using (var fs = new FileStream(IMAGE_PATH, FileMode.Open, FileAccess.Read)) {
                    message.WithAttachments(LocalAttachment.File(fs));

                    await Bot.SendMessageAsync(NOTIFICATION_CHANNEL_ID, message);
                    Logger.LogInformation("Sent join notification for user {memberId} in guild {guildId}", newMember.Id, guildId);
                }
            } else {
                Logger.LogWarning("Image file not found at {ImagePath}", IMAGE_PATH);
            }
        } catch (Exception ex) {
            Logger.LogError(ex, "Failed to send join message for member {memberId} in guild {guildId}", newMember.Id, guildId);
        }
    }
}
