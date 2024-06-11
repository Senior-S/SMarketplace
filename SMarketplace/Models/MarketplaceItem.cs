using SDG.Unturned;
using System;

namespace SeniorS.SMarketplace.Models;
public class MarketplaceItem
{
    public int ID { get; set; }

    public ushort ItemID { get; set; }

    public string ItemName { get; set; }

    public int Price { get; set; }

    public ulong SellerID { get; set; }

    public string SellerName { get; set; }

    public byte Amount { get; set; }

    public byte Durability { get; set; }

    public string Base64State { get; set; }

    public MarketplaceItem(int id, ushort itemID, string itemName, int price, ulong sellerID, string sellerName, byte amount, byte durability, string base64State)
    {
        ID = id;
        ItemID = itemID;
        ItemName = itemName;
        Price = price;
        SellerID = sellerID;
        SellerName = sellerName;
        Amount = amount;
        Durability = durability;
        Base64State = base64State;
    }

    public MarketplaceItem(Item item, int price, ulong sellerID, string sellerName)
    {
        ID = -1;
        ItemID = item.id;
        ItemName = item.GetAsset().FriendlyName;
        Price = price;
        SellerID = sellerID;
        SellerName = sellerName;
        Amount = item.amount;
        Durability = item.durability;
        Base64State = Convert.ToBase64String(item.state);
    }

    public byte[] State => Convert.FromBase64String(Base64State);

    public string GetInfoName()
    {
        return SMarketplace.Instance._msgHelper.FormatMessage("ui_name_format", this.ItemName, this.ID);
    }

    public string GetSeller()
    {
        return SMarketplace.Instance._msgHelper.FormatMessage("ui_seller_format", this.SellerName);
    }

    public string GetPrice()
    {
        return SMarketplace.Instance._msgHelper.FormatMessage("ui_price_format", this.Price);
    }

    public string GetIconURL()
    {
        return SMarketplace.Instance.Configuration.Instance.iconsCDN.Replace("{0}", this.ItemID.ToString());
    }

    public Item GetItem()
    {
        return new(this.ItemID, this.Amount, this.Durability, this.State);
    }
}
