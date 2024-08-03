using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Extensions.Interactivity;
using Disqord.Extensions.Voice;
using Disqord.Gateway;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace HidamariBot;

public static class Program {
    const ulong OWNER_ID = 435044125033889793;

    static void Main(string[] args) {
        DotNetEnv.Env.Load();

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
                x.AddEnvironmentVariables();
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
            .ConfigureDiscordBot<HidamariDiscordBot>((context, bot) => {
                bot.Token = context.Configuration["DISCORD_TOKEN"];
                bot.ReadyEventDelayMode = ReadyEventDelayMode.Guilds;
                bot.Status = UserStatus.Online;
                bot.Activities = new[] { LocalActivity.Watching("la neige tomber") };
                bot.OwnerIds = new[] { new Snowflake(OWNER_ID) };
                bot.Intents = GetDiscordIntents();
                bot.UseMentionPrefix = false;
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
