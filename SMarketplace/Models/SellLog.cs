namespace SeniorS.SMarketplace.Models;
public class SellLog
{
    public string ItemName { get; set; }

    public int ItemPrice { get; set; }

    public string SellerName { get; set; }

    public string BuyerName { get; set; }

    public SellLog(string itemName, int itemPrice, string sellerName, string buyerName)
    {
        ItemName = itemName;
        ItemPrice = itemPrice;
        SellerName = sellerName;
        BuyerName = buyerName;
    }
}
