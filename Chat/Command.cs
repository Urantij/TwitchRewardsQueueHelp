using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchRewardsQueueHelp.Chat;

public class Command
{
    public readonly string trigger;
    public readonly TimeSpan cooldown;
    public readonly Func<TwitchPrivateMessage, Task> @delegate;
    public DateTime? lastTriggered;

    public Command(string trigger, TimeSpan cooldown, Func<TwitchPrivateMessage, Task> @delegate)
    {
        this.trigger = trigger;
        this.cooldown = cooldown;
        this.@delegate = @delegate;
    }

    public bool CanUse()
    {
        return lastTriggered == null || DateTime.UtcNow - lastTriggered.Value >= cooldown;
    }

    public void Update()
    {
        lastTriggered = DateTime.UtcNow;
    }
}
