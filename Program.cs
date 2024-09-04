using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TwitchRewardsQueueHelp.Chat;
using TwitchRewardsQueueHelp.Work;

namespace TwitchRewardsQueueHelp;

class Program
{
    public const string configPath = "./config.json";

    public static Config config = null!;
    public static CoolTwitchApi capi = null!;
    public static ChatBot chatBot = null!;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        using var loggerFactory = LoggerFactory.Create(b =>
        {
            b.AddSimpleConsole(c => { c.TimestampFormat = "[HH:mm:ss] "; });
        });
        var logger = loggerFactory.CreateLogger("Main");

        if (!File.Exists(configPath))
        {
            logger.LogCritical("Конфиг файла нет.");
            await Task.Delay(5);
            return;
        }

        {
            string content = await File.ReadAllTextAsync(configPath);
            config = JsonSerializer.Deserialize<Config>(content)!;
        }

        capi = new CoolTwitchApi(config.ClientId, config.Secret, config.Refresh);

        chatBot = new(config.ChannelName,
            new TwitchSimpleLib.Chat.TwitchChatClientOpts(config.BotUsername, config.BotToken), loggerFactory);
        chatBot.AddCommand(new Command("Добавить", TimeSpan.Zero, AddQueueCommand.ExecuteAsync));
        chatBot.AddCommand(new Command("Очередь", config.QueueCooldown, QueueCommand.ExecuteAsync));

        await chatBot.StartAsync();

        while (true)
        {
            Console.WriteLine(":)");
            string? commandText = Console.ReadLine();

            if (commandText == null)
                continue;

            if (commandText.StartsWith('+'))
            {
                Match match = AddQueueCommand.regex.Match(commandText[1..]);

                if (!match.Success)
                {
                    Console.WriteLine("\"Название\" Стоимость \"Описание\"");
                    continue;
                }

                string title = match.Groups["title"].Value;
                int cost = int.Parse(match.Groups["cost"].Value);
                string description = match.Groups["description"].Value;

                await AddQueueCommand.AddRewardAsync(title, cost, description);
            }

            Console.WriteLine("Добавил");
        }
    }
}