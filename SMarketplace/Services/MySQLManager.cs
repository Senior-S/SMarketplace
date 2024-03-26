using MySql.Data.MySqlClient;
using Rocket.Core.Logging;
using Rocket.Core.Utils;
using SeniorS.SMarketplace.Models;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace SeniorS.SMarketplace.Services;
public class MySQLManager : IDisposable
{
    private MySqlConnection _connection;

    public MySQLManager()
    {
        try
        {
            _connection = new MySqlConnection(SMarketplace.Instance.connectionString);
            _connection.Open();
        }
        catch (MySqlException ex)
        {
            switch (ex.Number)
            {
                case 1045:
                    TaskDispatcher.QueueOnMainThread(() =>
                    {
                        Logger.LogException(ex, "Error! Please check your credentials.");
                    });
                    break;
                case 1144:
                    TaskDispatcher.QueueOnMainThread(() =>
                    {
                        Logger.LogException(ex, "Error! Table is full, possible due a full disk issue.");
                    });
                    break;
                case 1040:
                    TaskDispatcher.QueueOnMainThread(() =>
                    {
                        Logger.LogException(ex, "Error! Can't connect due there's already too many connections.");
                    });
                    break;
                case 1042:
                    TaskDispatcher.QueueOnMainThread(() =>
                    {
                        Logger.LogException(ex, "Error! Check your database information.");
                    });
                    break;
                default:
                    TaskDispatcher.QueueOnMainThread(() =>
                    {
                        Logger.LogException(ex, "Unexpected MySQL error!");
                    });
                    break;
            }
            throw new Exception("Error loading MySQL service");
        }
        catch (Exception ex)
        {
            TaskDispatcher.QueueOnMainThread(() =>
            {
                Logger.LogException(ex, "Unexpected error!");
            });
            throw new Exception("Error loading MySQL service");
        }
    }

    public void Init()
    {
        const string sql_table_items = "CREATE TABLE IF NOT EXISTS `smarketplace_Item` (`ID` SERIAL, `ItemID` SMALLINT UNSIGNED NOT NULL, `ItemName` VARCHAR(255) NOT NULL, `ItemPrice` INT NOT NULL, `ItemAmount` TINYINT UNSIGNED NOT NULL, `ItemDurability` TINYINT UNSIGNED NOT NULL, `ItemState` VARCHAR(172) DEFAULT NULL, `SellerID` BIGINT UNSIGNED NOT NULL, PRIMARY KEY (`ID`));";
        const string sql_table_logs = "CREATE TABLE IF NOT EXISTS `smarketplace_Log` (`ID` SERIAL, `ItemID` SMALLINT UNSIGNED NOT NULL, `ItemName` VARCHAR(255) NOT NULL, `ItemPrice` INT NOT NULL, `SellerID` BIGINT UNSIGNED NOT NULL, `BuyerID` BIGINT UNSIGNED NOT NULL, `Paid` TINYINT(1) NOT NULL, PRIMARY KEY (`ID`));";

        MySqlCommand query_table_items = new(sql_table_items, _connection);
        MySqlCommand query_table_logs = new(sql_table_logs, _connection);

        query_table_items.ExecuteNonQuery();
        query_table_logs.ExecuteNonQuery();
    }

    public async Task<List<MarketplaceItem>> GetItems()
    {
        const string sql_select = "SELECT * FROM `smarketplace_Item`;";

        List<MarketplaceItem> marketplaceItems = new();

        MySqlCommand query_select = new(sql_select, _connection);
        
        DbDataReader reader = await query_select.ExecuteReaderAsync();

        if (!reader.HasRows)
        {
            reader.Close();
            return marketplaceItems;
        }

        while(await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            ushort itemID = ushort.Parse(reader["ItemID"].ToString());
            string itemName = reader.GetString(2);
            int itemPrice = reader.GetInt32(3);
            byte amount = reader.GetByte(4);
            byte durability = reader.GetByte(5);
            string stateBase64 = reader.GetString(6);
            ulong sellerID = ulong.Parse(reader["SellerID"].ToString());

            MarketplaceItem item = new (id, itemID, itemName, itemPrice, sellerID, amount, durability, stateBase64);
            marketplaceItems.Add(item);
        }

        reader.Close();
        return marketplaceItems;
    }

    public async Task<int> AddItem(MarketplaceItem item)
    {
        const string sql_insert = "INSERT INTO `smarketplace_Item` (`ItemID`, `ItemName`, `ItemPrice`, `ItemAmount`, `ItemDurability`, `ItemState`, `SellerID`) VALUES (@id, @name, @price, @amount, @durability, @state, @sellerID); SELECT LAST_INSERT_ID();";

        MySqlCommand query_insert = new(sql_insert, _connection);
        query_insert.Parameters.AddWithValue("@id", item.ItemID);
        query_insert.Parameters.AddWithValue("@name", item.ItemName);
        query_insert.Parameters.AddWithValue("@price", item.Price);
        query_insert.Parameters.AddWithValue("@amount", item.Amount);
        query_insert.Parameters.AddWithValue("@durability", item.Durability);
        query_insert.Parameters.AddWithValue("@state", item.Base64State);
        query_insert.Parameters.AddWithValue("@sellerID", item.SellerID);

        int insertedID = Convert.ToInt32(await query_insert.ExecuteScalarAsync());

        return insertedID;
    }

    public async Task<bool> RemoveItem(int id)
    {
        const string sql_delete = "DELETE FROM `smarketplace_Item` WHERE `ID` = @id;";

        MySqlCommand query_delete = new(sql_delete, _connection);
        query_delete.Parameters.AddWithValue("@id", id);

        int rowsAffected = await query_delete.ExecuteNonQueryAsync();

        return rowsAffected > 0;
    }

    public async Task AddLog(MarketplaceItem soldItem, ulong buyerID, bool paid)
    {
        const string sql_insert = "INSERT INTO `smarketplace_Log` (`ItemID`, `ItemName`, `ItemPrice`, `SellerID`, `BuyerID`, `Paid`) VALUES (@id, @name, @price, @sellerID, @buyerID, @paid);";

        MySqlCommand query_insert = new(sql_insert, _connection);
        query_insert.Parameters.AddWithValue("@id", soldItem.ItemID);
        query_insert.Parameters.AddWithValue("@name", soldItem.ItemName);
        query_insert.Parameters.AddWithValue("@price", soldItem.Price);
        query_insert.Parameters.AddWithValue("@sellerID", soldItem.SellerID);
        query_insert.Parameters.AddWithValue("@buyerID", buyerID);
        query_insert.Parameters.AddWithValue("@paid", paid ? 1 : 0);

        await query_insert.ExecuteNonQueryAsync();
    }

    public async Task<int> GetPendingPaids(ulong playerID)
    {
        const string sql_select = "SELECT `ItemPrice` FROM `smarketplace_Log` WHERE `SellerID` = @playerID AND `Paid` = 0;";

        MySqlCommand query_select = new(sql_select, _connection);
        query_select.Parameters.AddWithValue("@playerID", playerID);

        DbDataReader reader = await query_select.ExecuteReaderAsync();

        if (!reader.HasRows)
        {
            reader.Close();
            return 0;
        }

        int pendingPaid = 0;

        while(await reader.ReadAsync())
        {
            int itemPrice = reader.GetInt32(0);

            pendingPaid += itemPrice;
        }

        reader.Close();
        return pendingPaid;
    }

    public async Task UpdatePendingPaids(ulong playerID)
    {
        const string sql_update = "UPDATE `smarketplace_Log` SET `Paid` = 1 WHERE `SellerID` = @playerID;";

        MySqlCommand query_update = new(sql_update, _connection);
        query_update.Parameters.AddWithValue("@playerID", playerID);

        await query_update.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
