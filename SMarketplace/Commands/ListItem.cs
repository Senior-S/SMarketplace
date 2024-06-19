using Rocket.API;
using Rocket.Core.Utils;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using SeniorS.SMarketplace.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SeniorS.SMarketplace.Commands;
public class ListItem : IRocketCommand
{
    public AllowedCaller AllowedCaller => AllowedCaller.Player;

    public string Name => "ListItem";

    public string Help => "";

    public string Syntax => "/ListItem <item name> <price>";

    private string SyntaxError => $"Wrong syntax! Correct usage: {Syntax}";
    public List<string> Aliases => new List<string>();

    public List<string> Permissions => new List<string> { "ss.command.ListItem" };

    public void Execute(IRocketPlayer caller, string[] command)
    {
        if (command.Length < 2 || !int.TryParse(command[command.Length - 1], out int price))
        {
            UnturnedChat.Say(caller, SyntaxError, Color.red, true);
            return;
        }

        UnturnedPlayer user = (UnturnedPlayer)caller;
        SMarketplace Instance = SMarketplace.Instance;

        if(price < 1)
        {
            Instance._msgHelper.Say(user, "error_price", true);
            return;
        }
        if(price > Instance.Configuration.Instance.maxItemPrice)
        {
            Instance._msgHelper.Say(user, "error_price_max", true, Instance.Configuration.Instance.maxItemPrice);
            return;
        }

        string itemName = string.Join(" ", command.Take(command.Length - 1)).ToLower();
        ItemJar itemJar = null;
        byte itemPage = 0;
        byte itemIndex = 0;

        foreach (Items items in user.Player.inventory.items)
        {
            if (items == null || items.page >= 7 || items.items.Count < 1) continue;
            
            var found = items.items.FirstOrDefault(c => c != null && c.item.GetAsset() != null && c.item.GetAsset().FriendlyName.ToLower().Contains(itemName));

            if(found != null)
            {
                itemJar = found;
                itemPage = items.page;
                itemIndex = items.findIndex(found.x, found.y, out _, out _);
                break;
            }
        }
        if (itemJar == null)
        {
            Instance._msgHelper.Say(user, "error_item", true, itemName);
            return;
        }
        if (Instance.Configuration.Instance.blacklistedItems.Contains(itemJar.item.id))
        {
            Instance._msgHelper.Say(user, "error_blacklist", true);
            return;
        }

        itemName = itemJar.item.GetAsset().FriendlyName;
        user.Player.inventory.removeItem(itemPage, itemIndex);
        Instance._msgHelper.Say(user, "info_item", false);
        MarketplaceItem item = new(itemJar.item, price, user.CSteamID.m_SteamID, user.CharacterName);
        Task.Run(async () =>
        {
            bool listed = await Instance.marketplaceService.ListItem(item);

            TaskDispatcher.QueueOnMainThread(() =>
            {
                if (!listed)
                {
                    user.Player.inventory.forceAddItem(itemJar.item, true);
                    Instance._msgHelper.Say(user, "error_list", true);
                    return;
                }

                Instance._msgHelper.Say(user, "success_item", false, itemName);
            });
        });
    }
}