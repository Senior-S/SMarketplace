using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using SeniorS.SMarketplace.Sessions;
using System.Collections.Generic;
using UnityEngine;

namespace SeniorS.SMarketplace.Commands;
public class Marketplace : IRocketCommand
{
    public AllowedCaller AllowedCaller => AllowedCaller.Player;

    public string Name => "Marketplace";

    public string Help => "";

    public string Syntax => "/Marketplace";

    private string SyntaxError => $"Wrong syntax! Correct usage: {Syntax}";
    public List<string> Aliases => new List<string> { "market" };

    public List<string> Permissions => new List<string> { "ss.command.Marketplace" };

    public void Execute(IRocketPlayer caller, string[] command)
    {
        if (command.Length != 0)
        {
            UnturnedChat.Say(caller, SyntaxError, Color.red, true);
            return;
        }

        UnturnedPlayer user = (UnturnedPlayer)caller;
        SMarketplace Instance = SMarketplace.Instance;

        if (Instance.Configuration.Instance.requiredItemToMarketplace != 0)
        {
            if(user.Player.equipment.itemID == Instance.Configuration.Instance.requiredItemToMarketplace)
            {
                Market_Session sessionRequired = user.Player.gameObject.AddComponent<Market_Session>();
                sessionRequired.Init(user.Player);
                return;
            }

            Asset asset = Assets.find(EAssetType.ITEM, Instance.Configuration.Instance.requiredItemToMarketplace);
            if(asset != null && asset is ItemAsset itemAsset)
            {
                Instance._msgHelper.Say(user, "error_required_item", true, itemAsset.FriendlyName);
                return;
            }

            Instance._msgHelper.Say(user, "error_required_item", true, "???");
            return;
        }

        Market_Session session = user.Player.gameObject.AddComponent<Market_Session>();
        session.Init(user.Player);
    }
}