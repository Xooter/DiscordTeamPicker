using Avalonia.Media.Imaging;
using Discord.WebSocket;

namespace DiscordTeamPicker.Models;

public class DiscordUser
{
   public Bitmap Avatar { get; set; }

   public SocketGuildUser? User { get; set; }

}