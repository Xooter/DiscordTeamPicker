using System.Collections.Generic;
using System.Collections.ObjectModel;
using Discord.WebSocket;

namespace DiscordTeamPicker.Models;

public class Team
{
    public string Name { get; set; } = "TEAM";

    public ObservableCollection<DiscordUser> Users { get; set; } = [];
    
    public SocketGuildChannel Channel { get; set; }
    
    public bool Blocked { get; set; }
}