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
        postsLimit = 6;
        filterMapItems = false;
        maxItemPrice = 2000000;
        useTaxSystem = false;
        taxPercentage = (decimal)12.5;
        useWebhooks = true;
        webhookURL = "";
        buyEmbedColor = "#5783DB";
        listEmbedColor = "#F1C40F";
        delistEmbedColor = "#E74C3C";

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
    public int postsLimit = 0;
    public int updateCacheMinutes = 0;
    public bool filterMapItems = false;
    public int maxItemPrice = 2000000;
    public bool useTaxSystem = false;
    public decimal taxPercentage = (decimal)12.5;
    public bool useWebhooks = true;
    public string webhookURL = string.Empty;
    public string buyEmbedColor = "#5783DB";
    public string listEmbedColor = "#F1C40F";
    public string delistEmbedColor = "#E74C3C";

    [XmlArrayItem("ItemID")]
    public List<ushort> blacklistedItems = new();

    public string dbServer;
    public string dbPort;
    public string dbUser;
    public string dbPassword;
    public string dbDatabase = "unturned";
    public string dbTablePrefix = "smarketplace_";
}