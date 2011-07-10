using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlServerCe;

namespace pHMb.Router.Loggers
{
    public class BandwidthUsage : ILogger
    {
        private DateTime _lastUpdateTime;
        private uint[] _lastBytesTransferred;
        private bool _firstLog = true;

        #region ILogger Members
        public string Name
        {
            get { return "Bandwidth Usage"; }
        }

        public void Log(Interfaces.IRouterInterface routerCommand, SqlCeConnection databaseConnection)
        {
            uint[] bytesTransferred;
            uint[] bytesSinceLastUpdate = new uint[2];
            double uptime;

            try
            {
                bytesTransferred = routerCommand.RouterInfo.BytesTransferred;
                uptime = routerCommand.RouterInfo.Uptime;

                if (_firstLog || _lastUpdateTime < DateTime.Now - new TimeSpan(0, 10, 0))
                {
                    _lastUpdateTime = DateTime.Now;
                    _firstLog = false;
                }
                else
                {
                    for (int i = 0; i <= 1; i++)
                    {
                        if (uptime > 90)
                        {
                            if (bytesTransferred[i] < _lastBytesTransferred[i])
                            {
                                // Value has exceeded capacity of int32 (used by ifconfig)
                                bytesSinceLastUpdate[i] = (bytesTransferred[i] + (2 ^ 32 - 1)) - _lastBytesTransferred[i];
                            }
                            else
                            {
                                bytesSinceLastUpdate[i] = bytesTransferred[i] - _lastBytesTransferred[i];
                            }
                        }
                        else
                        {
                            // Uptime is less than 1min 30, router has been reset since last grab (extra 30 seconds to allow for delays)
                            bytesSinceLastUpdate[i] = bytesTransferred[i];
                        }
                    }

                    // Check value isn't rediculous:
                    if ((DateTime.Now - _lastUpdateTime).Seconds > 0)
                    {
                        long upSpeed = bytesSinceLastUpdate[0] / (DateTime.Now - _lastUpdateTime).Seconds;
                        long downSpeed = bytesSinceLastUpdate[1] / (DateTime.Now - _lastUpdateTime).Seconds;
                        if ((downSpeed > 3932160) ||
                            (upSpeed > 3932160))
                        {
                            throw new ApplicationException("Speed out of range!");
                        }
                    }

                    // Add value to database
                    string sqlQuery = string.Format("INSERT INTO BandwidthUsage (startTime, endTime, usageUp, usageDown) VALUES ('{0:MM/dd/yyyy HH:mm:ss}', '{1:MM/dd/yyyy HH:mm:ss}', {2:g}, {3:g})",
                        _lastUpdateTime, DateTime.Now, bytesSinceLastUpdate[0], bytesSinceLastUpdate[1]);

                    _lastUpdateTime = DateTime.Now;

                    SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection);
                    cmd.ExecuteNonQuery();
                }
                _lastBytesTransferred = bytesTransferred;
            }
            catch (Exception ex)
            {
                if (!_firstLog)
                {
                    // Could not grab data
                    _firstLog = true;

                    // Add value to database
                    string sqlQuery = string.Format("INSERT INTO BandwidthUsage (startTime, endTime, usageUp, usageDown) VALUES ('{0:MM/dd/yyyy HH:mm:ss}', '{1:MM/dd/yyyy HH:mm:ss}', NULL, NULL)",
                        _lastUpdateTime, DateTime.Now);

                    SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    _lastUpdateTime = DateTime.Now;
                }

                throw new Exceptions.LoggerException("Error grabbing bandwidth usage", ex, this.Name);
            }
        }

        /// <summary>
        /// Consolidates all your existing loans into one easy to pay monthly statement ... or consolidates the logs
        /// into a certain time period ... one of the two.
        /// </summary>
        /// <param name="limit">The date at which to start consolidating (all entries before this date will be consolidated)</param>
        /// <param name="resolution">The time span to consiladate to</param>
        public void Consolidate(DateTime limit, TimeSpan resolution, SqlCeConnection databaseConnection)
        {
            // First get the consolidated values
            string sqlQuery = string.Format(@"SELECT        MIN(startTime) AS startDate, MAX(endTime) AS endDate, SUM(usageUp) AS tUsageUp, SUM(usageDown) AS tUsageDown
                                              FROM          BandwidthUsage
                                              WHERE         (startTime < '{0:MM/dd/yyyy HH:mm:ss}')
                                              GROUP BY DATEADD(second, DATEDIFF(second, '01/01/2009', startTime) / {1:G0} * {1:G0}, '01/01/2009')
                                              HAVING        (COUNT(*) > 1)",
                                              limit, resolution.TotalSeconds);

            using (SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection))
            {
                using (SqlCeDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // Delete old, non-consolidated values
                        sqlQuery = string.Format(@"DELETE FROM BandwidthUsage
                                                   WHERE  (startTime >= '{0:MM/dd/yyyy HH:mm:ss}') AND (endTime <= '{1:MM/dd/yyyy HH:mm:ss}')",
                                                   reader.GetDateTime(0), reader.GetDateTime(1));
                        using (SqlCeCommand subCmd = new SqlCeCommand(sqlQuery, databaseConnection))
                        {
                            subCmd.ExecuteNonQuery();
                        }

                        // Insert consolidated values
                        sqlQuery = string.Format(@"INSERT INTO BandwidthUsage (startTime, endTime, usageUp, usageDown) 
                                                   VALUES ('{0:MM/dd/yyyy HH:mm:ss}', '{1:MM/dd/yyyy HH:mm:ss}', {2:G}, {3:G})",
                                                   reader.GetDateTime(0), reader.GetDateTime(1), reader.GetSqlInt64(2), reader.GetSqlInt64(3));
                        using (SqlCeCommand subCmd = new SqlCeCommand(sqlQuery, databaseConnection))
                        {
                            subCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        public SqlCeDataReader Retrieve(DateTime startDate, DateTime endDate, SqlCeConnection databaseConnection)
        {
            string sqlQuery = string.Format("SELECT * FROM BandwidthUsage WHERE (startTime > '{0:MM/dd/yyyy HH:mm:ss}') AND (endTime < '{1:MM/dd/yyyy HH:mm:ss}') ORDER BY startTime DESC", startDate, endDate);
            SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection);

            return cmd.ExecuteReader();
        }

        public SqlCeDataReader GetAggregateValues(DateTime startDate, DateTime endDate, SqlCeConnection databaseConnection)
        {
            string sqlQuery = string.Format(@"SELECT        SUM(usageUp) AS totalUsageUp, 
                                                            SUM(usageDown) AS totalUsageDown, 
                                                            AVG(usageUp / DATEDIFF(second, startTime, endTime)) AS avgUpSpeed, 
                                                            AVG(usageDown /  DATEDIFF(second, startTime, endTime)) AS avgDownSpeed

                                                FROM        BandwidthUsage
                                                WHERE        (startTime > '{0:MM/dd/yyyy HH:mm:ss}') AND (endTime < '{1:MM/dd/yyyy HH:mm:ss}')", startDate, endDate);
            SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection);

            return cmd.ExecuteReader();
        }
        #endregion
    }
}
