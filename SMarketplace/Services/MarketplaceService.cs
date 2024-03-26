using fr34kyn01535.Uconomy;
using Rocket.Core.Logging;
using Rocket.Core.Utils;
using SDG.Unturned;
using SeniorS.SMarketplace.Models;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Instrumentation;
using System.Threading.Tasks;

namespace SeniorS.SMarketplace.Services;
public class MarketplaceService
{
    public List<MarketplaceItem> marketplaceItems { get; private set; }

    public MarketplaceService(List<MarketplaceItem> items)
    {
        this.marketplaceItems = items;
    }

    public List<MarketplaceItem> SearchCoincidences(string itemName)
    {
        if (itemName.Length < 1) return new();
        List<MarketplaceItem> items = marketplaceItems.Where(c => c.ItemName.ToLower().Contains(itemName.ToLower())).ToList();

        return items;
    }

    public async Task<bool> ListItem(MarketplaceItem item)
    {
        try
        {
            MySQLManager dbManager = new();

            int id = await dbManager.AddItem(item);
            dbManager.Dispose();

            item.ID = id;
            marketplaceItems.Add(item);

            return true;
        }
        catch (Exception ex)
        {
            TaskDispatcher.QueueOnMainThread(() =>
            {
                Logger.Log(ex);
            });
            return false;
        }
    }

    public async Task<bool> DelItem(MarketplaceItem item)
    {
        try
        {
            MySQLManager dbManager = new();

            bool removed = await dbManager.RemoveItem(item.ID);
            dbManager.Dispose();

            if (removed)
            {
                marketplaceItems.Remove(item);
            }

            return removed;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    public async Task<EError> TryBuyItem(MarketplaceItem item, Player buyer, bool isSellerOnline)
    {
        if (!marketplaceItems.Contains(item)) return EError.Item;

        uint balance = GetPlayerBalance(buyer);

        if (balance < item.Price)
        {
            return EError.Balance;
        }

        MySQLManager dbManager = new();
        bool removed = await dbManager.RemoveItem(item.ID);
        if (!removed)
        {
            return EError.Item;
        }
        marketplaceItems.Remove(item);
        
        await dbManager.AddLog(item, buyer.channel.owner.playerID.steamID.m_SteamID, isSellerOnline);
        dbManager.Dispose();

        TaskDispatcher.QueueOnMainThread(() =>
        {
            UpdatePlayerBalance(buyer, ((int)item.Price * -1));
            buyer.inventory.forceAddItem(item.GetItem(), true);

            Player sellerPlayer = PlayerTool.getPlayer(new CSteamID(item.SellerID));
            if(isSellerOnline && sellerPlayer != null)
            {
                UpdatePlayerBalance(sellerPlayer, (int)item.Price);
            }
        });

        return EError.None;
    }

    public List<MarketplaceItem> GetPlayerListedItems(ulong playerID)
    {
        List<MarketplaceItem> items = marketplaceItems.Where(c => c.SellerID == playerID).ToList();

        return items;
    }

    public List<MarketplaceItem> GetPageItems(int page)
    {
        return marketplaceItems.Skip(8 * page).Take(8).ToList();
    }

    private uint GetPlayerBalance(Player player)
    {
        if (SMarketplace.Instance.Configuration.Instance.useUconomy)
        {
            return (uint)Uconomy.Instance.Database.GetBalance(player.channel.owner.playerID.steamID.ToString());
        }

        return player.skills.experience;
    }

    public void UpdatePlayerBalance(Player player, int amount)
    {
        if (SMarketplace.Instance.Configuration.Instance.useUconomy)
        {
            Uconomy.Instance.Database.IncreaseBalance(player.channel.owner.playerID.steamID.ToString(), amount);

            return;
        }

        player.skills.ServerModifyExperience(amount);
    }
}
