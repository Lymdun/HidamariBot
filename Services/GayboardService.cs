using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Disqord.Rest;
using Microsoft.Extensions.Logging;
using Qommon;

namespace HidamariBot.Services;

public class GayboardService : DiscordBotService {
    const ulong CHANNEL_ID = 542701796787879937;
    const ushort MIN_REACTIONS_REQUIRED = 5;
    static readonly IEmoji DETECTABLE_EMOTE = new LocalEmoji("🏳️‍🌈");
    static List<IMessage> oldMessages = new List<IMessage>();

    protected override async ValueTask OnReactionAdded(ReactionAddedEventArgs e) {
        var message = await Client.FetchMessageAsync(e.ChannelId, e.MessageId) as IUserMessage;
        if (message == null)
            return;

        if (message.Reactions.TryGetValue(out IReadOnlyDictionary<IEmoji, IMessageReaction>? reactions)) {
            if (reactions.TryGetValue(DETECTABLE_EMOTE, out IMessageReaction? reaction) && reaction.Count == MIN_REACTIONS_REQUIRED) {
                if (oldMessages.Count == 0) {
                    oldMessages = new List<IMessage>(await Client.FetchMessagesAsync(CHANNEL_ID, limit: 50));
                }

                if (oldMessages.Any(x => x.Content.Equals($"https://discord.com/channels/{e.GuildId}/{e.ChannelId}/{e.MessageId}"))) {
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

                if (message.Attachments.Count > 0) {
                    IAttachment firstAttachment = message.Attachments[0];
                    if (firstAttachment.ContentType?.StartsWith("image/") == true) {
                        embed.ImageUrl = firstAttachment.Url;
                    }
                }

                var newMessage = new LocalMessage {
                    Embeds = new List<LocalEmbed> { embed },
                    Content = $"https://discord.com/channels/{e.GuildId}/{e.ChannelId}/{e.MessageId}"
                };

                

                oldMessages.Add(await Bot.SendMessageAsync(CHANNEL_ID, newMessage));
            }
        }
    }
}
