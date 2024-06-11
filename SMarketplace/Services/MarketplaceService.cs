using fr34kyn01535.Uconomy;
using Rocket.Core.Logging;
using Rocket.Core.Utils;
using SDG.Unturned;
using SeniorS.SMarketplace.Models;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SDG.Provider.SteamGetInventoryResponse;

namespace SeniorS.SMarketplace.Services;
public class MarketplaceService : IDisposable
{
    public List<MarketplaceItem> marketplaceItems { get; private set; }

    private readonly Timer _timer;
    private readonly bool _filterMapItems;

    public Dictionary<ushort, string> distinctItems { get; private set; }
    
    public MarketplaceService(int cacheUpdateDelay, bool filterMapItems)
    {
        _timer = new Timer(CheckExpiredItems, null, TimeSpan.Zero, cacheUpdateDelay > 0 ? TimeSpan.FromMinutes(cacheUpdateDelay) : TimeSpan.FromMinutes(45));
        _filterMapItems = filterMapItems;
    }

    private async void CheckExpiredItems(object state)
    {
        MySQLManager dbManager = new();
        dbManager.Init();
        List<MarketplaceItem> items = await dbManager.GetItems();
        distinctItems = await GetDistinctItems();
        dbManager.Dispose();

        if (_filterMapItems)
        {
            FilterMapItems(items);
            return;
        }

        marketplaceItems = items;
    }

    // Function added for servers running the same database in multiple servers with different maps
    // This function tries to remove items that don't exists in this server or map
    private void FilterMapItems(List<MarketplaceItem> items)
    {
        MarketplaceItem[] itemsCopy = new MarketplaceItem[items.Count];
        items.CopyTo(itemsCopy);

        TaskDispatcher.QueueOnMainThread(() =>
        {
            List<ushort> existingIds = new();
            itemsCopy.ToList().ForEach(c =>
            {
                if (existingIds.Contains(c.ItemID)) return;

                var asset = Assets.find(EAssetType.ITEM, c.ItemID);
                if (asset != null && asset.assetCategory != EAssetType.SKIN)
                {
                    existingIds.Add(c.ItemID);
                    return;
                }

                items.Remove(c);
            });

            marketplaceItems = items;
        });
    }

    private async Task<Dictionary<ushort, string>> GetDistinctItems()
    {
        MySQLManager dbManager = new();
        Dictionary<ushort, string> items = await dbManager.GetDistinctItems();
        return items;
    }

    public List<MarketplaceItem> SearchCoincidences(string itemName)
    {
        if (itemName.Length < 1) return new();
        List<MarketplaceItem> items = marketplaceItems.Where(c => c.ItemName.ToLower().Contains(itemName.ToLower())).ToList();

        return items;
    }

    public List<MarketplaceItem> FilterItems(ushort itemID)
    {
        List<MarketplaceItem> items = marketplaceItems.Where(c => c.ItemID == itemID).ToList();

        return items;
    }

    public async Task<bool> ListMultipleItems(List<MarketplaceItem> items)
    {
        try
        {
            MySQLManager dbManager = new();

            // It uploads one item at a time due I required the exact ID of the item added.
            // I can technically get the possible ID getting the last ID before submit but it can lead to unexpected behaviours.
            foreach (MarketplaceItem item in items) 
            {
                int id = await dbManager.AddItem(item);
                item.ID = id;
                marketplaceItems.Add(item);
            }

            dbManager.Dispose();
            distinctItems = await GetDistinctItems();
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

    public async Task<bool> ListItem(MarketplaceItem item)
    {
        try
        {
            MySQLManager dbManager = new();

            int id = await dbManager.AddItem(item);
            dbManager.Dispose();

            item.ID = id;
            marketplaceItems.Add(item);

            distinctItems = await GetDistinctItems();
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

            distinctItems = await GetDistinctItems();
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
        
        await dbManager.AddLog(item, buyer.channel.owner.playerID.steamID.m_SteamID, buyer.channel.owner.playerID.characterName, isSellerOnline);
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

        distinctItems = await GetDistinctItems();
        return EError.None;
    }

    public async Task<string> GetFormattedLogs()
    {
        MySQLManager dbManager = new();
        List<SellLog> logs = await dbManager.GetLatestLogs();

        string formattedLogs = "";
        if(logs.Count > 0)
        {
            logs.ForEach(log =>
            {
                formattedLogs += SMarketplace.Instance._msgHelper.FormatMessage("ui_selllog_format", log.BuyerName, log.ItemName, log.SellerName, log.ItemPrice) + Environment.NewLine;
            });

            formattedLogs = formattedLogs.Remove(formattedLogs.Length - Environment.NewLine.Length, Environment.NewLine.Length);
        }
        

        return formattedLogs;
    }

    public List<MarketplaceItem> GetPlayerListedItems(ulong playerID)
    {
        List<MarketplaceItem> items = marketplaceItems.Where(c => c.SellerID == playerID).ToList();

        return items;
    }

    public async Task<long> GetTotalBought()
    {
        MySQLManager dbManager = new();
        long totalBought = await dbManager.GetTotalLogs();
        dbManager.Dispose();

        return totalBought;
    }

    public int GetTotalSellers()
    {
        return marketplaceItems.Select(c => c.SellerID).Distinct().Count();
    }

    public List<MarketplaceItem> GetPageItems(int page, ushort itemID)
    {
        List<MarketplaceItem> items = FilterItems(itemID);

        return items.Skip(5 * page).Take(5).ToList();
    }

    public List<KeyValuePair<ushort, string>> GetPageFilter(int page)
    {
        return distinctItems.Skip(20 * page).Take(20).ToList();
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

    public void Dispose()
    {
        _timer.Dispose();
        marketplaceItems.Clear();
    }
}
