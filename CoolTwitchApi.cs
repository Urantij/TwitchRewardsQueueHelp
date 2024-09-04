using TwitchLib.Api;

namespace TwitchRewardsQueueHelp;

public class CoolTwitchApi
{
    private readonly TwitchAPI api;
    private readonly string refreshToken;

    public CoolTwitchApi(string clientId, string secret, string refreshToken)
    {
        api = new TwitchAPI();
        api.Settings.ClientId = clientId;
        api.Settings.Secret = secret;
        this.refreshToken = refreshToken;
    }

    public async Task<TwitchAPI> GetApiAsync()
    {
        await TrrrAsync();

        return api;
    }

    public async Task TrrrAsync()
    {
        var response = await api.Auth.RefreshAuthTokenAsync(refreshToken, api.Settings.Secret, api.Settings.ClientId);

        api.Settings.AccessToken = response.AccessToken;
    }
}