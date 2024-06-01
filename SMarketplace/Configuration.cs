using Rocket.API;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SeniorS.SMarketplace;
public class Configuration : IRocketPluginConfiguration
{
    public void LoadDefaults()
    {
        hexDefaultMessagesColor = "#2BC415";

        requiredItemToMarketplace = 0;

        uiEffectID = 51300;
        uiEffectKey = 31300;

        useUconomy = true;
        displayAttachments = false;
        iconsCDN = "https://cdn.lyhme.gg/items/{0}.png";
        updateCacheMinutes = 0;
        filterMapItems = false;

        blacklistedItems = new()
        {
            519
        };

        dbServer = "127.0.0.1";
        dbPort = "3306";
        dbUser = "root";
        dbPassword = "toor";
        dbDatabase = "unturned";
        dbTablePrefix = "smarketplace_";
    }

    public string hexDefaultMessagesColor;
    public ushort requiredItemToMarketplace = 0;

    public ushort uiEffectID;
    public short uiEffectKey;

    public bool useUconomy;
    public bool displayAttachments = false; // Attachments will be optional due some servers may experience some ping when searching the attachments info.
    public string iconsCDN;
    public int updateCacheMinutes = 0;
    public bool filterMapItems = false;

    [XmlArrayItem("ItemID")]
    public List<ushort> blacklistedItems = new();

    public string dbServer;
    public string dbPort;
    public string dbUser;
    public string dbPassword;
    public string dbDatabase;
    public string dbTablePrefix = "smarketplace_";
}