using System;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace SensorMonitor
{
    /// <summary>
    /// SQLite 数据库管理类 — 负责传感器数据的持久化存储与查询
    /// </summary>
    public class DbManager
    {
        private readonly string _connStr;

        public DbManager(string dbPath)
        {
            _connStr = $"Data Source={dbPath};Version=3;";
            InitDb();
        }

        private void InitDb()
        {
            using (var conn = new SQLiteConnection(_connStr))
            {
                conn.Open();
                string sql = @"
                    CREATE TABLE IF NOT EXISTS sensor_data (
                        id        INTEGER PRIMARY KEY AUTOINCREMENT,
                        channel   INTEGER NOT NULL,
                        temp      REAL NOT NULL,
                        humidity  REAL NOT NULL,
                        timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                    );
                    CREATE INDEX IF NOT EXISTS idx_channel_time
                        ON sensor_data(channel, timestamp);
                ";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>插入一条传感器数据</summary>
        public void Insert(int channel, double temp, double humidity)
        {
            using (var conn = new SQLiteConnection(_connStr))
            {
                conn.Open();
                string sql = "INSERT INTO sensor_data(channel, temp, humidity) VALUES(@ch, @t, @h)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ch", channel);
                    cmd.Parameters.AddWithValue("@t", Math.Round(temp, 2));
                    cmd.Parameters.AddWithValue("@h", Math.Round(humidity, 2));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>按时间范围查询数据，返回 DataTable</summary>
        public DataTable Query(DateTime from, DateTime to, int? channel = null)
        {
            using (var conn = new SQLiteConnection(_connStr))
            {
                conn.Open();
                string sql = @"
                    SELECT channel, temp, humidity, timestamp
                    FROM sensor_data
                    WHERE timestamp BETWEEN @f AND @t
                ";
                if (channel.HasValue)
                    sql += " AND channel = @ch";
                sql += " ORDER BY timestamp ASC";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@f", from.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@t", to.ToString("yyyy-MM-dd HH:mm:ss"));
                    if (channel.HasValue)
                        cmd.Parameters.AddWithValue("@ch", channel.Value);

                    var da = new SQLiteDataAdapter(cmd);
                    var dt = new DataTable();
                    da.Fill(dt);
                    return dt;
                }
            }
        }

        /// <summary>将查询结果导出为 CSV 文件</summary>
        public void ExportCsv(DataTable dt, string filePath)
        {
            using (var sw = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                // 写表头
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    sw.Write(dt.Columns[i].ColumnName);
                    if (i < dt.Columns.Count - 1) sw.Write(",");
                }
                sw.WriteLine();

                // 写数据
                foreach (DataRow row in dt.Rows)
                {
                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        sw.Write(row[i].ToString());
                        if (i < dt.Columns.Count - 1) sw.Write(",");
                    }
                    sw.WriteLine();
                }
            }
        }
    }
}
