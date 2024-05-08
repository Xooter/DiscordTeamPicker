using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
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

    [ObservableProperty] SocketTextChannel? _messageTextChannel;

    private Config? Config { get; set; }
    [ObservableProperty] SocketGuildChannel? _currentChannel;
    [ObservableProperty] private Bitmap _guildAvatar;

    [ObservableProperty] private bool _isDiscordAvaible;
    
    [ObservableProperty] private bool _tokenDialogIsOpen;
    [ObservableProperty] private string _tokenValueInput;

    [ObservableProperty] private bool _errorDialogOpen;
    [ObservableProperty] private string _errorText = "";
    
    public ObservableCollection<SocketVoiceChannel> BotVoiceChanels { get; set; } = [];

    [ObservableProperty]
    private bool _badToken;

    
    public MainWindowViewModel()
    {
        var configManager = new SecretManager("config.json");
        Config = configManager.LoadConfig();
        
        if(Config == null)return;
        TokenValueInput = Config.Token;
        
        InitDiscordApi();
    }

    private async Task ClientOnLog(LogMessage arg)
    {
        if (arg.Exception != null && (arg.Exception.Message.Contains("Closed") ||  arg.Exception.Message.Contains("Unauthorized")) && !BadToken)
        {
            BadToken = true;
            IsDiscordAvaible = false;
            await _client.StopAsync();
            OpenErrorDialog("Invalid token");   
        }
    }

    private async void InitDiscordApi()
    {
        BadToken = false;

        if (Config.Token == null)
        {
            IsDiscordAvaible = false;
            return;
        }

        _client = new DiscordSocketClient();
        _client.Ready += ClientOnReady;
        _client.Log += ClientOnLog; 
        
        await _client.LoginAsync(TokenType.Bot, Config?.Token);
        await _client.StartAsync();
    }

    private async Task ClientOnReady()
    {
        IsDiscordAvaible = true;
        GetBotVoiceChannels();
        await RefreshUsersInChannel();
    }

    private  void GetBotVoiceChannels()
    {
        foreach (var guild in _client.Guilds)
        {
            var channels= guild.VoiceChannels;
            
            if(!channels.Any()) return;

            var orderedVoiceChannels = channels
                                       .OfType<SocketVoiceChannel>()
                                       .OrderBy(chan => chan.Position);    
            
            
            foreach (var chan in orderedVoiceChannels)
            {
                BotVoiceChanels.Add(chan);
            }

        }
        if(Config.CurrentChannelId == 0)
            CurrentChannel = BotVoiceChanels.FirstOrDefault();
    }

    partial void OnTokenValueInputChanged(string value)
    {
        Config.Token = value;
        var configManager = new SecretManager("config.json");
        configManager.SaveConfig(Config);
    }
    
    partial void OnCurrentChannelChanged(SocketGuildChannel? value)
    {
        if (value == null) return;
        var configManager = new SecretManager("config.json");
        Config.CurrentChannelId = value.Id;
        configManager.SaveConfig(Config);
    }

    partial void OnMessageTextChannelChanged(SocketTextChannel value)
    {
        var configManager = new SecretManager("config.json");
        Config.TextChannelId = value.Id;
        configManager.SaveConfig(Config);
    }

    [RelayCommand]
    async Task RefreshUsersInChannel()
    {
        if(Config.CurrentChannelId == 0) return;
        if (await _client.GetChannelAsync(Config.CurrentChannelId) is SocketVoiceChannel channel)
        {
            CurrentChannel = channel;
            GetUserInChannel();
            GetGuildChannels();
        }
        else
        {
            OpenErrorDialog("Error Searching for current Voice Channel");
        }
    }

    private async void GetGuildChannels()
    {
        
        var guild = CurrentChannel.Guild;
        var channels =  guild.Channels;
            
            
        GuildAvatar = await DownloadAvatar(guild.IconUrl);
            
        if(!channels.Any()) return;

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

        if(Config != null && Config.TextChannelId != 0 && await _client.GetChannelAsync(Config.TextChannelId) is SocketTextChannel channelText)
            MessageTextChannel = channelText;
        if (GuildTextChannels.Count > 0 && MessageTextChannel == null)
            MessageTextChannel = GuildTextChannels.First();
    }

    private async void GetUserInChannel()
    {
        if(Users.Any()) Users.Clear();

        if (CurrentChannel.Users is not IEnumerable<IGuildUser?> usersInChannel) 
        {
            OpenErrorDialog("There is users in the voice channel");
            return;
        } 

        if(!usersInChannel.Any()) return;
                
        var filteredUsers = usersInChannel.Where(x =>  x is { IsBot: false } &&
                                                       x.VoiceChannel?.Id == CurrentChannel.Id);
                
        foreach (var user in filteredUsers)
        {
            var activeUser = CurrentChannel.GetUser(user.Id);
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
            OpenErrorDialog("Error downloading the avatar");
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

            if(!Teams.Where(x=> x.Blocked).Any(x=> x.Users.Contains(user)))
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
    async Task ReturnAll()
    {
        foreach (var team in Teams)
        {
            foreach (var user in team.Users)
            {
                await Task.Delay(30);
                await user.User?.ModifyAsync(x => x.ChannelId = CurrentChannel.Id)!;
            }
        }
    }
    
    [RelayCommand]
    void BlockTeam(Team team)
    {
        team.Blocked = !team.Blocked;
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
        if (Teams.Count < 10)
        {
            var availableChannels = GuildVoiceChannels 
                                    .Where(chan => Teams.All(team => team.Channel != chan) && chan.Id != CurrentChannel.Id)  
                                    .ToList();

            SocketVoiceChannel? selectedChannel = availableChannels.FirstOrDefault(); 
            
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

    [RelayCommand]
    async Task SendMessage()
    {
        foreach (var team in Teams)
        {
            if(team.Users.Count == 0) continue;
            StringBuilder messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"## **{team.Name}**");

            for (int i = 0; i < team.Users.Count; i++)
            {
                var user = team.Users[i].User;
                
                messageBuilder.AppendLine($"{i+1}. {user.Mention} ({user.GlobalName})");
            }
            
            Debug.WriteLine(messageBuilder.ToString());
            await MessageTextChannel.SendMessageAsync(messageBuilder.ToString());
        } 
        
    }
    
    [RelayCommand]
    void OpenTokenDialog()
    {
        TokenDialogIsOpen = true;
    }

    partial void OnTokenDialogIsOpenChanged(bool value)
    {
        if (!value)
        {
            InitDiscordApi();
        }
    }

    private void OpenErrorDialog(string errorMessage)
    {
        IsDiscordAvaible = false;
        ErrorText = errorMessage;
        ErrorDialogOpen = true;
    }
}