using HarmonyLib;
using Rocket.API.Collections;
using Rocket.Core.Plugins;
using Rocket.Core.Utils;
using Rocket.Unturned.Player;
using SDG.Unturned;
using SeniorS.SMarketplace.Helpers;
using SeniorS.SMarketplace.Services;
using System.Threading.Tasks;
using Logger = Rocket.Core.Logging.Logger;

namespace SeniorS.SMarketplace;
public class SMarketplace : RocketPlugin<Configuration>
{
    public static SMarketplace Instance;
    public MessageHelper _msgHelper;
    public MarketplaceService marketplaceService;

    internal Harmony harmony;
    internal string connectionString;

    internal bool Outdated = false;

    protected override void Load()
    {
        Instance = this;
        _msgHelper = new();
        connectionString = $"Server={Configuration.Instance.dbServer};Port={Configuration.Instance.dbPort};Database={Configuration.Instance.dbDatabase};Uid={Configuration.Instance.dbUser};Pwd={Configuration.Instance.dbPassword};";
        harmony = new ("com.seniors.smarketplace");
        harmony.PatchAll(this.Assembly);

        Provider.onEnemyConnected += OnEnemyConnected;
        Level.onLevelLoaded += OnLevelLoaded;

        Task.Run(async () =>
        {
            string latestVersion = await VersionHelper.GetLatestVersion();

            if(this.Assembly.GetName().Version.ToString() != latestVersion)
            {
                this.Outdated = true;
                TaskDispatcher.QueueOnMainThread(() =>
                {
                    Logger.Log("OUTDATED PLUGIN VERSION!");
                    Logger.Log("OUTDATED PLUGIN VERSION!");
                    Logger.Log("OUTDATED PLUGIN VERSION!");
                    Logger.Log("Please download the latest version to get the newest features and bug fixes!");
                    Logger.Log("https://unturnedstore.com/products/1618");
                });
            }
        });

        Logger.Log($"SMarketplace v{this.Assembly.GetName().Version}");
        Logger.Log("<<SSPlugins>>");
    }

    private void OnLevelLoaded(int level)
    {
        marketplaceService = new(Configuration.Instance.updateCacheMinutes, Configuration.Instance.filterMapItems);
    }

    private void OnEnemyConnected(SteamPlayer player)
    {
        MySQLManager dbManager = new();
        Task.Run(async () =>
        {
            int pendingPaid = await dbManager.GetPendingPaids(player.playerID.steamID.m_SteamID);
            if(pendingPaid > 0)
            {
                await dbManager.UpdatePendingPaids(player.playerID.steamID.m_SteamID);
                TaskDispatcher.QueueOnMainThread(() =>
                {
                    UnturnedPlayer user = UnturnedPlayer.FromSteamPlayer(player);
                    marketplaceService.UpdatePlayerBalance(player.player, pendingPaid);
                    _msgHelper.Say(user, "success_pending_paid", false, pendingPaid);
                });
            }
            dbManager.Dispose();
        });

        // A lot of server owners don't really read logs so adding this will assure owners always update to the latest version.
        if (Outdated && player.isAdmin)
        {
            ChatManager.say(player.playerID.steamID, "SMarketplace > Hi, you're using a outdated version of this plugin, please update it to the latest version to get the newest features and bug fixes!", _msgHelper.HexToColor("#F82302"), true);
        }
    }

    public override TranslationList DefaultTranslations => new() 
    {
        { "ui_error_balance", "You need at least {0} to buy this item!" },
        { "ui_error_item", "Sorry, this item isn't available on the market anymore!" },
        { "ui_error_delist", "There was an error while delisted this item, please try again!" },
        { "ui_message_delist", "Your {0} have been successfully delisted!" },
        { "ui_name_title", "Item:" },
        { "ui_durability_title", "Durability:" },
        { "ui_amount_title", "Amount:" },
        { "ui_seller_format", "Seller: {0}" },
        { "ui_name_format", "{0} ({1})" },
        { "ui_price_format", "${0}" },
        { "ui_search_placeholder", "Enter item name..." },
        { "ui_totallistings_title", "Total Listings" },
        { "ui_totalsellers_title", "Total Sellers" },
        { "ui_totalbought_title", "Total Bought" },
        { "ui_upload_price_placeholder", "Enter price..." },
        { "ui_selllog_format", "-=b=-{0}-=/b=- bought a -=i=-{1}-=/i=- from -=b=-{2}-=/b=- for ${3}" },
        { "error_upload_max", "Oops, you can't upload more than 28 items at a time!" },
        { "error_upload_min", "Oops, you need to atlest put 1 item on the box!" },
        { "error_upload_incomplete", "You can't change of tabs until you submit or cancel!" },
        { "error_price", "The price must be greater than 0!" },
        { "error_equipment", "You need to have the item you wanna sell on hand!" },
        { "error_item", "An item with name {0} wasn't found in your inventory!" },
        { "error_blacklist", "Sorry! This item can't be listed on the marketplace!" },
        { "error_list", "An error has occurred while listing your item, try again later!" },
        { "error_required_item", "Sorry! You need a {0} to open the marketplace!" },
        { "info_item", "Your item is being listed, wait a moment please!" },
        { "success_item", "Your {0} is now being sold at the marketplace!" },
        { "success_upload", "Your items are now being sold at the marketplace!" },
        { "success_buy", "You have successfully bought a {0} from the marketplace!" },
        { "success_pending_paid", "You have made ${0} while you were offline!" }
    };

    protected override void Unload()
    {
        Instance = null;
        _msgHelper = null;
        marketplaceService.Dispose();

        harmony.UnpatchAll(harmony.Id); 
        Provider.onEnemyConnected -= OnEnemyConnected;
        Level.onLevelLoaded -= OnLevelLoaded;

        Logger.Log("<<SSPlugins>>");
    }
}