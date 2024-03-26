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
        iconsCDN = "https://cdn.lyhme.gg/items/{0}.png";

        blacklistedItems = new()
        {
            519
        };

        dbServer = "127.0.0.1";
        dbPort = "3306";
        dbUser = "root";
        dbPassword = "toor";
        dbDatabase = "unturned";
    }

    public string hexDefaultMessagesColor;

    public ushort requiredItemToMarketplace = 0;

    public ushort uiEffectID;
    public short uiEffectKey;

    public bool useUconomy;
    public string iconsCDN;

    [XmlArrayItem("ItemID")]
    public List<ushort> blacklistedItems = new();

    public string dbServer;
    public string dbPort;
    public string dbUser;
    public string dbPassword;
    public string dbDatabase;
}