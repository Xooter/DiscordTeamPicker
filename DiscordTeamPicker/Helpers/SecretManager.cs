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
        T config;
        if (!File.Exists(configPath))
        {
            config = Activator.CreateInstance<T>();
            SaveConfig(config); 
        }
        else
        {
            var jsonString = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<T>(jsonString);
        }
        return config;
    }

    public void SaveConfig(T config)
    {
        var jsonString = JsonSerializer.Serialize(config);
        File.WriteAllText(configPath, jsonString);
    } 
}