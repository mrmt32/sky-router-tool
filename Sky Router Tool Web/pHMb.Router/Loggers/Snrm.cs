using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlServerCe;

namespace pHMb.Router.Loggers
{
    public class Snrm : ILogger
    {
        private DateTime _lastUpdateTime;
        private decimal[] _lastValues = { -1, -1 };

        private void InsertNull(SqlCeConnection databaseConnection)
        {
            string sqlQuery = string.Format("INSERT INTO Snrm (time, snrmUp, snrmDown) VALUES ('{0:MM/dd/yyyy HH:mm:ss}', NULL, NULL)",
                        DateTime.Now);

            using (SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #region ILogger Members
        public string Name
        {
            get { return "SNR Margin"; }
        }

        public void Log(Interfaces.IRouterInterface routerCommand, SqlCeConnection databaseConnection)
        {
            try
            {
                if (_lastUpdateTime > DateTime.Now - new TimeSpan(0, 10, 0))
                {
                    ConnectionDetails connectionDetails = routerCommand.RouterInfo.ConnectionDetails;
                    decimal UpMargin = connectionDetails.UpstreamSync.SnrMargin;
                    decimal DownMargin = connectionDetails.DownstreamSync.SnrMargin;

                    if (_lastValues[0] == -1)
                    {
                        _lastValues[0] = UpMargin;
                        _lastValues[1] = DownMargin;
                    }

                    // Prevent large spikes (almost always anomolous), if a difference in SNRM over 20db is detected
                    // then the current reading is discarded.
                    if (connectionDetails.Status.ToUpper() == "SHOWTIME" && (Math.Abs(UpMargin - _lastValues[0]) < 20) && (Math.Abs(DownMargin - _lastValues[1]) < 20))
                    {
                        string sqlQuery = string.Format("INSERT INTO Snrm (time, snrmUp, snrmDown) VALUES ('{0:MM/dd/yyyy HH:mm:ss}', {1:N1}, {2:N1})",
                            DateTime.Now, UpMargin, DownMargin);

                        using (SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        _lastValues[0] = UpMargin;
                        _lastValues[1] = DownMargin;
                    }
                    else
                    {
                        InsertNull(databaseConnection);
                    }
                }
                _lastUpdateTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                // Could not grab data
                InsertNull(databaseConnection);

                throw new Exceptions.LoggerException("Error grabbing SNR margin", ex, this.Name);
            }
        }

        public void Consolidate(DateTime limit, TimeSpan resolution, SqlCeConnection databaseConnection)
        {
            // First get the consolidated values
            string sqlQuery = string.Format(@"SELECT        DATEADD(second, DATEDIFF(second, MIN(time), MAX(time)) / 2, MIN(time)), AVG(snrmDown), AVG(snrmUp), MIN(time), MAX(time)
                                              FROM          Snrm
                                              WHERE         (time < '{0:MM/dd/yyyy HH:mm:ss}')
                                              GROUP BY DATEADD(second, DATEDIFF(second, '01/01/2009', time) / {1:G0} * {1:G0}, '01/01/2009')
                                              HAVING        (COUNT(*) > 1)",
                                              limit, resolution.TotalSeconds);

            using (SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection))
            {
                using (SqlCeDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // Delete old, non-consolidated values
                        sqlQuery = string.Format(@"DELETE FROM Snrm
                                                   WHERE  (time >= '{0:MM/dd/yyyy HH:mm:ss}') AND (time <= '{1:MM/dd/yyyy HH:mm:ss}')",
                                                   reader.GetDateTime(3), reader.GetDateTime(4));
                        using (SqlCeCommand subCmd = new SqlCeCommand(sqlQuery, databaseConnection))
                        {
                            subCmd.ExecuteNonQuery();
                        }

                        // Insert consolidated values
                        sqlQuery = string.Format(@"INSERT INTO Snrm (time, snrmDown, snrmUp) 
                                                   VALUES ('{0:MM/dd/yyyy HH:mm:ss}', {1:G2}, {2:G2})",
                                                   reader.GetDateTime(0), reader.GetSqlDouble(1), reader.GetSqlDouble(2));
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
            string sqlQuery = string.Format("SELECT * FROM Snrm WHERE (time > '{0:MM/dd/yyyy HH:mm:ss}') AND (time < '{1:MM/dd/yyyy HH:mm:ss}') ORDER BY time DESC", startDate, endDate);
            SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection);

            return cmd.ExecuteReader();
        }

        public SqlCeDataReader GetAggregateValues(DateTime startDate, DateTime endDate, SqlCeConnection databaseConnection)
        {
            string sqlQuery = string.Format(@"SELECT    AVG(snrmDown) AS avgSnrmDown, AVG(snrmUp) AS avgSnrmUp
                                                FROM    Snrm 
                                                WHERE (time > '{0:MM/dd/yyyy HH:mm:ss}') AND (time < '{1:MM/dd/yyyy HH:mm:ss}')", startDate, endDate);
            SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection);

            return cmd.ExecuteReader();
        }
        #endregion
    }
}
