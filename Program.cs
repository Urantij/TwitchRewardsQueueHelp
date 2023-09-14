using System.Text.Json;
using Microsoft.Extensions.Logging;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchRewardsQueueHelp.Chat;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchRewardsQueueHelp;
class Program
{
    const string configPath = "./config.json";

    static Config config = null!;
    static CoolTwitchApi capi = null!;
    static ChatBot chatBot = null!;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        using var loggerFactory = LoggerFactory.Create(b =>
        {
            b.AddSimpleConsole(c =>
            {
                c.TimestampFormat = "[HH:mm:ss] ";
            });
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

        chatBot = new(config.ChannelName, new TwitchSimpleLib.Chat.TwitchChatClientOpts(config.BotUsername, config.BotToken), loggerFactory);
        chatBot.AddCommand(new Command("ДобавитьОчередь", TimeSpan.Zero, AddQueueCommand));
        chatBot.AddCommand(new Command("Очередь", TimeSpan.FromMinutes(2), QueueCommand));

        await chatBot.StartAsync();

        while (true)
        {
            System.Console.WriteLine(":)");
            Console.ReadLine();
        }
    }

    static async Task AddQueueCommand(TwitchPrivateMessage e)
    {
        if (e.username != "urantij" && !e.badges.ContainsKey("broadcaster"))
            return;

        int counter = config.Rewards.Length + 1;

        var api = await capi.GetApiAsync();

        var result = await api.Helix.ChannelPoints.CreateCustomRewardsAsync(config.ChannelId, new TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest()
        {
            Title = $"Заказ x{counter}",
            Cost = 1000 * (int)Math.Pow(10, counter),
            IsEnabled = false,
            Prompt = "Талончик",
            IsUserInputRequired = true
        });

        config.Rewards = config.Rewards.Append(result.Data[0].Id).ToArray();
        await config.SaveAsync(configPath);

        await chatBot.channel.SendMessageAsync($"Добавил x{counter}", e.id);
    }

    static async Task QueueCommand(TwitchPrivateMessage e)
    {
        // Очередь от дорогой к дешёвой
        List<List<RewardRedemption>> queues = new();

        var api = await capi.GetApiAsync();

        foreach (var item in config.Rewards.Reverse())
        {
            string? cursor = null;

            List<RewardRedemption> list = new();
            do
            {
                var result = await api.Helix.ChannelPoints.GetCustomRewardRedemptionAsync(config.ChannelId, item, status: "UNFULFILLED", sort: "OLDEST", first: "50", after: cursor);

                list.AddRange(result.Data);

                cursor = result.Pagination?.Cursor;
            }
            while (!string.IsNullOrEmpty(cursor));

            queues.Add(list);
        }

        int total = queues.SelectMany(q => q).Count();

        int itemsBefore = 0;
        bool actuallyFound = false;
        foreach (var queue in queues)
        {
            RewardRedemption? requested = queue.FirstOrDefault(item => item.UserId == e.userId);

            if (requested != null)
            {
                actuallyFound = true;

                itemsBefore += queue.IndexOf(requested);
                break;
            }
            else
            {
                itemsBefore += queue.Count;
            }
        }

        if (actuallyFound)
        {
            await chatBot.channel.SendMessageAsync($"Ваша очередь: {itemsBefore}. Всего слотов: {total}", e.id);
        }
        else
        {
            if (total > 0)
            {
                await chatBot.channel.SendMessageAsync($"Всего слотов: {total}", e.id);
            }
            else
            {
                await chatBot.channel.SendMessageAsync("В очереди нет слотов.", e.id);
            }
        }
    }
}
