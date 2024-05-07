using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Discord.WebSocket;
using DiscordTeamPicker.ViewModels;

namespace DiscordTeamPicker.Models;

public partial class Team : ViewModelBase
{
    public string Name { get; set; } = "TEAM";

    public ObservableCollection<DiscordUser> Users { get; set; } = [];
    
    public SocketGuildChannel Channel { get; set; }

    [ObservableProperty] private bool _blocked;
}