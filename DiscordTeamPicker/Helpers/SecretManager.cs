using System;
using System.IO;
using System.Text.Json;

namespace DiscordTeamPicker.Helpers;

public class SecretManager<T>
{
    private readonly string configPath;

    public SecretManager(string configFileName)
    {
        string appDirectory = AppContext.BaseDirectory;

        configPath = Path.Combine(appDirectory, configFileName);
    }

    
    public T? LoadConfig()
    {
        if (!File.Exists(configPath))
        {
            return default(T);
        }

        var jsonString = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<T>(jsonString);
    }

    public void SaveConfig(T config)
    {
        var jsonString = JsonSerializer.Serialize(config);
        File.WriteAllText(configPath, jsonString);
    } 
}