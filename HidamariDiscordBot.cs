using Disqord;
using Disqord.Bot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HidamariBot;

public class HidamariDiscordBot : DiscordBot {
    public HidamariDiscordBot(IOptions<DiscordBotConfiguration> options, ILogger<HidamariDiscordBot> logger,
        IServiceProvider services, DiscordClient client)
        : base(options, logger, services, client) { }
}
