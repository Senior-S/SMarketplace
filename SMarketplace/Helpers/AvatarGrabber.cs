using Rocket.Core.Logging;
using Rocket.Core.Steam;
using Rocket.Core.Utils;
using System;
using System.Net;
using System.Xml;

namespace SeniorS.SMarketplace.Helpers;
public class AvatarGrabber
{
    public static string GetAvatar(ulong steamID)
    {
        string avatarURL = "https://i.imgur.com/zSuzXRZ.png";
        try
        {
            XmlDocument doc = new XmlDocument();

            using WebClient webClient = new WebClient();
            string xml = webClient.DownloadString($"http://steamcommunity.com/profiles/{steamID}?xml=1");
            doc.LoadXml(xml);

            avatarURL = doc["profile"]["avatarFull"]?.ParseUri().ToString();
        }
        catch (Exception ex)
        {
            TaskDispatcher.QueueOnMainThread(() =>
            {
                Logger.LogException(ex);
            });
        }

        return avatarURL;
    }
}
