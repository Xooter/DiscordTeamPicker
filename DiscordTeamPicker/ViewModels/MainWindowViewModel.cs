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
            var guild = channel.Guild;


            if (guild != null)
            {
                var usersInChannel = channel.GetUsersAsync().FlattenAsync();
                var filteredUsers = usersInChannel.Result
                                                  .Where(x => !x.IsBot &&
                                                              x.VoiceChannel.Id == channelId);
                
                foreach (var user in filteredUsers)
                {
                    var activeUser = channel.GetUserAsync(user.Id);
                    if (activeUser.Result is { IsBot: false })
                    {
        string url = "https://cdn.discordapp.com/avatars/" +
                     user.Id +
                     "/" +
                     user.AvatarId +
                     ".png?size=128 ";
                        DiscordUser newUser = new DiscordUser()
                        {
                            User = activeUser.Result  as SocketGuildUser,
                            
        
                            Avatar = await DownloadAvatar(url)
                        };
                       
                        Users.Add(newUser);
                    }
                }
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
    
    // async void ShuffleUsers()
    // {
    // var checkedItems = UsersList.CheckedItems;
    //
    // List<IUser?> CheckedUsers = users.Where(x => checkedItems.Contains(x.Username)).ToList();
    //
    // ClearLists();
    //
    // Random rnd = new Random();
    // ListTeam1 = CheckedUsers.OrderBy(x => rnd.Next()).Take(CheckedUsers.Count / 2).ToList();
    // ListTeam2 = CheckedUsers.Except(ListTeam1).ToList();
    // foreach (var item in ListTeam1)
    // {
    //     Team1.Items.Add(item.Username);
    // }
    //
    // foreach (var item in ListTeam2)
    // {
    //     Team2.Items.Add(item.Username);
    // }
    // }
    
    // async void MoveParticipants(List<IUser?> team, ulong channelToMove)
    // {
    //     var channel = await _client.GetChannelAsync(channelToMove);
    //     foreach (var item in team)
    //     {
    //         IGuildUser? guildUser = item as IGuildUser;
    //         Thread.Sleep(50);
    //         await guildUser.ModifyAsync(x => x.ChannelId = channel.Id);
    //     }
    // }
    
    [RelayCommand]
    void NewTeam()
    {
        if (Teams.Count < 5)
        {
            var availableChannels =GuildVoiceChannels 
                                   .Where(chan => Teams.All(team => team.Channel != chan))  
                                   .ToList();

            SocketGuildChannel selectedChannel = availableChannels.FirstOrDefault(); 
            
            Teams.Add(new Team()
            {
                Users = Users.ToList(),
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