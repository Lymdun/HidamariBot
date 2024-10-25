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

    protected override async ValueTask OnReactionAdded(ReactionAddedEventArgs e) {
        var message = await Client.FetchMessageAsync(e.ChannelId, e.MessageId) as IUserMessage;
        if (message == null) return;

        if (!HasRequiredReactions(message)) return;
        if (await IsAlreadyPosted(e.GuildId.GetValueOrDefault(), message)) return;

        List<LocalEmbed> embeds = CreateEmbeds(message);
        await PostToBoard(e.GuildId.GetValueOrDefault(), message, embeds);
    }

    bool HasRequiredReactions(IUserMessage message) {
        if (!message.Reactions.TryGetValue(out IReadOnlyDictionary<IEmoji, IMessageReaction>? reactions))
            return false;

        return reactions.TryGetValue(DETECTABLE_EMOTE, out IMessageReaction? reaction)
            && reaction.Count == MIN_REACTIONS_REQUIRED;
    }

    async Task<bool> IsAlreadyPosted(Snowflake guildId, IMessage message) {
        IReadOnlyList<IMessage> oldMessages = await Client.FetchMessagesAsync(CHANNEL_ID, limit: 50);
        string contentString = GetMessageLink(guildId, message);

        if (oldMessages.Any(x => x.Content.Equals(contentString))) {
            Logger.LogWarning("Failed to post this gay message as it was already posted");
            return true;
        }

        return false;
    }

    List<LocalEmbed> CreateEmbeds(IUserMessage message) {
        var embeds = new List<LocalEmbed>();

        if (message.ReferencedMessage.HasValue) {
            embeds.Add(CreateEmbed(message.ReferencedMessage.Value));
        }

        embeds.Add(CreateEmbed(message));
        return embeds;
    }

    LocalEmbed CreateEmbed(IUserMessage message) {
        var embed = new LocalEmbed {
            Color = Color.Pink,
            Author = new LocalEmbedAuthor {
                Name = message.Author.Name,
                IconUrl = message.Author.GetAvatarUrl()
            },
            Description = message.Content,
            Footer = new LocalEmbedFooter {
                Text = TimeZoneInfo.ConvertTime(message.CreatedAt(), SchedulerService.GetTimeZoneInfo())
                    .ToString("dd/MM/yyyy HH:mm"),
                IconUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/8/8a/LGBT_Rainbow_Flag.png/800px-LGBT_Rainbow_Flag.png"
            }
        };

        AddAttachmentIfExists(message, embed);

        return embed;
    }

    void AddAttachmentIfExists(IUserMessage message, LocalEmbed embed) {
        if (message.Attachments.Count == 0) return;

        var firstAttachment = message.Attachments[0];
        if (firstAttachment.ContentType?.StartsWith("image/") == true) {
            embed.ImageUrl = firstAttachment.Url;
        }
    }

    async Task PostToBoard(Snowflake guildId, IMessage message, List<LocalEmbed> embeds) {
        var newMessage = new LocalMessage {
            Embeds = embeds,
            Content = GetMessageLink(guildId, message)
        };

        await Bot.SendMessageAsync(CHANNEL_ID, newMessage);
    }

    string GetMessageLink(Snowflake guildId, IMessage message) {
        return $"https://discord.com/channels/{guildId}/{message.ChannelId}/{message.Id}";
    }
}
