using CodeChavez.AgilePoker.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace CodeChavez.AgilePoker.Services;

public class PokerHub : Hub
{
    private static readonly Dictionary<string, Dictionary<string, PlayerVote>> Sessions = new();

    public async Task Join(string sessionId, string playerName)
    {
        if (!Sessions.TryGetValue(sessionId, out var session))
        {
            session = new();
            Sessions[sessionId] = session;
        }

        session[Context.ConnectionId] = new PlayerVote { Name = playerName };
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        await Clients.Group(sessionId).SendAsync("PlayersUpdated", session.Values);
    }

    public async Task Vote(string sessionId, string vote)
    {
        if (Sessions.TryGetValue(sessionId, out var session) && session.TryGetValue(Context.ConnectionId, out var player))
        {
            player.Vote = vote;
            player.HasVoted = true;
            await Clients.Group(sessionId).SendAsync("PlayersUpdated", session.Values);
        }
    }

    public async Task Reveal(string sessionId)
    {
        if (Sessions.TryGetValue(sessionId, out var session))
        {
            foreach (var player in session.Values)
                player.Revealed = true;


            await Clients.Group(sessionId).SendAsync("PlayersUpdated", session.Values);
        }
    }

    public async Task Reset(string sessionId)
    {
        if (Sessions.TryGetValue(sessionId, out var session))
        {
            foreach (var player in session.Values)
            {
                player.Vote = string.Empty;
                player.HasVoted = false;
                player.Revealed = false;
            }

            await Clients.Group(sessionId).SendAsync("PlayersUpdated", session.Values);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var session in Sessions.Values)
        {
            if (session.Remove(Context.ConnectionId))
                break;
        }

        foreach (var (id, players) in Sessions)
        {
            await Clients.Group(id).SendAsync("PlayersUpdated", players.Values);
        }
    }
}