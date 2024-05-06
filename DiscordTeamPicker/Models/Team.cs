using System.Collections.Generic;
using Discord.WebSocket;

namespace DiscordTeamPicker.Models;

public class Team
{
    public string Name { get; set; } = "TEAM";
    
    public List<DiscordUser> Users { get; set; }
    
    public SocketGuildChannel Channel { get; set; }
}