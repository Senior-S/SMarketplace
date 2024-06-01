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

    internal string connectionString;

    protected override void Load()
    {
        Instance = this;
        _msgHelper = new();
        connectionString = $"Server={Configuration.Instance.dbServer};Port={Configuration.Instance.dbPort};Database={Configuration.Instance.dbDatabase};Uid={Configuration.Instance.dbUser};Pwd={Configuration.Instance.dbPassword};";

        Provider.onEnemyConnected += OnEnemyConnected;
        Level.onLevelLoaded += OnLevelLoaded;

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
    }

    public override TranslationList DefaultTranslations => new() 
    {
        { "ui_error_balance", "You need at least {0} to buy this item!" },
        { "ui_error_item", "Sorry, this item isn't available on the market anymore!" },
        { "ui_error_delist", "There was an error while delisted this item, please try again!" },
        { "ui_message_delist", "Your {0} have been successfully delisted!" },
        { "ui_name_title", "Item:" },
        { "ui_price_title", "Price:" },
        { "ui_durability_title", "Durability:" },
        { "ui_amount_title", "Amount:" },
        { "ui_name_format", "{0} ({1})" },
        { "ui_price_format", "${0}" },
        { "ui_search_placeholder", "Enter item name..." },
        { "error_price", "The price must be greater than 0!" },
        { "error_equipment", "You need to have the item you wanna sell on hand!" },
        { "error_item", "An item with name {0} wasn't found in your inventory!" },
        { "error_blacklist", "Sorry! This item can't be listed on the marketplace!" },
        { "error_list", "An error has occurred while listing your item, try again later!" },
        { "error_required_item", "Sorry! You need a {0} to open the marketplace!" },
        { "info_item", "Your item is being listed, wait a moment please!" },
        { "success_item", "Your {0} is now being sold at the marketplace!" },
        { "success_buy", "You have successfully bought a {0} from the marketplace!" },
        { "success_pending_paid", "You have made ${0} while you were offline!" }
    };

    protected override void Unload()
    {
        Instance = null;
        _msgHelper = null;
        marketplaceService.Dispose();

        Provider.onEnemyConnected -= OnEnemyConnected;
        Level.onLevelLoaded -= OnLevelLoaded;

        Logger.Log("<<SSPlugins>>");
    }
}