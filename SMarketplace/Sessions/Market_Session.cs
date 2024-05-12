using Rocket.Core.Utils;
using Rocket.Unturned.Player;
using SDG.NetTransport;
using SDG.Unturned;
using SeniorS.SMarketplace.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace SeniorS.SMarketplace.Sessions;
public class Market_Session : MonoBehaviour
{
    private Player player;
    private SMarketplace Instance;

    private short keyID;
    private ETab currentTab;

    private string searchInputFieldText = "";

    private int storeCurrentPage = 0;
    private int inventoryCurrentPage = 0;
    private int searchCurrentPage = 0;

    private List<MarketplaceItem> playerListedItems;
    private List<MarketplaceItem> pageItems;

    private MarketplaceItem infoItem;

    public void Init(Player player)
    {
        this.player = player;
        Instance = SMarketplace.Instance;
        keyID = Instance.Configuration.Instance.uiEffectKey;
        playerListedItems = Instance.marketplaceService.GetPlayerListedItems(this.player.channel.owner.playerID.steamID.m_SteamID);

        EffectManager.onEffectButtonClicked += OnEffectButtonClicked;
        EffectManager.onEffectTextCommitted += OnEffectTextCommitted;
        Provider.onEnemyDisconnected += OnEnemyDisconnected;

        player.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
        player.enablePluginWidgetFlag(EPluginWidgetFlags.NoBlur);
        EffectManager.sendUIEffect(Instance.Configuration.Instance.uiEffectID, keyID, player.channel.owner.transportConnection, true);
        ChangeTab(ETab.Store);
        EffectManager.sendUIEffectText(keyID, player.channel.owner.transportConnection, true, "Canvas/Background/Info_Tab/Base/Content_Background/Container/Child/Item_ID_Title", Instance._msgHelper.FormatMessage("ui_id_title"));
        EffectManager.sendUIEffectText(keyID, player.channel.owner.transportConnection, true, "Canvas/Background/Info_Tab/Base/Content_Background/Container/Child/Item_Name_Title", Instance._msgHelper.FormatMessage("ui_name_title"));
        EffectManager.sendUIEffectText(keyID, player.channel.owner.transportConnection, true, "Canvas/Background/Info_Tab/Base/Content_Background/Container/Child/Item_Price_Title", Instance._msgHelper.FormatMessage("ui_price_title"));
        EffectManager.sendUIEffectText(keyID, player.channel.owner.transportConnection, true, "Canvas/Background/Info_Tab/Base/Content_Background/Container/Child/Item_Durability_Title", Instance._msgHelper.FormatMessage("ui_durability_title"));
        EffectManager.sendUIEffectText(keyID, player.channel.owner.transportConnection, true, "Canvas/Background/Info_Tab/Base/Content_Background/Container/Child/Item_Amount_Title", Instance._msgHelper.FormatMessage("ui_amount_title"));
        EffectManager.sendUIEffectText(keyID, player.channel.owner.transportConnection, true, "Canvas/Background/Search_Tab/Search_InputField/Text Area/SearchPlaceholder", Instance._msgHelper.FormatMessage("ui_search_placeholder"));
    }

    private void OnEnemyDisconnected(SteamPlayer steamPlayer)
    {
        if(player.channel.owner.playerID.steamID != steamPlayer.playerID.steamID)
        {
            return;
        }

        Close();
    }

    private void OnEffectTextCommitted(Player player, string buttonName, string text)
    {
        if (player.channel.owner.playerID.steamID != this.player.channel.owner.playerID.steamID)
        {
            return;
        }

        if(buttonName == "Search_InputField")
        {
            this.searchInputFieldText = text;
        }
    }

    private void OnEffectButtonClicked(Player player, string buttonName)
    {
        if(player.channel.owner.playerID.steamID != this.player.channel.owner.playerID.steamID)
        {
            return;
        }

        ITransportConnection connection = this.player.channel.owner.transportConnection;

        #region Patterns
        string storeBuyPattern = @"^StoreItem_\d+_Buy$";
        if(Regex.Match(buttonName, storeBuyPattern).Success)
        {
            int index = int.Parse(buttonName.Substring(10, 1));
            MarketplaceItem item = pageItems[index - 1];

            EffectManager.sendUIEffectVisibility(keyID, connection, false, "Loading_Tab", true);
            bool isSellerOnline = Provider.clients.Any(c => c.playerID.steamID.m_SteamID == item.SellerID);
            Task.Run(async () =>
            {
                EError error = await Instance.marketplaceService.TryBuyItem(item, this.player, isSellerOnline);

                TaskDispatcher.QueueOnMainThread(() =>
                {
                    switch (error)
                    {
                        case EError.None:
                            UnturnedPlayer user = UnturnedPlayer.FromPlayer(this.player);
                            Instance._msgHelper.Say(user, "success_buy", false, item.ItemName);
                            Close();
                            return;
                        case EError.Balance:
                            SendNotification(Instance._msgHelper.FormatMessage("ui_error_balance", item.Price), ENotification.Error);
                            ChangeTab(ETab.Store);
                            break;
                        case EError.Item:
                            SendNotification(Instance._msgHelper.FormatMessage("ui_error_item"), ENotification.Error);
                            ChangeTab(ETab.Store);
                            break;
                    }
                });
            });

            return;
        }

        string storeViewButton = @"^StoreItem_\d+_View$";
        if (Regex.Match(buttonName, storeViewButton).Success)
        {
            int index = int.Parse(buttonName.Substring(10, 1));
            MarketplaceItem item = pageItems[index - 1];

            EffectManager.sendUIEffectVisibility(keyID, connection, false, "Loading_Tab", true);

            infoItem = pageItems[index - 1];
            ChangeTab(ETab.Info);

            return;
        }

        string inventoryRemovedPattern = @"^InventoryItem_\d+_Remove$";
        if(Regex.Match(buttonName, inventoryRemovedPattern).Success)
        {
            int index = int.Parse(buttonName.Substring(14, 1));

            MarketplaceItem item = playerListedItems.Skip(12 * inventoryCurrentPage).Take(8).ToList()[index - 1];

            Task.Run(async () =>
            {
                bool removed = await Instance.marketplaceService.DelItem(item);

                TaskDispatcher.QueueOnMainThread(() =>
                {
                    playerListedItems = Instance.marketplaceService.GetPlayerListedItems(player.channel.owner.playerID.steamID.m_SteamID);
                    
                    if (!removed)
                    {
                        string delistError = Instance._msgHelper.FormatMessage("ui_error_delist");
                        SendNotification(delistError, ENotification.Error);
                        ChangeTab(ETab.Inventory);
                        return;
                    }

                    player.inventory.forceAddItem(item.GetItem(), true);

                    string delistSuccess = Instance._msgHelper.FormatMessage("ui_message_delist", item.ItemName);
                    SendNotification(delistSuccess, ENotification.Success);
                    ChangeTab(ETab.Inventory);
                });
            });

            return;
        }

        string searchBuyPattern = @"^SearchItem_\d+_Buy$";
        if (Regex.Match(buttonName, searchBuyPattern).Success)
        {
            int index = int.Parse(buttonName.Substring(11, 1));
            MarketplaceItem item = pageItems[index - 1];

            EffectManager.sendUIEffectVisibility(keyID, connection, false, "Loading_Tab", true);
            bool isSellerOnline = Provider.clients.Any(c => c.playerID.steamID.m_SteamID == item.SellerID);
            Task.Run(async () =>
            {
                EError error = await Instance.marketplaceService.TryBuyItem(item, this.player, isSellerOnline);

                TaskDispatcher.QueueOnMainThread(() =>
                {
                    switch (error)
                    {
                        case EError.None:
                            UnturnedPlayer user = UnturnedPlayer.FromPlayer(this.player);
                            Instance._msgHelper.Say(user, "success_buy", false, item.ItemName);
                            Close();
                            break;
                        case EError.Balance:
                            SendNotification(Instance._msgHelper.FormatMessage("ui_error_balance", item.Price), ENotification.Error);
                            ChangeTab(ETab.Search);
                            break;
                        case EError.Item:
                            SendNotification(Instance._msgHelper.FormatMessage("ui_error_item"), ENotification.Error);
                            ChangeTab(ETab.Search);
                            break;
                    }
                });
            });

            return;
        }

        string searchViewPattern = @"^SearchItem_\d+_View$";
        if(Regex.Match(buttonName, searchViewPattern).Success)
        {
            int index = int.Parse(buttonName.Substring(11, 1));
            MarketplaceItem item = pageItems[index - 1];

            EffectManager.sendUIEffectVisibility(keyID, connection, false, "Loading_Tab", true);

            infoItem = pageItems[index - 1];
            ChangeTab(ETab.Info);

            return;
        }
        #endregion

        List<MarketplaceItem> items = Instance.marketplaceService.SearchCoincidences(searchInputFieldText);
        int searchPages = (int)Math.Ceiling((decimal)items.Count / 6);
        int inventoryPages = (int)Math.Ceiling((decimal)this.playerListedItems.Count / 8);
        int storePages = (int)Math.Ceiling((decimal)Instance.marketplaceService.marketplaceItems.Count / 8);
        switch (buttonName)
        {
            case "Home":
                ChangeTab(ETab.Store);
                break;
            case "Inventory":
                ChangeTab(ETab.Inventory);
                break;
            case "Search":
                ChangeTab(ETab.Search);
                break;
            case "Close":
                ChangeTab(ETab.Close);
                break;
            case "SearchButton":
                ChangeTab(ETab.Search);
                break;

            case "Info_Tab_Buy":
                EffectManager.sendUIEffectVisibility(keyID, connection, false, "Loading_Tab", true);
                bool isSellerOnline = Provider.clients.Any(c => c.playerID.steamID.m_SteamID == infoItem.SellerID);
                Task.Run(async () =>
                {
                    EError error = await Instance.marketplaceService.TryBuyItem(infoItem, this.player, isSellerOnline);

                    TaskDispatcher.QueueOnMainThread(() =>
                    {
                        switch (error)
                        {
                            case EError.None:
                                UnturnedPlayer user = UnturnedPlayer.FromPlayer(this.player);
                                Instance._msgHelper.Say(user, "success_buy", false, infoItem.ItemName);
                                Close();
                                break;
                            case EError.Balance:
                                SendNotification(Instance._msgHelper.FormatMessage("ui_error_balance", infoItem.Price), ENotification.Error);
                                ChangeTab(ETab.Store);
                                break;
                            case EError.Item:
                                SendNotification(Instance._msgHelper.FormatMessage("ui_error_item"), ENotification.Error);
                                ChangeTab(ETab.Store);
                                break;
                        }
                    });
                });
                break;
            case "InventoryNext":
                if ((inventoryCurrentPage + 1) >= inventoryPages)
                {
                    inventoryCurrentPage = 0;
                    ChangeTab(ETab.Inventory);
                    break;
                }
                inventoryCurrentPage++;
                ChangeTab(ETab.Inventory);
                break;
            case "InventoryBack":
                if ((inventoryCurrentPage - 1) < 0)
                {
                    inventoryCurrentPage = (inventoryPages - 1);
                    ChangeTab(ETab.Inventory);
                    break;
                }
                inventoryCurrentPage--;
                ChangeTab(ETab.Inventory);
                break;
            case "StoreNext":
                if ((storeCurrentPage + 1) >= storePages)
                {
                    storeCurrentPage = 0;
                    ChangeTab(ETab.Store);
                    break;
                }
                storeCurrentPage++;
                ChangeTab(ETab.Store);
                break;
            case "StoreBack":
                if ((storeCurrentPage - 1) < 0)
                {
                    storeCurrentPage = (storePages - 1);
                    ChangeTab(ETab.Store);
                    break;
                }
                storeCurrentPage--;
                ChangeTab(ETab.Store);
                break;
            case "SearchNext":
                if ((searchCurrentPage + 1) >= searchPages)
                {
                    searchCurrentPage = 0;
                    ChangeTab(ETab.Search);
                    break;
                }
                storeCurrentPage++;
                ChangeTab(ETab.Search);
                break;
            case "SearchBack":
                if ((searchCurrentPage - 1) < 0)
                {
                    searchCurrentPage = (searchPages - 1);
                    ChangeTab(ETab.Search);
                    break;
                }
                searchCurrentPage--;
                ChangeTab(ETab.Search);
                break;
        }
    }

    private void SendNotification(string text, ENotification notificationType)
    {
        ITransportConnection connection = player.channel.owner.transportConnection;

        EffectManager.sendUIEffectText(keyID, connection, false, "Notification_Text", text);

        switch (notificationType)
        {
            case ENotification.Information:
                EffectManager.sendUIEffectVisibility(keyID, connection, false, "Notification_Information", true);
                break;
            case ENotification.Success:
                EffectManager.sendUIEffectVisibility(keyID, connection, false, "Notification_Success", true);
                break;
            case ENotification.Error:
                EffectManager.sendUIEffectVisibility(keyID, connection, false, "Notification_Error", true);
                break;
        }

        EffectManager.sendUIEffectVisibility(keyID, connection, false, "Notification", true);
        StartCoroutine(CloseNotification());
    }

    private IEnumerator CloseNotification()
    {
        yield return new WaitForSeconds(5);
        EffectManager.sendUIEffectVisibility(keyID, player.channel.owner.transportConnection, false, "Notification", false);

        yield break;
    }

    private void ChangeTab(ETab newTab)
    {
        currentTab = newTab;
        ITransportConnection connection = player.channel.owner.transportConnection;
        if(newTab != ETab.Search)
        {
            searchCurrentPage = 0;
        }

        switch (newTab)
        {
            case ETab.Store:

                this.pageItems = Instance.marketplaceService.GetPageItems(storeCurrentPage);

                int storePages = (int)Math.Ceiling((decimal)Instance.marketplaceService.marketplaceItems.Count / 8);
                storePages = storePages == 0 ? 1 : storePages;
                EffectManager.sendUIEffectText(this.keyID, connection, true, "StorePage", $"<b>{storeCurrentPage + 1}</b>/<b>{storePages}</b>");

                for (int i = 0; i < 8; i++)
                {
                    if(i < pageItems.Count)
                    {
                        EffectManager.sendUIEffectText(this.keyID, connection, true, $"StoreItem_{i + 1}_Name", pageItems[i].ItemName);
                        EffectManager.sendUIEffectText(this.keyID, connection, true, $"StoreItem_{i + 1}_Price", $"${pageItems[i].Price}");
                        EffectManager.sendUIEffectImageURL(this.keyID, connection, true, $"StoreItem_{i + 1}_Icon", pageItems[i].GetIconURL(), false, false);

                        EffectManager.sendUIEffectVisibility(this.keyID, connection, true, $"StoreItem_{i + 1}_Buy", pageItems[i].SellerID != player.channel.owner.playerID.steamID.m_SteamID);
                        EffectManager.sendUIEffectVisibility(this.keyID, connection, true, $"StoreItem_{i + 1}", true);

                        continue;
                    }

                    EffectManager.sendUIEffectVisibility(keyID, connection, false, $"StoreItem_{i + 1}", false);
                }

                break;
            case ETab.Inventory:

                List<MarketplaceItem> inventoryPageItems = playerListedItems.Skip(8 * inventoryCurrentPage).Take(8).ToList();

                int inventoryPages = (int)Math.Ceiling((decimal)this.playerListedItems.Count / 8);
                inventoryPages = inventoryPages == 0 ? 1 : inventoryPages;
                EffectManager.sendUIEffectText(this.keyID, connection, true, "InventoryPage", $"<b>{storeCurrentPage + 1}</b>/<b>{inventoryPages}</b>");

                for (int i = 0; i < 8; i++)
                {
                    if(i < inventoryPageItems.Count)
                    {
                        EffectManager.sendUIEffectText(this.keyID, connection, true, $"InventoryItem_{i + 1}_Name", inventoryPageItems[i].ItemName);
                        EffectManager.sendUIEffectText(this.keyID, connection, true, $"InventoryItem_{i + 1}_Price", $"${inventoryPageItems[i].Price}");
                        EffectManager.sendUIEffectImageURL(this.keyID, connection, true, $"InventoryItem_{i + 1}_Icon", inventoryPageItems[i].GetIconURL(), false, false);

                        EffectManager.sendUIEffectVisibility(this.keyID, connection, true, $"InventoryItem_{i + 1}", true);

                        continue;
                    }

                    EffectManager.sendUIEffectVisibility(keyID, connection, false, $"InventoryItem_{i + 1}", false);
                }

                break;
            case ETab.Search:

                List<MarketplaceItem> items = Instance.marketplaceService.SearchCoincidences(searchInputFieldText);
                this.pageItems = items.Skip(6 * searchCurrentPage).Take(6).ToList();

                int searchPages = (int)Math.Ceiling((decimal)items.Count / 6);
                searchPages = searchPages == 0 ? 1 : searchPages;
                EffectManager.sendUIEffectText(this.keyID, connection, true, "SearchPage", $"<b>{searchCurrentPage + 1}</b>/<b>{searchPages}</b>");
                
                for (int i = 0; i < 6; i++)
                {
                    if (i < pageItems.Count)
                    {
                        EffectManager.sendUIEffectText(this.keyID, connection, true, $"SearchItem_{i + 1}_Name", pageItems[i].ItemName);
                        EffectManager.sendUIEffectText(this.keyID, connection, true, $"SearchItem_{i + 1}_Price", $"${pageItems[i].Price}");
                        EffectManager.sendUIEffectImageURL(this.keyID, connection, true, $"SearchItem_{i + 1}_Icon", pageItems[i].GetIconURL(), false, false);

                        EffectManager.sendUIEffectVisibility(this.keyID, connection, true, $"SearchItem_{i + 1}_Buy", pageItems[i].SellerID != player.channel.owner.playerID.steamID.m_SteamID);
                        EffectManager.sendUIEffectVisibility(this.keyID, connection, true, $"SearchItem_{i + 1}", true);

                        continue;
                    }

                    EffectManager.sendUIEffectVisibility(keyID, connection, false, $"SearchItem_{i + 1}", false);
                }

                break;
            case ETab.Info:

                EffectManager.sendUIEffectImageURL(keyID, connection, false, "Item_Icon", infoItem.GetIconURL(), false, false);

                EffectManager.sendUIEffectText(keyID, connection, false, "Item_ID", infoItem.ItemID.ToString());
                EffectManager.sendUIEffectText(keyID, connection, false, "Item_Name", infoItem.ItemName);
                EffectManager.sendUIEffectText(keyID, connection, false, "Item_Price", infoItem.Price.ToString());
                EffectManager.sendUIEffectText(keyID, connection, false, "Item_Durability", infoItem.Durability.ToString());
                EffectManager.sendUIEffectText(keyID, connection, false, "Item_Amount", infoItem.Amount.ToString());

                EffectManager.sendUIEffectVisibility(keyID, connection, false, "Info_Tab_Buy", infoItem.SellerID != player.channel.owner.playerID.steamID.m_SteamID);

                break;
            case ETab.Close:

                Close();

                return;
        }

        EffectManager.sendUIEffectVisibility(keyID, connection, false, "Loading_Tab", false);
        UpdateUI();
    }

    private void Close()
    {
        storeCurrentPage = 0;
        inventoryCurrentPage = 0;

        EffectManager.onEffectButtonClicked -= OnEffectButtonClicked;
        EffectManager.onEffectTextCommitted -= OnEffectTextCommitted;
        Provider.onEnemyDisconnected -= OnEnemyDisconnected;

        EffectManager.askEffectClearByID(Instance.Configuration.Instance.uiEffectID, player.channel.owner.transportConnection);
        player.disablePluginWidgetFlag(EPluginWidgetFlags.Modal);

        UnityEngine.Object.Destroy(this);
    }

    private void UpdateUI()
    {
        ITransportConnection connection = player.channel.owner.transportConnection;

        EffectManager.sendUIEffectVisibility(keyID, connection, true, "Inventory_Selected", currentTab == ETab.Inventory);
        EffectManager.sendUIEffectVisibility(keyID, connection, true, "Home_Selected", currentTab == ETab.Store);
        EffectManager.sendUIEffectVisibility(keyID, connection, true, "Search_Selected", currentTab == ETab.Search);
        EffectManager.sendUIEffectVisibility(keyID, connection, true, "Close_Selected", currentTab == ETab.Close);

        EffectManager.sendUIEffectVisibility(keyID, connection, true, "Info_Tab", false);

        EffectManager.sendUIEffectVisibility(keyID, connection, true, "Store_Tab", currentTab == ETab.Store);
        EffectManager.sendUIEffectVisibility(keyID, connection, true, "Inventory_Tab", currentTab == ETab.Inventory);
        EffectManager.sendUIEffectVisibility(keyID, connection, true, "Search_Tab", currentTab == ETab.Search);
        EffectManager.sendUIEffectVisibility(keyID, connection, true, "Info_Tab", currentTab == ETab.Info);
    }
}
