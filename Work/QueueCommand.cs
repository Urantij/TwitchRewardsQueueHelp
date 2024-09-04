using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchRewardsQueueHelp.Work;

public static class QueueCommand
{
    public static async Task ExecuteAsync(TwitchPrivateMessage e)
    {
        // Очередь от поздней к ранней
        List<List<RewardRedemption>> queues = new();

        TwitchAPI api = await Program.capi.GetApiAsync();

        foreach (string item in Program.config.Rewards.Reverse())
        {
            string? cursor = null;

            List<RewardRedemption> list = new();
            do
            {
                var result = await api.Helix.ChannelPoints.GetCustomRewardRedemptionAsync(Program.config.ChannelId,
                    item, status: "UNFULFILLED", sort: "OLDEST", first: "50", after: cursor);

                list.AddRange(result.Data);

                cursor = result.Pagination?.Cursor;
            } while (!string.IsNullOrEmpty(cursor));

            queues.Add(list);
        }

        int itemsBefore = 0;
        bool actuallyFound = false;
        foreach (List<RewardRedemption> queue in queues)
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
            await Program.chatBot.channel.SendMessageAsync($"Заказов перед вашим заказом: {itemsBefore}. {totalText}",
                e.id);
        }
        else
        {
            await Program.chatBot.channel.SendMessageAsync(totalText, e.id);
        }
    }
}