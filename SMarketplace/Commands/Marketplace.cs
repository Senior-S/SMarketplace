using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
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
    public List<string> Aliases => new List<string>();

    public List<string> Permissions => new List<string> { "ss.command.Marketplace" };

    public void Execute(IRocketPlayer caller, string[] command)
    {
        if (command.Length != 0)
        {
            UnturnedChat.Say(caller, SyntaxError, Color.red, true);
            return;
        }

        UnturnedPlayer user = (UnturnedPlayer)caller;

        Market_Session session = user.Player.gameObject.AddComponent<Market_Session>();
        session.Init(user.Player);
    }
}