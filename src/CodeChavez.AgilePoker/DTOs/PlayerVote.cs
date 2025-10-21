namespace CodeChavez.AgilePoker.DTOs;

public class PlayerVote
{
    public string Name { get; set; } = string.Empty;
    public string Vote { get; set; } = string.Empty;
    public bool HasVoted { get; set; } = false;
    public bool Revealed { get; set; } = false;
    public string ConnectionId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}
