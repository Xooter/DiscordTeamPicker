using System.Collections.Generic;

namespace DiscordTeamPicker.Models;

public class Team
{
    public string Name { get; set; } = "TEAM";
    
    public List<DiscordUser> Users { get; set; }
}