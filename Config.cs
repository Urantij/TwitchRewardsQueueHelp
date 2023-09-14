using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace TwitchRewardsQueueHelp;

public class Config
{
    [Required]
    public required string ChannelName { get; set; }
    [Required]
    public required string ChannelId { get; set; }

    [Required]
    public required string BotUsername { get; set; }
    [Required]
    public required string BotToken { get; set; }

    [Required]
    public required string ClientId { get; set; }
    [Required]
    public required string Secret { get; set; }
    [Required]
    public required string Refresh { get; set; }

    /// <summary>
    /// Чем ниже в списке, тем более важная очередь.
    /// </summary>
    public string[] Rewards { get; set; } = Array.Empty<string>();

    public Task SaveAsync(string path)
    {
        string content = JsonSerializer.Serialize(this, options: new JsonSerializerOptions()
        {
            WriteIndented = true
        });
        return File.WriteAllTextAsync(path, content);
    }
}
