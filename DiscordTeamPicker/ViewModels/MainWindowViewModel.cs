using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Discord;
using Discord.WebSocket;
using DiscordTeamPicker.Helpers;
using DiscordTeamPicker.Models;

namespace DiscordTeamPicker.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private DiscordSocketClient _client =new(); 
    public ObservableCollection<DiscordUser> Users { get; set; } = [];
    public ObservableCollection<Team> Teams { get; set; } = [];

    public ObservableCollection<SocketVoiceChannel> GuildVoiceChannels { get; set; } = [];
    public ObservableCollection<SocketTextChannel> GuildTextChannels { get; set; } = [];

    [ObservableProperty] SocketTextChannel _messageTextChannel;

    private Config? config { get; set; }
    [ObservableProperty] private string _currentChannelId;
    [ObservableProperty] private Bitmap _guildAvatar;

    public MainWindowViewModel()
    {
        InitDiscordApi();
    }

    private async void InitDiscordApi()
    {
        _client = new DiscordSocketClient();

        var configManager = new SecretManager<Config?>("config.json");
        config = configManager.LoadConfig();

        CurrentChannelId = config.CurrentChannelId;
        
        await _client.LoginAsync(TokenType.Bot, config?.Token);
        await _client.StartAsync();
        _client.Ready += ClientOnReady;
    }

    private Task ClientOnReady()
    {
        RefreshUsersInChannel();
        return Task.CompletedTask;
    }

    partial void OnCurrentChannelIdChanged(string value)
    {
        var configManager = new SecretManager<Config?>("config.json");
        config.CurrentChannelId = value;
        configManager.SaveConfig(config);
    }

    [RelayCommand]
    void RefreshUsersInChannel()
    {
        if (ulong.TryParse(config.CurrentChannelId, out ulong channelId))
        {
            GetUserInChannel(channelId);
            GetGuildChannels(channelId);
        }
    }

    private async void GetGuildChannels(ulong channelId)
    {

        if (await _client.GetChannelAsync(channelId) is IGuildChannel channel)
        {
            var guild = channel.Guild;
            var channels = await guild.GetChannelsAsync();
            
            GuildAvatar = await DownloadAvatar(guild.IconUrl);

            var orderedTextChannels = channels
                                      .OfType<SocketTextChannel>()
                                      // ultra mega rebuscado
                                      .Where(x=> (x as SocketVoiceChannel)?.Bitrate == null)
                                      .OrderBy(chan => chan.Position);    
            
            var orderedVoiceChannels = channels
                                       .OfType<SocketVoiceChannel>()
                                       .OrderBy(chan => chan.Position);    
            
            
            foreach (var chan in orderedVoiceChannels)
            {
                GuildVoiceChannels.Add(chan);
            }
            
            foreach (var chan in orderedTextChannels)
            {
                GuildTextChannels.Add(chan);
            }

            if (GuildTextChannels.Count > 0 )
                MessageTextChannel = GuildTextChannels.First();
        }
    }

    private async void GetUserInChannel(ulong channelId)
    {
        Users.Clear();
        if (await _client.GetChannelAsync(channelId) is IGuildChannel channel)
        {
            try
            {
                Users.Clear();
                    
                var usersInChannel = await channel.GetUsersAsync().FlattenAsync();
                var filteredUsers = usersInChannel.Where(x =>  x is { IsBot: false } &&
                                                               x.VoiceChannel?.Id == channelId);
                
                foreach (var user in filteredUsers)
                {
                    var activeUser = await channel.GetUserAsync(user.Id);
                    if (activeUser is { IsBot: false })
                    {
                        string url = "https://cdn.discordapp.com/avatars/" +
                                     user.Id +
                                     "/" +
                                     user.AvatarId +
                                     ".png?size=128 ";
                        DiscordUser newUser = new DiscordUser()
                        {
                            User = activeUser  as SocketGuildUser,
        
                            Avatar = await DownloadAvatar(url)
                        };
                       
                        Users.Add(newUser);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
    private async Task<Bitmap> DownloadAvatar(string url)
    {
        
        try
        {
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(url))
            using (HttpContent content = response.Content)
            {
                if (response.IsSuccessStatusCode)
                {
                    using (var stream = await content.ReadAsStreamAsync())
                    {
                        return new Bitmap(stream);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al descargar la imagen: {ex.Message}");
        }

        return null;
    }
    
    [RelayCommand]
    private void Shuffle()
    {
        foreach (var team in Teams)
        {
            if (!team.Blocked)
            {
                team.Users.Clear();
            }
        }
        
        var usersShuffled = ShuffleList(Users);
        int teamIndex = 0;
        var eligibleTeams = Teams.Where(team => !team.Blocked).ToList();
        
        foreach (var user in usersShuffled)
        {
            var currentTeam = eligibleTeams[teamIndex];

            currentTeam.Users.Add(user);

            teamIndex = (teamIndex + 1) % eligibleTeams.Count;
        }
    }    
    
    private ObservableCollection<T> ShuffleList<T>(ObservableCollection<T> list)
    {
        Random rng = new Random();
        ObservableCollection<T> newList = new ObservableCollection<T>(list);
        
        int n = newList.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (newList[k], newList[n]) = (newList[n], newList[k]);
        }
        return newList;
    }
    
    [RelayCommand]
    async Task MuteTeam(Team team)
    {
        foreach (var user in team.Users)
        {
            await user.User?.ModifyAsync(x => x.Mute = true)!;
        }
    }

    [RelayCommand]
    void MuteAll()
    {
        foreach (var team in Teams)
        {
           MuteTeam(team);
        }
    }
    
    [RelayCommand]
    async Task MoveTeam(Team team)
    {
        foreach (var user in team.Users)
        {
            await user.User?.ModifyAsync(x => x.ChannelId = team.Channel.Id)!;
        }
    }

    [RelayCommand]
    void MoveAll()
    {
        foreach (var team in Teams)
        {
            MoveTeam(team);
        }
    }
    
    [RelayCommand]
    void NewTeam()
    {
        if (Teams.Count < 5)
        {
            var availableChannels = GuildVoiceChannels 
                                   .Where(chan => Teams.All(team => team.Channel != chan) && chan.Id.ToString() != CurrentChannelId)  
                                   .ToList();

            SocketGuildChannel selectedChannel = availableChannels.FirstOrDefault(); 
            
            Teams.Add(new Team()
            {
                Channel = selectedChannel
            });
        }
    }

    [RelayCommand]
    void RemoveTeam(Team team)
    {
        Teams.Remove(team);
    }
}