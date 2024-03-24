using Rocket.API;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace SeniorS.SMarketplace.Helpers;
public class MessageHelper
{
    private readonly SMarketplace Instance;
    private readonly Color defaultMessageColor;

    //internal readonly string Prefix = "<color=#f5d21f>[</color><color=#5fcca6>RacingSystem</color><color=#f5d21f>]</color>";

    public MessageHelper()
    {
        Instance = SMarketplace.Instance;
        defaultMessageColor = HexToColor(Instance.Configuration.Instance.hexDefaultMessagesColor);
    }

    public void Say(IRocketPlayer caller, string translationKey, bool error, params object[] values)
    {
        string message = FormatMessage(translationKey, values);

        UnturnedChat.Say(caller, $"{message}", error ? HexToColor("#F82302") : defaultMessageColor, true);
    }

    public void Broadcast(string translationKey, params object[] values)
    {
        string message = FormatMessage(translationKey, values);

        UnturnedChat.Say($"{message}", defaultMessageColor, true);
    }

    public void Hint(Player player, string translationKey, bool error, params object[] values)
    {
        string message = FormatMessage(translationKey, values);

        string hexColor = error ? "#F82302" : Instance.Configuration.Instance.hexDefaultMessagesColor;

        string finalMessage = $"<color={hexColor}>{message}</color>";

        player.ServerShowHint(finalMessage, 8f);
    }

    private Color HexToColor(string hex)
    {
        if (!ColorUtility.TryParseHtmlString(hex, out Color color))
        {
            Logger.LogError($"Could not convert {hex} to a Color.");
            return Color.white;
        }

        return color;
    }


    public string FormatMessage(string translationKey, params object[] values)
    {
        string baseMessage = Instance.Translate(translationKey, values);
        baseMessage = baseMessage.Replace("-=", "<").Replace("=-", ">");

        return baseMessage;
    }
}