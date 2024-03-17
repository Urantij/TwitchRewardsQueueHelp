using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchRewardsQueueHelp.Chat;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchRewardsQueueHelp;

partial class Program
{
    const string configPath = "./config.json";

    static Config config = null!;
    static CoolTwitchApi capi = null!;
    static ChatBot chatBot = null!;

    static readonly Regex addQueueArgsRegex = AddQueueArgsRegex();

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
        chatBot.AddCommand(new Command("Добавить", TimeSpan.Zero, AddQueueCommand));
        chatBot.AddCommand(new Command("Очередь", config.QueueCooldown, QueueCommand));

        await chatBot.StartAsync();

        while (true)
        {
            Console.WriteLine(":)");
            Console.ReadLine();
        }
    }

    static async Task AddQueueCommand(TwitchPrivateMessage e)
    {
        if (e.username != "urantij" && !e.badges.ContainsKey("broadcaster"))
            return;

        var match = addQueueArgsRegex.Match(e.text);

        if (!match.Success)
        {
            await chatBot.channel.SendMessageAsync("\"Название\" Стоимость \"Описание\"", e.id);
            return;
        }

        string title = match.Groups["title"].Value;
        int cost = int.Parse(match.Groups["cost"].Value);
        string description = match.Groups["description"].Value;

        await AddRewardAsync(title, cost, description);

        await chatBot.channel.SendMessageAsync($"Добавил", e.id);
    }

    static async Task AddRewardAsync(string title, int cost, string prompt)
    {
        var api = await capi.GetApiAsync();

        var result = await api.Helix.ChannelPoints.CreateCustomRewardsAsync(config.ChannelId, new TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest()
        {
            Title = title,
            Cost = cost,
            IsEnabled = true,
            Prompt = prompt,
            IsUserInputRequired = true
        });

        config.Rewards = [.. config.Rewards, result.Data[0].Id];
        await config.SaveAsync(configPath);
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

        string totalText;
        {
            List<List<RewardRedemption>> nonEmptyQueues = queues.Where(q => q.Count > 0).ToList();

            if (nonEmptyQueues.Count == 0)
            {
                totalText = "В очереди нет слотов.";
            }
            else if (nonEmptyQueues.Count == 1)
            {
                totalText = $"Всего слотов: {nonEmptyQueues.First().Count}";
            }
            else
            {
                string queueText = string.Join("+", nonEmptyQueues.Select(q => q.Count));
                totalText = $"Всего слотов: {queueText}";
            }
        }

        if (actuallyFound)
        {
            await chatBot.channel.SendMessageAsync($"Заказов перед вашим заказом: {itemsBefore}. {totalText}", e.id);
        }
        else
        {
            await chatBot.channel.SendMessageAsync(totalText, e.id);
        }
    }

    [GeneratedRegex("\"(?<title>.+?)\" (?<cost>\\d+) \"(?<description>.+?)\"$", RegexOptions.Compiled)]
    private static partial Regex AddQueueArgsRegex();
}
