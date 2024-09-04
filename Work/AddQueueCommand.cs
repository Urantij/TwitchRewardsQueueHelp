using System.Text.RegularExpressions;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchRewardsQueueHelp.Work;

public static partial class AddQueueCommand
{
    public static readonly Regex regex = AddQueueArgsRegex();

    public static async Task ExecuteAsync(TwitchPrivateMessage e)
    {
        if (e.username != "urantij" && !e.badges.ContainsKey("broadcaster"))
            return;

        var match = regex.Match(e.text);

        if (!match.Success)
        {
            await Program.chatBot.channel.SendMessageAsync("\"Название\" Стоимость \"Описание\"", e.id);
            return;
        }

        string title = match.Groups["title"].Value;
        int cost = int.Parse(match.Groups["cost"].Value);
        string description = match.Groups["description"].Value;

        await AddRewardAsync(title, cost, description);

        await Program.chatBot.channel.SendMessageAsync($"Добавил", e.id);
    }

    public static async Task AddRewardAsync(string title, int cost, string prompt)
    {
        TwitchAPI api = await Program.capi.GetApiAsync();

        CreateCustomRewardsResponse result = await api.Helix.ChannelPoints.CreateCustomRewardsAsync(
            Program.config.ChannelId,
            new TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest()
            {
                Title = title,
                Cost = cost,
                IsEnabled = true,
                Prompt = prompt,
                IsUserInputRequired = true
            });

        Program.config.Rewards = [.. Program.config.Rewards, result.Data[0].Id];
        await Program.config.SaveAsync(Program.configPath);
    }

    [GeneratedRegex("\"(?<title>.+?)\" (?<cost>\\d+) \"(?<description>.+?)\"$", RegexOptions.Compiled)]
    private static partial Regex AddQueueArgsRegex();
}