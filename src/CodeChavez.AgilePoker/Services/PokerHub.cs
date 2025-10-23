using CodeChavez.AgilePoker.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace CodeChavez.AgilePoker.Services;

public class PokerHub : Hub
{
    private static readonly Dictionary<string, List<PlayerVote>> Sessions = new();

    public async Task Join(string sessionId, string playerName, string playerId)
    {
        if (!Sessions.ContainsKey(sessionId))
            Sessions[sessionId] = [];

        var players = Sessions[sessionId];

        var existing = players.FirstOrDefault(p => p.PlayerId == playerId);
        if (existing != null)
        {
            existing.ConnectionId = Context.ConnectionId;
            existing.Name = playerName;
            existing.Revealed = false;
            existing.Vote = string.Empty;
        }
        else
        {
            var httpContext = Context.GetHttpContext();
            var forwarded = httpContext?.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            var ip = forwarded ?? httpContext?.Connection.RemoteIpAddress?.ToString();

            players.Add(new PlayerVote
            {
                Name = playerName,
                ConnectionId = Context.ConnectionId,
                IpAddress = ip ?? "unknown",
                PlayerId = playerId
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

    public async Task EndSession(string sessionId)
    {
        if (Sessions.TryGetValue(sessionId, out var players))
        {
            foreach (var player in players)
            {
                await Clients.Client(player.ConnectionId).SendAsync("SessionEnded");
            }

            Sessions.Remove(sessionId);
        }
    }

}