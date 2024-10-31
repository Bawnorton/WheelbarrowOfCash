using BepInEx.Configuration;

namespace WheelbarrowOfCash;

public class PluginConfig
{
    public static ConfigEntry<float> Threshold { get; private set; }

    public static void Init(ConfigFile config)
    {
        Threshold = config.Bind("Settings", "WheelbarrowThreshold", 20000f, "The min balance that a customer has for them to spawn with a wheelbarrow");
    }
}