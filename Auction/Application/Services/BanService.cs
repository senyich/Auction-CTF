using System.Collections.Concurrent;
using Application.Common;

public class BanService
{
    private readonly ConcurrentDictionary<string, DateTime> _bannedIps = new();

    public void Ban(string ip, TimeSpan duration) =>
        _bannedIps[ip] = DateTime.UtcNow.Add(duration);

    public Result<DateTime> IsBanned(string ip)
    {
        if (_bannedIps.TryGetValue(ip, out var expiry))
        {
            if (DateTime.UtcNow < expiry) 
                return Result.Ok(expiry);
            _bannedIps.TryRemove(ip, out _);
        }
        return Result<DateTime>.Fail();
    }
}
