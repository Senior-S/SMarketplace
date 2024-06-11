using MySql.Data.MySqlClient;
using Rocket.Core.Logging;
using Rocket.Core.Utils;
using SeniorS.SMarketplace.Models;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace SeniorS.SMarketplace.Services;
public class MySQLManager : IDisposable
{
    private MySqlConnection _connection;
    private string _tablePrefix;

    public MySQLManager()
    {
        try
        {
            _connection = new MySqlConnection(SMarketplace.Instance.connectionString);
            _tablePrefix = SMarketplace.Instance.Configuration.Instance.dbTablePrefix;
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
        string sql_table_items = $"CREATE TABLE IF NOT EXISTS `{_tablePrefix}Item` (`ID` SERIAL, `ItemID` SMALLINT UNSIGNED NOT NULL, `ItemName` VARCHAR(255) NOT NULL, `ItemPrice` INT NOT NULL, `ItemAmount` TINYINT UNSIGNED NOT NULL, `ItemDurability` TINYINT UNSIGNED NOT NULL, `ItemState` VARCHAR(172) DEFAULT NULL, `SellerID` BIGINT UNSIGNED NOT NULL, `SellerName` VARCHAR(255) NOT NULL, PRIMARY KEY (`ID`));";
        string sql_table_logs = $"CREATE TABLE IF NOT EXISTS `{_tablePrefix}Log` (`ID` SERIAL, `ItemID` SMALLINT UNSIGNED NOT NULL, `ItemName` VARCHAR(255) NOT NULL, `ItemPrice` INT NOT NULL, `SellerID` BIGINT UNSIGNED NOT NULL, `SellerName` VARCHAR(255) NOT NULL, `BuyerID` BIGINT UNSIGNED NOT NULL, `BuyerName` VARCHAR(255) NOT NULL, `Paid` TINYINT(1) NOT NULL, PRIMARY KEY (`ID`));";

        MySqlCommand query_table_items = new(sql_table_items, _connection);
        MySqlCommand query_table_logs = new(sql_table_logs, _connection);

        query_table_items.ExecuteNonQuery();
        query_table_logs.ExecuteNonQuery();

        string sql_alter_items = $"ALTER TABLE `{_tablePrefix}Item` ADD COLUMN `SellerName` VARCHAR(255) NOT NULL AFTER `SellerID`;";
        string sql_alter_logs_seller = $"ALTER TABLE `{_tablePrefix}Log` ADD COLUMN `SellerName` VARCHAR(255) NOT NULL AFTER `SellerID`;";
        string sql_alter_logs_buyer = $"ALTER TABLE `{_tablePrefix}Log` ADD COLUMN `BuyerName` VARCHAR(255) NOT NULL AFTER `BuyerID`;";

        MySqlCommand query_alter_items = new(sql_alter_items, _connection);
        MySqlCommand query_alter_logs_seller = new(sql_alter_logs_seller, _connection);
        MySqlCommand query_alter_logs_buyer = new(sql_alter_logs_buyer, _connection);

        try
        {
            query_alter_items.ExecuteNonQuery();
            query_alter_logs_seller.ExecuteNonQuery();
            query_alter_logs_buyer.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            if(ex.Number == 1060)
            {
                // The table was already altered so ignore the issue.
                // TODO: Remove this for update 2.0.0 due everyone should already have the updated table for when that version comes out.
                return;
            }
            TaskDispatcher.QueueOnMainThread(() =>
            {
                Logger.LogException(ex, "Unexpected MySQL error!");
            });
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

    public async Task<List<MarketplaceItem>> GetItems()
    {
        string sql_select = $"SELECT * FROM `{_tablePrefix}Item`;";

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
            string sellerName = reader.GetString(8);

            MarketplaceItem item = new (id, itemID, itemName, itemPrice, sellerID, sellerName.Length < 1 ? sellerID.ToString() : sellerName, amount, durability, stateBase64);
            marketplaceItems.Add(item);
        }

        reader.Close();
        return marketplaceItems;
    }

    public async Task<int> AddItem(MarketplaceItem item)
    {
        string sql_insert = $"INSERT INTO `{_tablePrefix}Item` (`ItemID`, `ItemName`, `ItemPrice`, `ItemAmount`, `ItemDurability`, `ItemState`, `SellerID`, `SellerName`) VALUES (@id, @name, @price, @amount, @durability, @state, @sellerID, @sellerName); SELECT LAST_INSERT_ID();";

        MySqlCommand query_insert = new(sql_insert, _connection);
        query_insert.Parameters.AddWithValue("@id", item.ItemID);
        query_insert.Parameters.AddWithValue("@name", item.ItemName);
        query_insert.Parameters.AddWithValue("@price", item.Price);
        query_insert.Parameters.AddWithValue("@amount", item.Amount);
        query_insert.Parameters.AddWithValue("@durability", item.Durability);
        query_insert.Parameters.AddWithValue("@state", item.Base64State);
        query_insert.Parameters.AddWithValue("@sellerID", item.SellerID);
        query_insert.Parameters.AddWithValue("@sellerName", item.SellerName);

        int insertedID = Convert.ToInt32(await query_insert.ExecuteScalarAsync());

        return insertedID;
    }

    public async Task<bool> RemoveItem(int id)
    {
        string sql_delete = $"DELETE FROM `{_tablePrefix}Item` WHERE `ID` = @id;";

        MySqlCommand query_delete = new(sql_delete, _connection);
        query_delete.Parameters.AddWithValue("@id", id);

        int rowsAffected = await query_delete.ExecuteNonQueryAsync();

        return rowsAffected > 0;
    }

    public async Task<long> GetTotalLogs()
    {
        string sql_select = $"SELECT COUNT(*) FROM `{_tablePrefix}Log`;";

        MySqlCommand query_select = new(sql_select, _connection);

        object obj = await query_select.ExecuteScalarAsync();
        long totalLogs = (long)obj;

        return totalLogs;
    }

    public async Task AddLog(MarketplaceItem soldItem, ulong buyerID, string buyerName, bool paid)
    {
        string sql_insert = $"INSERT INTO `{_tablePrefix}Log` (`ItemID`, `ItemName`, `ItemPrice`, `SellerID`, `SellerName`, `BuyerID`, `BuyerName`, `Paid`) VALUES (@id, @name, @price, @sellerID, @sellerName, @buyerID, @buyerName, @paid);";

        MySqlCommand query_insert = new(sql_insert, _connection);
        query_insert.Parameters.AddWithValue("@id", soldItem.ItemID);
        query_insert.Parameters.AddWithValue("@name", soldItem.ItemName);
        query_insert.Parameters.AddWithValue("@price", soldItem.Price);
        query_insert.Parameters.AddWithValue("@sellerID", soldItem.SellerID);
        query_insert.Parameters.AddWithValue("@sellerName", soldItem.SellerName);
        query_insert.Parameters.AddWithValue("@buyerID", buyerID);
        query_insert.Parameters.AddWithValue("@buyerName", buyerName);
        query_insert.Parameters.AddWithValue("@paid", paid ? 1 : 0);

        await query_insert.ExecuteNonQueryAsync();
    }

    public async Task<List<SellLog>> GetLatestLogs()
    {
        string sql_select = $"SELECT ItemName, ItemPrice, SellerName, BuyerName FROM `{_tablePrefix}Log` ORDER BY `ID` DESC LIMIT 10;";

        MySqlCommand query_select = new(sql_select, _connection);

        DbDataReader reader = await query_select.ExecuteReaderAsync();

        if (!reader.HasRows)
        {
            reader.Close();
            return new();
        }

        List<SellLog> logs = new();

        while (await reader.ReadAsync())
        {
            string itemName = reader.GetString(0);
            int itemPrice = reader.GetInt32(1);
            string sellerName = reader.GetString(2);
            string buyerName = reader.GetString(3);

            logs.Add(new(itemName, itemPrice, sellerName, buyerName));
        }

        reader.Close();
        return logs;
    }

    public async Task<Dictionary<ushort, string>> GetDistinctItems()
    {
        string sql_select = $"SELECT DISTINCT `ItemID`, `ItemName` FROM `{_tablePrefix}Item`;";

        MySqlCommand query_select = new(sql_select, _connection);
        DbDataReader reader = await query_select.ExecuteReaderAsync();

        if (!reader.HasRows)
        {
            reader.Close();
            return new();
        }

        Dictionary<ushort, string> items = new();

        while (await reader.ReadAsync())
        {
            ushort itemID = (ushort)reader.GetValue(0);
            string itemName = reader.GetString(1);

            items.Add(itemID, itemName);
        }

        reader.Close();
        return items;
    }

    public async Task<int> GetPendingPaids(ulong playerID)
    {
        string sql_select = $"SELECT `ItemPrice` FROM `{_tablePrefix}Log` WHERE `SellerID` = @playerID AND `Paid` = 0;";

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
        string sql_update = $"UPDATE `{_tablePrefix}Log` SET `Paid` = 1 WHERE `SellerID` = @playerID;";

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
