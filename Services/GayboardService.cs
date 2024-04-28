using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Disqord.Rest;
using Microsoft.Extensions.Logging;
using Qommon;

namespace HidamariBot.Services;

public class GayboardService : DiscordBotService {
    const ulong CHANNEL_ID = 542701796787879937;
    static readonly IEmoji DETECTABLE_EMOTE = new LocalEmoji("🏳️‍🌈");

    protected override async ValueTask OnReactionAdded(ReactionAddedEventArgs e) {
        IMessage? message = await Client.FetchMessageAsync(e.ChannelId, e.MessageId);
        if (message == null)
            return;

        if (message.Reactions.TryGetValue(out IReadOnlyDictionary<IEmoji, IMessageReaction>? reactions)) {
            if (reactions.TryGetValue(DETECTABLE_EMOTE, out IMessageReaction? reaction) && reaction.Count == 5) {
                IReadOnlyList<IMessage> oldMessages = await Client.FetchMessagesAsync(CHANNEL_ID, limit: 50);
                if (oldMessages.Any(x => x.Content.Equals(message.Content) && x.Author.Equals(message.Author))) {
                    Logger.LogWarning("Failed to post this gay message as it was already posted");
                    return;
                }

                var embed = new LocalEmbed {
                    Color = Color.Pink,
                    Author = new LocalEmbedAuthor {
                        Name = message.Author.Name,
                        IconUrl = message.Author.GetAvatarUrl()
                    },
                    Description = message.Content,
                    Footer = new LocalEmbedFooter {
                        Text = TimeZoneInfo.ConvertTime(message.CreatedAt(), SchedulerService.GetTimeZoneInfo()).ToString("dd/MM/yyyy HH:mm"),
                        IconUrl ="https://upload.wikimedia.org/wikipedia/commons/thumb/8/8a/LGBT_Rainbow_Flag.png/800px-LGBT_Rainbow_Flag.png"
                    }
                };

                await Bot.SendMessageAsync(CHANNEL_ID, new LocalMessage().WithEmbeds(embed));
            }
        }
    }
}
