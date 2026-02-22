using Dapper;
using Microsoft.Data.Sqlite;
using Spc.Api.Models;

namespace Spc.Api.Data;

public class SpcRepository
{
    private readonly string _connectionString;

    public SpcRepository()
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "spc.db");
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Measurements (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MachineId VARCHAR(100) NOT NULL,
                ItemName VARCHAR(100) NOT NULL,
                Value REAL NOT NULL,
                Timestamp TEXT NOT NULL
            );
            
            CREATE INDEX IF NOT EXISTS idx_measurement_machine_item 
            ON Measurements(MachineId, ItemName);
        ");
    }

    public async Task<long> AddMeasurementAsync(Measurement m)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = @"INSERT INTO Measurements (MachineId, ItemName, Value, Timestamp) 
                    VALUES (@MachineId, @ItemName, @Value, @Timestamp);
                    SELECT last_insert_rowid();";
        
        return await connection.ExecuteScalarAsync<long>(sql, new {
            m.MachineId,
            m.ItemName,
            m.Value,
            Timestamp = m.Timestamp.ToString("o")
        });
    }

    public async Task<List<Measurement>> GetRecentMeasurementsAsync(string machineId, string itemName, int limit = 30)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = @"SELECT Id, MachineId, ItemName, Value, Timestamp 
                    FROM Measurements 
                    WHERE MachineId = @MachineId AND ItemName = @ItemName
                    ORDER BY Timestamp DESC 
                    LIMIT @Limit";
        
        var results = await connection.QueryAsync<dynamic>(sql, new { MachineId = machineId, ItemName = itemName, Limit = limit });
        
        return results.Select(r => new Measurement {
            Id = (long)r.Id,
            MachineId = r.MachineId,
            ItemName = r.ItemName,
            Value = (double)r.Value,
            Timestamp = DateTime.Parse(r.Timestamp)
        }).OrderBy(m => m.Timestamp).ToList();
    }

    public async Task<(double Mean, double StdDev, double? Ucl, double? Lcl)> 
        GetStatisticsAsync(string machineId, string itemName, int limit = 30)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = @"SELECT 
                        AVG(Value) as Mean,
                        (SELECT SQRT(AVG(Value * Value) - AVG(Value) * AVG(Value)) FROM Measurements 
                         WHERE MachineId = @MachineId AND ItemName = @ItemName
                         ORDER BY Timestamp DESC LIMIT @Limit) as StdDev
                    FROM Measurements 
                    WHERE MachineId = @MachineId AND ItemName = @ItemName
                    ORDER BY Timestamp DESC LIMIT 1";
        
        using var reader = await connection.ExecuteReaderAsync(sql, new { MachineId = machineId, ItemName = itemName, Limit = limit });
        
        if (await reader.ReadAsync())
        {
            var mean = reader.GetDouble(0);
            var stdDev = reader.IsDBNull(1) ? 0.0 : reader.GetDouble(1);
            var ucl = mean + 3 * stdDev;
            var lcl = mean - 3 * stdDev;
            return (mean, stdDev, ucl, lcl);
        }
        
        return (0, 0, null, null);
    }

    public async Task<int> ClearMeasurementsAsync(string? machineId = null, string? itemName = null)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql;
        if (!string.IsNullOrEmpty(machineId) && !string.IsNullOrEmpty(itemName))
        {
            sql = "DELETE FROM Measurements WHERE MachineId = @MachineId AND ItemName = @ItemName";
            return await connection.ExecuteAsync(sql, new { MachineId = machineId, ItemName = itemName });
        }
        else if (!string.IsNullOrEmpty(machineId))
        {
            sql = "DELETE FROM Measurements WHERE MachineId = @MachineId";
            return await connection.ExecuteAsync(sql, new { MachineId = machineId });
        }
        else
        {
            sql = "DELETE FROM Measurements";
            return await connection.ExecuteAsync(sql);
        }
    }
}
