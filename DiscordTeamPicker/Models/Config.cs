namespace DiscordTeamPicker.Models;

public class Config
{
    public string Token { get; set; } = "";
    
    public ulong CurrentChannelId {get; set; }
    
    public ulong TextChannelId { get; set; }
}