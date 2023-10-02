using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchLib.Api;
using TwitchRewardsQueueHelp.Chat;
using TwitchSimpleLib.Chat;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchRewardsQueueHelp.Chat;

public class ChatBot
{
    const string prefix = "=";

    readonly List<Command> commands = new();

    readonly ILogger? logger;

    public readonly TwitchChatClient client;

    public readonly ChatAutoChannel channel;

    public ChatBot(string channelName, TwitchChatClientOpts opts, ILoggerFactory? loggerFactory)
    {
        logger = loggerFactory?.CreateLogger(typeof(ChatBot));

        client = new TwitchChatClient(true, opts, loggerFactory);

        client.AuthFailed += AuthFailed;
        client.Connected += Connected;
        client.ConnectionClosed += ConnectionClosed;

        channel = client.AddAutoJoinChannel(channelName);
        channel.PrivateMessageReceived += PrivateMessageReceived;
    }

    public Task StartAsync()
    {
        return client.ConnectAsync();
    }

    public void AddCommand(Command command)
    {
        commands.Add(command);
    }

    private void PrivateMessageReceived(object? sender, TwitchPrivateMessage e)
    {
        if (!e.text.StartsWith("="))
            return;

        string trigger;
        {
            trigger = e.text.Split(' ')[0][prefix.Length..];
        }

        Command? command = commands.FirstOrDefault(c => c.trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase));
        if (command == null)
            return;

        if (!e.mod && !command.CanUse())
            return;

        command.Update();
        Task delegateTask = command.@delegate.Invoke(e);

        delegateTask.ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                logger?.LogError(task.Exception, "Ошибка при выполнении делегате {trigger}", command.trigger);
            }
        });
    }

    private void AuthFailed(object? sender, EventArgs e)
    {
        logger?.LogCritical("Аутентификация провалилась");
    }

    private void Connected()
    {
        logger?.LogInformation("Подключился.");
    }

    private void ConnectionClosed(Exception? exception)
    {
        logger?.LogWarning("Потерял соединение {message}", exception?.Message);
    }
}
