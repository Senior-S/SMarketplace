using HarmonyLib;
using Rocket.Core.Utils;
using Rocket.Unturned.Player;
using SDG.NetTransport;
using SDG.Unturned;
using SeniorS.SMarketplace.Models;
using SeniorS.SMarketplace.Patchs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace SeniorS.SMarketplace.Sessions;
public class Market_Session : MonoBehaviour
{
    private SMarketplace Instance;
    
    private Player _player;
    private short _keyID;
    private ETab _currentTab;

    private string _searchInputFieldText = "";
    private int _filterCurrentPage = 0;
    private int _storeCurrentPage = 0;
    private int _inventoryCurrentPage = 0;
    private int _searchCurrentPage = 0;
    private ushort _actualFilter;
    private bool _waitingUpload = false;

    private List<MarketplaceItem> _playerListedItems;
    private List<MarketplaceItem> _pageItems;
    private MarketplaceItem _infoItem;

    private List<MarketplaceItem> _uploadItems = null;

    public void Init(Player player)
    {
        this._player = player;
        Instance = SMarketplace.Instance;
        _keyID = Instance.Configuration.Instance.uiEffectKey;
        _playerListedItems = Instance.marketplaceService.GetPlayerListedItems(this._player.channel.owner.playerID.steamID.m_SteamID);
        
        player.disablePluginWidgetFlag(EPluginWidgetFlags.ShowLifeMeters);

        EffectManager.onEffectButtonClicked += OnEffectButtonClicked;
        EffectManager.onEffectTextCommitted += OnEffectTextCommitted;
        Provider.onEnemyDisconnected += OnEnemyDisconnected;
        Inventory_Patch.onPlayerCloseStorage += OnPlayerCloseStorage;

        player.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
        player.enablePluginWidgetFlag(EPluginWidgetFlags.NoBlur);
        EffectManager.sendUIEffect(Instance.Configuration.Instance.uiEffectID, _keyID, player.channel.owner.transportConnection, true);
        ChangeTab(ETab.Home);
        EffectManager.sendUIEffectText(_keyID, player.channel.owner.transportConnection, true, "Canvas/Background/Info_Tab/Base/Content_Background/Container/Child/Item_ID_Title", Instance._msgHelper.FormatMessage("ui_id_title"));
        EffectManager.sendUIEffectText(_keyID, player.channel.owner.transportConnection, true, "Canvas/Background/Info_Tab/Base/Content_Background/Container/Child/Item_Name_Title", Instance._msgHelper.FormatMessage("ui_name_title"));
        EffectManager.sendUIEffectText(_keyID, player.channel.owner.transportConnection, true, "Canvas/Background/Info_Tab/Base/Content_Background/Container/Child/Item_Price_Title", Instance._msgHelper.FormatMessage("ui_price_title"));
        EffectManager.sendUIEffectText(_keyID, player.channel.owner.transportConnection, true, "Canvas/Background/Info_Tab/Base/Content_Background/Container/Child/Item_Durability_Title", Instance._msgHelper.FormatMessage("ui_durability_title"));
        EffectManager.sendUIEffectText(_keyID, player.channel.owner.transportConnection, true, "Canvas/Background/Info_Tab/Base/Content_Background/Container/Child/Item_Amount_Title", Instance._msgHelper.FormatMessage("ui_amount_title"));
        EffectManager.sendUIEffectText(_keyID, player.channel.owner.transportConnection, true, "Canvas/Background/Search_Tab/Search_InputField/Text Area/SearchPlaceholder", Instance._msgHelper.FormatMessage("ui_search_placeholder"));
    }

    // !! This event occurs while the UI is still invisible to the user.
    private void OnPlayerCloseStorage(Player player, Items items)
    {
        if (!_waitingUpload || _player.channel.owner.playerID.steamID != player.channel.owner.playerID.steamID) return;
        _waitingUpload = false;

        if (items.getItemCount() > 28)
        {
            Instance._msgHelper.Say(UnturnedPlayer.FromPlayer(player), "error_upload_max", true);
            foreach(ItemJar itemJar in items.items)
            {
                player.inventory.forceAddItem(itemJar.item, true);
            }

            Close();
            return;
        }
        if(items.getItemCount() < 1)
        {
            Instance._msgHelper.Say(UnturnedPlayer.FromPlayer(player), "error_upload_min", true);

            Close();
            return;
        }

        if (items.items.Any(c => Instance.Configuration.Instance.blacklistedItems.Contains(c.item.id)))
        {
            Instance._msgHelper.Say(UnturnedPlayer.FromPlayer(player), "error_blacklist", true);
            Close();
            return;
        }

        _uploadItems = items.items.Select(c => new MarketplaceItem(c.item, 0, player.channel.owner.playerID.steamID.m_SteamID, player.channel.owner.playerID.characterName)).ToList();

        _player.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
        ChangeTab(ETab.Upload);
        ITransportConnection connection = player.channel.owner.transportConnection;

        for (byte i = 0; i < _uploadItems.Count; i++)
        {
            string uploadBoxString = $"UploadBox_{i + 1}";

            EffectManager.sendUIEffectImageURL(_keyID, connection, false, $"Canvas/Background/Upload_Tab/Container/Viewport/Content/{uploadBoxString}/{uploadBoxString}_Icon", Instance.Configuration.Instance.iconsCDN.Replace("{0}", _uploadItems[i].ItemID.ToString()), true, true);
            EffectManager.sendUIEffectText(_keyID, connection, false, $"Canvas/Background/Upload_Tab/Container/Viewport/Content/{uploadBoxString}/{uploadBoxString}_Price/Text Area/Placeholder", Instance._msgHelper.FormatMessage("ui_upload_price_placeholder"));

            EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"Canvas/Background/Upload_Tab/Container/Viewport/Content/{uploadBoxString}", true);
        }

        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Canvas", true);
    }

    private void OnEnemyDisconnected(SteamPlayer steamPlayer)
    {
        if(_player.channel.owner.playerID.steamID != steamPlayer.playerID.steamID)
        {
            return;
        }

        Close();
    }

    private void OnEffectTextCommitted(Player player, string buttonName, string text)
    {
        if (player.channel.owner.playerID.steamID != this._player.channel.owner.playerID.steamID)
        {
            return;
        }

        #region Patterns
        string uploadPricePattern = @"^UploadBox_\d+_Price$";
        if (Regex.Match(buttonName, uploadPricePattern).Success)
        {
            if(!int.TryParse(text, out int price) || price < 1)
            {
                SendNotification(Instance._msgHelper.FormatMessage("error_price"), ENotification.Error);
                return;
            }

            int index = int.Parse(buttonName.Substring(10, 1));
            _uploadItems[index - 1].Price = price;
            return;
        }
        #endregion

        if (buttonName == "Search_InputField")
        {
            this._searchInputFieldText = text;
        }
    }

    private void OnEffectButtonClicked(Player player, string buttonName)
    {
        if(player.channel.owner.playerID.steamID != this._player.channel.owner.playerID.steamID)
        {
            return;
        }

        ITransportConnection connection = this._player.channel.owner.transportConnection;

        #region Patterns
        string filterItemPattern = @"^FilterItem_\d+$";
        if(Regex.Match(buttonName, filterItemPattern).Success)
        {
            int index = int.Parse(buttonName.Substring(11, 1));
            List<KeyValuePair<ushort, string>> distinctItems = Instance.marketplaceService.GetPageFilter(_filterCurrentPage);
            ushort filterItem = distinctItems[index - 1].Key;

            _actualFilter = filterItem;
            ChangeTab(ETab.Store);
            return;
        }

        string storeBuyPattern = @"^StoreItem_\d+_Buy$";
        if(Regex.Match(buttonName, storeBuyPattern).Success)
        {
            int index = int.Parse(buttonName.Substring(10, 1));
            MarketplaceItem item = _pageItems[index - 1];

            EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Loading_Tab", true);
            bool isSellerOnline = Provider.clients.Any(c => c.playerID.steamID.m_SteamID == item.SellerID);
            Task.Run(async () =>
            {
                EError error = await Instance.marketplaceService.TryBuyItem(item, this._player, isSellerOnline);

                TaskDispatcher.QueueOnMainThread(() =>
                {
                    switch (error)
                    {
                        case EError.None:
                            UnturnedPlayer user = UnturnedPlayer.FromPlayer(this._player);
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
            MarketplaceItem item = _pageItems[index - 1];

            EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Loading_Tab", true);

            _infoItem = _pageItems[index - 1];
            ChangeTab(ETab.Info);

            return;
        }

        string inventoryRemovedPattern = @"^InventoryItem_\d+_Remove$";
        if(Regex.Match(buttonName, inventoryRemovedPattern).Success)
        {
            int index = int.Parse(buttonName.Substring(14, 1));

            MarketplaceItem item = _playerListedItems.Skip(8 * _inventoryCurrentPage).Take(8).ToList()[index - 1];

            Task.Run(async () =>
            {
                bool removed = await Instance.marketplaceService.DelItem(item);

                TaskDispatcher.QueueOnMainThread(() =>
                {
                    _playerListedItems = Instance.marketplaceService.GetPlayerListedItems(player.channel.owner.playerID.steamID.m_SteamID);
                    
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
            MarketplaceItem item = _pageItems[index - 1];

            EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Loading_Tab", true);
            bool isSellerOnline = Provider.clients.Any(c => c.playerID.steamID.m_SteamID == item.SellerID);
            Task.Run(async () =>
            {
                EError error = await Instance.marketplaceService.TryBuyItem(item, this._player, isSellerOnline);

                TaskDispatcher.QueueOnMainThread(() =>
                {
                    switch (error)
                    {
                        case EError.None:
                            UnturnedPlayer user = UnturnedPlayer.FromPlayer(this._player);
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
            MarketplaceItem item = _pageItems[index - 1];

            EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Loading_Tab", true);

            _infoItem = _pageItems[index - 1];
            ChangeTab(ETab.Info);

            return;
        }
        #endregion

        List<MarketplaceItem> items = Instance.marketplaceService.SearchCoincidences(_searchInputFieldText);
        int searchPages = (int)Math.Ceiling((decimal)items.Count / 6);
        int inventoryPages = (int)Math.Ceiling((decimal)this._playerListedItems.Count / 8);
        int filterPages = (int)Math.Ceiling((decimal)Instance.marketplaceService.distinctItems.Count / 20);
        int storePages = _pageItems != null ? (int)Math.Ceiling((decimal)_pageItems.Count / 5) : 1;
        switch (buttonName)
        {
            case "Home":
                if (_uploadItems != null)
                {
                    SendNotification(Instance._msgHelper.FormatMessage("error_upload_incomplete"), ENotification.Error);
                    break;
                }
                ChangeTab(ETab.Home);
                break;
            case "Store":
                if (_uploadItems != null)
                {
                    SendNotification(Instance._msgHelper.FormatMessage("error_upload_incomplete"), ENotification.Error);
                    break;
                }
                ChangeTab(ETab.Filter);
                break;
            case "Inventory":
                if (_uploadItems != null)
                {
                    SendNotification(Instance._msgHelper.FormatMessage("error_upload_incomplete"), ENotification.Error);
                    break;
                }
                ChangeTab(ETab.Inventory);
                break;
            case "Search":
                if (_uploadItems != null)
                {
                    SendNotification(Instance._msgHelper.FormatMessage("error_upload_incomplete"), ENotification.Error);
                    break;
                }
                ChangeTab(ETab.Search);
                break;
            case "Close":
                if (_uploadItems != null)
                {
                    SendNotification(Instance._msgHelper.FormatMessage("error_upload_incomplete"), ENotification.Error);
                    break;
                }
                ChangeTab(ETab.Close);
                break;
            case "SearchButton":
                if (_uploadItems != null)
                {
                    SendNotification(Instance._msgHelper.FormatMessage("error_upload_incomplete"), ENotification.Error);
                    break;
                }
                ChangeTab(ETab.Search);
                break;
            case "InventoryUpload":

                EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas", false);
                _player.disablePluginWidgetFlag(EPluginWidgetFlags.Modal);

                Items storage = new(PlayerInventory.STORAGE);
                storage.resize(8, 15);
                FieldInfo isStoring = AccessTools.Field(typeof(PlayerInventory), "isStoring");
                FieldInfo isStorageTrunk = AccessTools.Field(typeof(PlayerInventory), "isStorageTrunk");
                isStoring.SetValue(_player.inventory, true);
                isStorageTrunk.SetValue(_player.inventory, false);

                _waitingUpload = true;
                _player.inventory.updateItems(PlayerInventory.STORAGE, storage);
                _player.inventory.sendStorage();

                break;
            case "UploadSubmit":

                if(_uploadItems.Any(c => c.Price <= 0))
                {
                    SendNotification(Instance._msgHelper.FormatMessage("error_price"), ENotification.Error);
                    break;
                }

                EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Loading_Tab", true);
                TaskDispatcher.QueueOnMainThread(async () =>
                {
                    bool success = await Instance.marketplaceService.ListMultipleItems(_uploadItems);
                    TaskDispatcher.QueueOnMainThread(() =>
                    {
                        if (!success)
                        {
                            foreach (MarketplaceItem item in _uploadItems)
                            {
                                player.inventory.forceAddItem(item.GetItem(), true);
                            }
                            _uploadItems = null;
                            SendNotification(Instance._msgHelper.FormatMessage("error_list"), ENotification.Error);
                            ChangeTab(ETab.Home);
                            return;
                        }
                        _uploadItems = null;
                        SendNotification(Instance._msgHelper.FormatMessage("success_upload"), ENotification.Success);
                        _playerListedItems = Instance.marketplaceService.GetPlayerListedItems(this._player.channel.owner.playerID.steamID.m_SteamID);
                        ChangeTab(ETab.Home);
                    });
                });
                break;
            case "UploadCancel":
                EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Loading_Tab", true);
                foreach (MarketplaceItem item in _uploadItems)
                {
                    player.inventory.forceAddItem(item.GetItem(), true);
                }
                Close();
                break;
            case "Info_Tab_Buy":
                EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Loading_Tab", true);
                bool isSellerOnline = Provider.clients.Any(c => c.playerID.steamID.m_SteamID == _infoItem.SellerID);
                Task.Run(async () =>
                {
                    EError error = await Instance.marketplaceService.TryBuyItem(_infoItem, this._player, isSellerOnline);

                    TaskDispatcher.QueueOnMainThread(() =>
                    {
                        switch (error)
                        {
                            case EError.None:
                                UnturnedPlayer user = UnturnedPlayer.FromPlayer(this._player);
                                Instance._msgHelper.Say(user, "success_buy", false, _infoItem.ItemName);
                                Close();
                                break;
                            case EError.Balance:
                                SendNotification(Instance._msgHelper.FormatMessage("ui_error_balance", _infoItem.Price), ENotification.Error);
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
            case "FilterNext":
                if ((_filterCurrentPage + 1) >= filterPages)
                {
                    _filterCurrentPage = 0;
                    ChangeTab(ETab.Filter);
                    break;
                }
                _filterCurrentPage++;
                ChangeTab(ETab.Filter);
                break;
            case "FilterBack":
                if ((_filterCurrentPage - 1) < 0)
                {
                    _filterCurrentPage = (filterPages - 1);
                    ChangeTab(ETab.Filter);
                    break;
                }
                _filterCurrentPage--;
                ChangeTab(ETab.Filter);
                break;
            case "InventoryNext":
                if ((_inventoryCurrentPage + 1) >= inventoryPages)
                {
                    _inventoryCurrentPage = 0;
                    ChangeTab(ETab.Inventory);
                    break;
                }
                _inventoryCurrentPage++;
                ChangeTab(ETab.Inventory);
                break;
            case "InventoryBack":
                if ((_inventoryCurrentPage - 1) < 0)
                {
                    _inventoryCurrentPage = (inventoryPages - 1);
                    ChangeTab(ETab.Inventory);
                    break;
                }
                _inventoryCurrentPage--;
                ChangeTab(ETab.Inventory);
                break;
            case "StoreNext":
                if ((_storeCurrentPage + 1) >= storePages)
                {
                    _storeCurrentPage = 0;
                    ChangeTab(ETab.Store);
                    break;
                }
                _storeCurrentPage++;
                ChangeTab(ETab.Store);
                break;
            case "StoreBack":
                if ((_storeCurrentPage - 1) < 0)
                {
                    _storeCurrentPage = (storePages - 1);
                    ChangeTab(ETab.Store);
                    break;
                }
                _storeCurrentPage--;
                ChangeTab(ETab.Store);
                break;
            case "SearchNext":
                if ((_searchCurrentPage + 1) >= searchPages)
                {
                    _searchCurrentPage = 0;
                    ChangeTab(ETab.Search);
                    break;
                }
                _storeCurrentPage++;
                ChangeTab(ETab.Search);
                break;
            case "SearchBack":
                if ((_searchCurrentPage - 1) < 0)
                {
                    _searchCurrentPage = (searchPages - 1);
                    ChangeTab(ETab.Search);
                    break;
                }
                _searchCurrentPage--;
                ChangeTab(ETab.Search);
                break;
        }
    }

    private void SendNotification(string text, ENotification notificationType)
    {
        ITransportConnection connection = _player.channel.owner.transportConnection;

        EffectManager.sendUIEffectText(_keyID, connection, false, "Notification_Text", text);

        switch (notificationType)
        {
            case ENotification.Information:
                EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Notification_Information", true);
                break;
            case ENotification.Success:
                EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Notification_Success", true);
                break;
            case ENotification.Error:
                EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Notification_Error", true);
                break;
        }

        EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Notification", true);
        StartCoroutine(CloseNotification());
    }

    private IEnumerator CloseNotification()
    {
        yield return new WaitForSeconds(5);
        EffectManager.sendUIEffectVisibility(_keyID, _player.channel.owner.transportConnection, false, "Notification", false);

        yield break;
    }

    private void ChangeTab(ETab newTab)
    {
        _currentTab = newTab;
        ITransportConnection connection = _player.channel.owner.transportConnection;
        if (newTab != ETab.Search)
        {
            _searchCurrentPage = 0;
        }
        EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/PlaceholderListings", true);
        EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/PlaceholderSellers", true);
        EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/PlaceholderBought", true);
        EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/TotalListings", false);
        EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/TotalSellers", false);
        EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/TotalBought", false);

        switch (newTab)
        {
            case ETab.Home:

                EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/TotalListings_Title", Instance._msgHelper.FormatMessage("ui_totallistings_title"));
                EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/TotalSellers_Title", Instance._msgHelper.FormatMessage("ui_totalsellers_title"));
                EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/TotalBought_Title", Instance._msgHelper.FormatMessage("ui_totalbought_title"));

                string totalListings = Instance.marketplaceService.marketplaceItems.Count().ToString();
                string totalSellers = Instance.marketplaceService.GetTotalSellers().ToString();

                Task.Run(async () =>
                {
                    try
                    {
                        string totalBought = (await Instance.marketplaceService.GetTotalBought()).ToString();
                        string formatedLogs = await Instance.marketplaceService.GetFormattedLogs();

                        TaskDispatcher.QueueOnMainThread(() =>
                        {
                            EffectManager.sendUIEffectText(_keyID, connection, true, "Canvas/Background/Home_Tab/Logs/Log", formatedLogs);
                            EffectManager.sendUIEffectText(_keyID, connection, true, "Canvas/Background/Home_Tab/Container/Box/TotalBought", totalBought);
                            EffectManager.sendUIEffectText(_keyID, connection, true, "Canvas/Background/Home_Tab/Container/Box/TotalListings", totalListings);
                            EffectManager.sendUIEffectText(_keyID, connection, true, "Canvas/Background/Home_Tab/Container/Box/TotalSellers", totalSellers);

                            EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/PlaceholderListings", false);
                            EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/TotalListings", true);

                            EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/PlaceholderSellers", false);
                            EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/TotalSellers", true);

                            EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/PlaceholderBought", false);
                            EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Home_Tab/Container/Box/TotalBought", true);
                        });
                    } 
                    catch(Exception ex)
                    {
                        TaskDispatcher.QueueOnMainThread(() =>
                        {
                            Rocket.Core.Logging.Logger.Log(ex);
                        });
                    }
                });

                break;
            case ETab.Filter:
                EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Canvas/Background/Loading_Tab", true);
                int filterPages = (int)Math.Ceiling((decimal)Instance.marketplaceService.distinctItems.Count / 20);
                filterPages = filterPages == 0 ? 1 : filterPages;
                EffectManager.sendUIEffectText(this._keyID, connection, true, "FilterPage", $"<b>{_filterCurrentPage + 1}</b>/<b>{filterPages}</b>");

                List<KeyValuePair<ushort, string>> distinctItems = Instance.marketplaceService.GetPageFilter(_filterCurrentPage);

                if (distinctItems.Count < 1) break;

                for (int i = 0; i < distinctItems.Count; i++)
                {
                    EffectManager.sendUIEffectImageURL(_keyID, connection, false, $"Canvas/Background/Filter_Tab/Filter_Gameobject/FilterItem_{i + 1}/Background/FilterItem_{i + 1}_Icon", Instance.Configuration.Instance.iconsCDN.Replace("{0}", distinctItems[i].Key.ToString()), true, true);
                    EffectManager.sendUIEffectText(_keyID, connection, false, $"Canvas/Background/Filter_Tab/Filter_Gameobject/FilterItem_{i + 1}/FilterItem_{i + 1}_Name", distinctItems[i].Value);

                    EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"Canvas/Background/Filter_Tab/Filter_Gameobject/FilterItem_{i + 1}", true);
                }
                break;
            case ETab.Store:

                this._pageItems = Instance.marketplaceService.GetPageItems(_storeCurrentPage, _actualFilter);

                int storePages = (int)Math.Ceiling((decimal)_pageItems.Count / 5);
                storePages = storePages == 0 ? 1 : storePages;
                EffectManager.sendUIEffectText(this._keyID, connection, true, "StorePage", $"<b>{_storeCurrentPage + 1}</b>/<b>{storePages}</b>");

                for (int i = 0; i < 5; i++)
                {
                    if(i < _pageItems.Count)
                    {
                        bool shouldDisplayAttachmentsStore = Instance.Configuration.Instance.displayAttachments && IsGun(_pageItems[i].ItemID);
                        if (shouldDisplayAttachmentsStore)
                        {
                            byte[] itemState = _pageItems[i].State;

                            ushort tactical = BitConverter.ToUInt16(itemState, 2);
                            ushort grip = BitConverter.ToUInt16(itemState, 4);
                            ushort barrel = BitConverter.ToUInt16(itemState, 6);

                            DisplayAttachmentStore(EAttachmentType.Tactical, tactical, (i + 1));
                            DisplayAttachmentStore(EAttachmentType.Grip, grip, (i + 1));
                            DisplayAttachmentStore(EAttachmentType.Barrel, barrel, (i + 1));
                        }
                        else if (!IsGun(_pageItems[i].ItemID))
                        {
                            EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"Canvas/Background/Store_Tab/Store_Gameobject/StoreItem_{i + 1}/StoreItem_{i + 1}_Attachments/Grip_On", false);
                            EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"Canvas/Background/Store_Tab/Store_Gameobject/StoreItem_{i + 1}/StoreItem_{i + 1}_Attachments/Grip_Off", false);

                            EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"Canvas/Background/Store_Tab/Store_Gameobject/StoreItem_{i + 1}/StoreItem_{i + 1}_Attachments/Barrel_On", false);
                            EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"Canvas/Background/Store_Tab/Store_Gameobject/StoreItem_{i + 1}/StoreItem_{i + 1}_Attachments/Barrel_Off", false);

                            EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"Canvas/Background/Store_Tab/Store_Gameobject/StoreItem_{i + 1}/StoreItem_{i + 1}_Attachments/Tactical_On", false);
                            EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"Canvas/Background/Store_Tab/Store_Gameobject/StoreItem_{i + 1}/StoreItem_{i + 1}_Attachments/Tactical_Off", false);
                        }

                        EffectManager.sendUIEffectText(this._keyID, connection, true, $"StoreItem_{i + 1}_Name", $"<b>{_pageItems[i].ItemName}</b> ({_pageItems[i].GetPrice()})");
                        EffectManager.sendUIEffectImageURL(this._keyID, connection, true, $"StoreItem_{i + 1}_Icon", _pageItems[i].GetIconURL(), false, false);
                        EffectManager.sendUIEffectText(this._keyID, connection, true, $"StoreItem_{i + 1}_Seller", _pageItems[i].GetSeller());

                        EffectManager.sendUIEffectVisibility(this._keyID, connection, true, $"StoreItem_{i + 1}_Buy", _pageItems[i].SellerID != _player.channel.owner.playerID.steamID.m_SteamID);
                        EffectManager.sendUIEffectVisibility(this._keyID, connection, true, $"StoreItem_{i + 1}", true);

                        continue;
                    }

                    EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"StoreItem_{i + 1}", false);
                }

                break;
            case ETab.Inventory:

                List<MarketplaceItem> inventoryPageItems = _playerListedItems.Skip(8 * _inventoryCurrentPage).Take(8).ToList();

                int inventoryPages = (int)Math.Ceiling((decimal)this._playerListedItems.Count / 8);
                inventoryPages = inventoryPages == 0 ? 1 : inventoryPages;
                EffectManager.sendUIEffectText(this._keyID, connection, true, "InventoryPage", $"<b>{_storeCurrentPage + 1}</b>/<b>{inventoryPages}</b>");

                for (int i = 0; i < 8; i++)
                {
                    if(i < inventoryPageItems.Count)
                    {
                        EffectManager.sendUIEffectText(this._keyID, connection, true, $"InventoryItem_{i + 1}_Name", inventoryPageItems[i].ItemName);
                        EffectManager.sendUIEffectText(this._keyID, connection, true, $"InventoryItem_{i + 1}_Price", inventoryPageItems[i].GetPrice());
                        EffectManager.sendUIEffectImageURL(this._keyID, connection, true, $"InventoryItem_{i + 1}_Icon", inventoryPageItems[i].GetIconURL(), false, false);

                        EffectManager.sendUIEffectVisibility(this._keyID, connection, true, $"InventoryItem_{i + 1}", true);

                        continue;
                    }

                    EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"InventoryItem_{i + 1}", false);
                }

                break;
            case ETab.Search:

                List<MarketplaceItem> items = Instance.marketplaceService.SearchCoincidences(_searchInputFieldText);
                this._pageItems = items.Skip(6 * _searchCurrentPage).Take(6).ToList();

                int searchPages = (int)Math.Ceiling((decimal)items.Count / 6);
                searchPages = searchPages == 0 ? 1 : searchPages;
                EffectManager.sendUIEffectText(this._keyID, connection, true, "SearchPage", $"<b>{_searchCurrentPage + 1}</b>/<b>{searchPages}</b>");
                
                for (int i = 0; i < 6; i++)
                {
                    if (i < _pageItems.Count)
                    {
                        EffectManager.sendUIEffectText(this._keyID, connection, true, $"SearchItem_{i + 1}_Name", _pageItems[i].ItemName);
                        EffectManager.sendUIEffectText(this._keyID, connection, true, $"SearchItem_{i + 1}_Price", _pageItems[i].GetPrice());
                        EffectManager.sendUIEffectImageURL(this._keyID, connection, true, $"SearchItem_{i + 1}_Icon", _pageItems[i].GetIconURL(), false, false);

                        EffectManager.sendUIEffectVisibility(this._keyID, connection, true, $"SearchItem_{i + 1}_Buy", _pageItems[i].SellerID != _player.channel.owner.playerID.steamID.m_SteamID);
                        EffectManager.sendUIEffectVisibility(this._keyID, connection, true, $"SearchItem_{i + 1}", true);

                        continue;
                    }

                    EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"SearchItem_{i + 1}", false);
                }

                break;
            case ETab.Info:

                EffectManager.sendUIEffectImageURL(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/GeneralInfo/Item_Background/Item_Icon", _infoItem.GetIconURL(), false, false);

                EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/GeneralInfo/Content_Background/Container/Child/Item_Name", _infoItem.GetInfoName());
                EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/GeneralInfo/Content_Background/Container/Child/Item_Price", _infoItem.GetPrice());
                EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/GeneralInfo/Content_Background/Container/Child/Item_Durability", _infoItem.Durability.ToString());
                EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/GeneralInfo/Content_Background/Container/Child/Item_Amount", _infoItem.Amount.ToString());

                bool shouldDisplayAttachmentsInfo = Instance.Configuration.Instance.displayAttachments && IsGun(_infoItem.ItemID);
                EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Canvas/Background/Info_Tab/Base/Attachments", shouldDisplayAttachmentsInfo);

                if (shouldDisplayAttachmentsInfo)
                {
                    byte[] itemState = _infoItem.State;

                    ushort sight = BitConverter.ToUInt16(itemState, 0);
                    ushort tactical = BitConverter.ToUInt16(itemState, 2);
                    ushort grip = BitConverter.ToUInt16(itemState, 4);
                    ushort barrel = BitConverter.ToUInt16(itemState, 6);

                    EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Canvas/Background/Info_Tab/Base/Attachments", true);

                    DisplayAttachmentInfo(EAttachmentType.Sight, sight);
                    DisplayAttachmentInfo(EAttachmentType.Tactical, tactical);
                    DisplayAttachmentInfo(EAttachmentType.Grip, grip);
                    DisplayAttachmentInfo(EAttachmentType.Barrel, barrel);
                }

                EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Info_Tab_Buy", _infoItem.SellerID != _player.channel.owner.playerID.steamID.m_SteamID);

                break;
            case ETab.Close:

                Close();

                return;
        }

        EffectManager.sendUIEffectVisibility(_keyID, connection, false, "Canvas/Background/Loading_Tab", false);
        UpdateUI();
    }

    private bool IsGun(ushort itemID)
    {
        return Assets.find(EAssetType.ITEM, itemID) is ItemGunAsset;
    }

    private void DisplayAttachmentInfo(EAttachmentType attachmentType, ushort attachmentID)
    {
        ITransportConnection connection = _player.channel.owner.transportConnection;
        string attachmentName = attachmentType.ToString();
        EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"Canvas/Background/Info_Tab/Base/Attachments/{attachmentName}", attachmentID != 0);
        if (attachmentID == 0)
        {
            return;
        }
        Asset attachmentAsset = Assets.find(EAssetType.ITEM, attachmentID);

        string iconUrl = Instance.Configuration.Instance.iconsCDN.Replace("{0}", attachmentID.ToString()) ;

        switch (attachmentType)
        {
            case EAttachmentType.Sight:
                if(attachmentAsset is not ItemSightAsset sightAsset)
                {
                    EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/Attachments/Sight/Sight_Name", "Unknown");
                    break;
                }

                EffectManager.sendUIEffectImageURL(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/Attachments/Sight/Icon", iconUrl, true, true);
                EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/Attachments/Sight/Sight_Name", sightAsset.FriendlyName);

                break;
            case EAttachmentType.Barrel:
                if (attachmentAsset is not ItemBarrelAsset barrelAsset)
                {
                    EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/Attachments/Barrel/Barrel_Name", "Unknown");
                    break;
                }

                EffectManager.sendUIEffectImageURL(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/Attachments/Barrel/Icon", iconUrl, true, true);
                EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/Attachments/Barrel/Barrel_Name", barrelAsset.FriendlyName);

                break;
            case EAttachmentType.Grip:
                if (attachmentAsset is not ItemGripAsset gripAsset)
                {
                    EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/Attachments/Grip/Grip_Name", "Unknown");
                    break;
                }

                EffectManager.sendUIEffectImageURL(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/Attachments/Grip/Icon", iconUrl, true, true);
                EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/Attachments/Grip/Grip_Name", gripAsset.FriendlyName);

                break;
            case EAttachmentType.Tactical:
                if (attachmentAsset is not ItemTacticalAsset tacticalAsset)
                {
                    EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/Attachments/Tactical/Tactical_Name", "Unknown");
                    break;
                }

                EffectManager.sendUIEffectImageURL(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/Attachments/Tactical/Icon", iconUrl, true, true);
                EffectManager.sendUIEffectText(_keyID, connection, false, "Canvas/Background/Info_Tab/Base/Attachments/Tactical/Tactical_Name", tacticalAsset.FriendlyName);

                break;
        }
    }

    private void DisplayAttachmentStore(EAttachmentType attachmentType, ushort attachmentID, int storeIndex)
    {
        ITransportConnection connection = _player.channel.owner.transportConnection;
        string attachmentName = attachmentType.ToString();

        EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"Canvas/Background/Store_Tab/Store_Gameobject/StoreItem_{storeIndex}/StoreItem_{storeIndex}_Attachments/{attachmentName}_On", attachmentID != 0);
        EffectManager.sendUIEffectVisibility(_keyID, connection, false, $"Canvas/Background/Store_Tab/Store_Gameobject/StoreItem_{storeIndex}/StoreItem_{storeIndex}_Attachments/{attachmentName}_Off", attachmentID == 0);
    }

    private void Close()
    {
        _storeCurrentPage = 0;
        _inventoryCurrentPage = 0;
        _uploadItems = null;

        EffectManager.onEffectButtonClicked -= OnEffectButtonClicked;
        EffectManager.onEffectTextCommitted -= OnEffectTextCommitted;
        Provider.onEnemyDisconnected -= OnEnemyDisconnected;
        Inventory_Patch.onPlayerCloseStorage -= OnPlayerCloseStorage;

        EffectManager.askEffectClearByID(Instance.Configuration.Instance.uiEffectID, _player.channel.owner.transportConnection);
        _player.disablePluginWidgetFlag(EPluginWidgetFlags.Modal);
        _player.enablePluginWidgetFlag(EPluginWidgetFlags.ShowLifeMeters);

        UnityEngine.Object.Destroy(this);
    }

    private void UpdateUI()
    {
        ITransportConnection connection = _player.channel.owner.transportConnection;

        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Home_Selected", _currentTab == ETab.Home);
        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Store_Selected", _currentTab == ETab.Filter | _currentTab == ETab.Store);
        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Inventory_Selected", _currentTab == ETab.Inventory | _currentTab == ETab.Upload);
        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Search_Selected", _currentTab == ETab.Search);
        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Close_Selected", _currentTab == ETab.Close);

        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Canvas/Background/Home_Tab", _currentTab == ETab.Home);
        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Canvas/Background/Filter_Tab", _currentTab == ETab.Filter);
        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Canvas/Background/Store_Tab", _currentTab == ETab.Store);
        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Canvas/Background/Upload_Tab", _currentTab == ETab.Upload);
        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Canvas/Background/Inventory_Tab", _currentTab == ETab.Inventory);
        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Canvas/Background/Search_Tab", _currentTab == ETab.Search);
        EffectManager.sendUIEffectVisibility(_keyID, connection, true, "Canvas/Background/Info_Tab", _currentTab == ETab.Info);
    }
}
