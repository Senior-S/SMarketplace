﻿using SDG.Unturned;
using System;
using System.Management.Instrumentation;

namespace SeniorS.SMarketplace.Models;
public class MarketplaceItem
{
    public int ID { get; set; }

    public ushort ItemID { get; set; }

    public string ItemName { get; set; }

    public int Price { get; set; }

    public ulong SellerID { get; set; }

    public byte Amount { get; set; }

    public byte Durability { get; set; }

    public string Base64State { get; set; }

    public MarketplaceItem(int id, ushort itemID, string itemName, int price, ulong sellerID, byte amount, byte durability, string base64State)
    {
        ID = id;
        ItemID = itemID;
        ItemName = itemName;
        Price = price;
        SellerID = sellerID;
        Amount = amount;
        Durability = durability;
        Base64State = base64State;
    }

    //public MarketplaceItem(ushort itemID, string itemName, decimal price, ulong sellerID, byte amount, byte durability, string state)
    //{
    //    ID = -1;
    //    ItemID = itemID;
    //    ItemName = itemName;
    //    Price = price;
    //    SellerID = sellerID;
    //    Amount = amount;
    //    Durability = durability;
    //    Base64State = state;
    //}

    public MarketplaceItem(Item item, int price, ulong sellerID)
    {
        ID = -1;
        ItemID = item.id;
        ItemName = item.GetAsset().FriendlyName;
        Price = price;
        SellerID = sellerID;
        Amount = item.amount;
        Durability = item.durability;
        Base64State = Convert.ToBase64String(item.state);
    }

    public byte[] State => Convert.FromBase64String(Base64State);

    public string GetIconURL()
    {
        return SMarketplace.Instance.Configuration.Instance.iconsCDN.Replace("{0}", this.ItemID.ToString());
    }

    public Item GetItem()
    {
        return new(this.ItemID, this.Amount, this.Durability, this.State);
    }
}
