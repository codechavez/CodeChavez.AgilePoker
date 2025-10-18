namespace CodeChavez.AgilePoker.DTOs;

public class PlayerVote
{
    public string Name { get; set; } = string.Empty;
    public string Vote { get; set; } = string.Empty;
    public bool HasVoted { get; set; }
    public bool Revealed { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
}
