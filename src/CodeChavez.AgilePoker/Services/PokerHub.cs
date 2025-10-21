using CodeChavez.AgilePoker.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace CodeChavez.AgilePoker.Services;

public class PokerHub : Hub
{
    private static readonly Dictionary<string, List<PlayerVote>> Sessions = new();

    public async Task Join(string sessionId, string playerName)
    {
        var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrEmpty(ip))
        {
            await Clients.Caller.SendAsync("Error", "Could not determine IP address.");
            return;
        }

        if (!Sessions.ContainsKey(sessionId))
            Sessions[sessionId] = [];

        var players = Sessions[sessionId];

        var existing = players.FirstOrDefault(p => p.IpAddress == ip);
        if (existing != null)
        {
            // Option A: Replace old connection
            existing.ConnectionId = Context.ConnectionId;
            existing.Name = playerName;
            existing.Revealed = false;
            existing.Vote = string.Empty;
        }
        else
        {
            // Option B: Allow new connection if IP is not used
            players.Add(new PlayerVote
            {
                Name = playerName,
                ConnectionId = Context.ConnectionId,
                IpAddress = ip
            });
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        await Clients.Group(sessionId).SendAsync("PlayersUpdated", players);
    }

    public async Task Vote(string sessionId, string vote)
    {
        if (Sessions.TryGetValue(sessionId, out var players))
        {
            var player = players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player != null)
            {
                player.Vote = vote;
                player.HasVoted = true;
                await Clients.Group(sessionId).SendAsync("PlayersUpdated", players);
            }
        }
    }

    public async Task Reveal(string sessionId)
    {
        if (Sessions.TryGetValue(sessionId, out var players))
        {
            foreach (var player in players)
                player.Revealed = true;

            await Clients.Group(sessionId).SendAsync("PlayersUpdated", players);
        }
    }

    public async Task Hide(string sessionId)
    {
        if (Sessions.TryGetValue(sessionId, out var players))
        {
            foreach (var player in players)
                player.Revealed = false;

            await Clients.Group(sessionId).SendAsync("PlayersUpdated", players);
        }
    }

    public async Task Reset(string sessionId)
    {
        if (Sessions.TryGetValue(sessionId, out var players))
        {
            foreach (var player in players)
            {
                player.Vote = string.Empty;
                player.HasVoted = false;
                player.Revealed = false;
            }

            await Clients.Group(sessionId).SendAsync("PlayersUpdated", players);
        }
    }

    public async Task UpdatePlayerName(string sessionId, string newName)
    {
        if (Sessions.TryGetValue(sessionId, out var players))
        {
            var player = players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player != null)
            {
                player.Name = newName;
                await Clients.Group(sessionId).SendAsync("PlayersUpdated", players);
            }
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var (sessionId, players) in Sessions)
        {
            var player = players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player != null)
            {
                players.Remove(player);
                await Clients.Group(sessionId).SendAsync("PlayersUpdated", players);
                break;
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}