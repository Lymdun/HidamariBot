using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Extensions.Interactivity;
using Disqord.Extensions.Voice;
using Disqord.Gateway;
using HidamariBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace HidamariBot;

public static class Program {
    const string TOKEN = "MTA5NTAxMzE1MTgzNTE4MTE5Nw.GjSENx.M9iKyyiB9eKwE8HxmQIbAQjUSoqZqphZ32Vge0";
    const ulong OWNER_ID = 435044125033889793;

    static void Main(string[] args) {
        using (IHost host = CreateHost(args)) {
            host.Run();
        }
    }

    static IHost CreateHost(string[] args) {
        return new HostBuilder()
            .ConfigureHostConfiguration(x => {
                x.AddCommandLine(args);
            })
            .ConfigureAppConfiguration(x => {
                x.AddCommandLine(args);
            })
            .ConfigureLogging(x => {
                ILogger loggerConfig = new LoggerConfiguration()
                    .WriteTo.Console(LogEventLevel.Information)
                    .CreateLogger();
                x.AddSerilog(loggerConfig, true);

                x.Services.Remove(x.Services.First(y => y.ServiceType == typeof(ILogger<>)));
                x.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            })
            .ConfigureServices(ConfigureServices)
            .ConfigureDiscordBot<HidamariDiscordBot>((_, bot) => {
                bot.Token = TOKEN;
                bot.ReadyEventDelayMode = ReadyEventDelayMode.Guilds;
                bot.Status = UserStatus.Online;
                bot.Activities = new[] { LocalActivity.Watching("la neige tomber") };
                bot.OwnerIds = new[] { new Snowflake(OWNER_ID) };
                bot.Intents = GetDiscordIntents();
            })
            .UseDefaultServiceProvider(x => {
                x.ValidateOnBuild = true;
                x.ValidateScopes = true;
            })
            .Build();
    }

    static void ConfigureServices(HostBuilderContext context, IServiceCollection services) {
        services.AddInteractivityExtension();
        services.AddVoiceExtension();
        services.AddLogging();
    }

    static GatewayIntents GetDiscordIntents() {
        return (GatewayIntents.Guilds | GatewayIntents.Integrations | GatewayIntents.Members | GatewayIntents.GuildReactions | GatewayIntents.MessageContent | GatewayIntents.VoiceStates);
    }
}
