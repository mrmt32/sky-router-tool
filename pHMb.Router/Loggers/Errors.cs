using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlServerCe;

namespace pHMb.Router.Loggers
{
    public class Errors : ILogger
    {
        private bool _isFirstLog = true;
        private Error _lastErrors;
        private DateTime _lastUpdateTime;

        #region ILogger Members

        public string Name
        {
            get { return "Errors"; }
        }

        public void Log(pHMb.Router.Interfaces.IRouterInterface routerCommand, SqlCeConnection databaseConnection)
        {
            Error totalErrors;
            Error errorsSinceLastUpdate;

            if (_isFirstLog)
            {
                try
                {
                    string sqlQuery = "CREATE TABLE Errors (startTime datetime, endTime datetime, crcErrors int, losErrors int, lofErrors int, erroredSeconds int)";
                    SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection);
                    cmd.ExecuteNonQuery();
                }
                catch (SqlCeException ex)
                {
                    if (!ex.Message.StartsWith("The specified table already exists"))
                    {
                        throw ex;
                    }
                }
            }

            try
            {
                totalErrors = routerCommand.RouterInfo.ConnectionDetails.Errors.Total;

                if (routerCommand.RouterInfo.ConnectionDetails.Status.ToUpper() == "SHOWTIME")
                {
                    if (_isFirstLog || _lastUpdateTime < DateTime.Now - new TimeSpan(0, 10, 0))
                    {
                        _lastUpdateTime = DateTime.Now;
                        _isFirstLog = false;
                    }
                    else
                    {
                        errorsSinceLastUpdate = new Error();
                        errorsSinceLastUpdate.Crc = Math.Max(0, totalErrors.Crc - _lastErrors.Crc);
                        errorsSinceLastUpdate.Es = Math.Max(0, totalErrors.Es - _lastErrors.Es);
                        errorsSinceLastUpdate.Lof = Math.Max(0, totalErrors.Lof - _lastErrors.Lof);
                        errorsSinceLastUpdate.Los = Math.Max(0, totalErrors.Los - _lastErrors.Los);

                        // Add value to database
                        string sqlQuery = string.Format(@"INSERT INTO Errors (startTime, endTime, crcErrors, losErrors, lofErrors, erroredSeconds) 
                                                      VALUES ('{0:MM/dd/yyyy HH:mm:ss}', '{1:MM/dd/yyyy HH:mm:ss}', {2:g}, {3:g}, {4:g}, {5:g})",
                            _lastUpdateTime, DateTime.Now, errorsSinceLastUpdate.Crc, errorsSinceLastUpdate.Los, errorsSinceLastUpdate.Lof, errorsSinceLastUpdate.Es);

                        _lastUpdateTime = DateTime.Now;

                        SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection);
                        cmd.ExecuteNonQuery();
                    }
                    _lastErrors = totalErrors;
                }
            }
            catch (Exception ex)
            {
                if (!_isFirstLog)
                {
                    // Could not grab data
                    _isFirstLog = true;

                    // Add value to database
                    string sqlQuery = string.Format("INSERT INTO Errors (startTime, endTime, crcErrors, losErrors, lofErrors, erroredSeconds) VALUES ('{0:MM/dd/yyyy HH:mm:ss}', '{1:MM/dd/yyyy HH:mm:ss}', NULL, NULL, NULL, NULL)",
                        _lastUpdateTime, DateTime.Now);

                    SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    _lastUpdateTime = DateTime.Now;
                }

                throw new Exceptions.LoggerException("Error grabbing errors", ex, this.Name);
            }
        }

        public void Consolidate(DateTime limit, TimeSpan resolution, SqlCeConnection databaseConnection)
        {
            // First get the consolidated values
            string sqlQuery = string.Format(@"SELECT        MIN(startTime) AS startDate, MAX(endTime) AS endDate, SUM(crcErrors), SUM(losErrors), SUM(lofErrors), SUM(erroredSeconds)
                                              FROM          Errors
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
                        sqlQuery = string.Format(@"DELETE FROM Errors
                                                   WHERE  (startTime >= '{0:MM/dd/yyyy HH:mm:ss}') AND (endTime <= '{1:MM/dd/yyyy HH:mm:ss}')",
                                                   reader.GetDateTime(0), reader.GetDateTime(1));
                        using (SqlCeCommand subCmd = new SqlCeCommand(sqlQuery, databaseConnection))
                        {
                            subCmd.ExecuteNonQuery();
                        }

                        // Insert consolidated values
                        sqlQuery = string.Format(@"INSERT INTO Errors (startTime, endTime, crcErrors, losErrors, lofErrors, erroredSeconds) 
                                                   VALUES ('{0:MM/dd/yyyy HH:mm:ss}', '{1:MM/dd/yyyy HH:mm:ss}', {2:G}, {3:G}, {4:G}, {5:G})",
                                                   reader.GetDateTime(0), reader.GetDateTime(1), reader.GetSqlInt32(2), reader.GetSqlInt32(3), reader.GetSqlInt32(4), reader.GetSqlInt32(5));
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
            string sqlQuery = string.Format("SELECT * FROM Errors WHERE (startTime > '{0:MM/dd/yyyy HH:mm:ss}') AND (endTime < '{1:MM/dd/yyyy HH:mm:ss}') ORDER BY startTime DESC", startDate, endDate);
            SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection);

            return cmd.ExecuteReader();
        }

        public SqlCeDataReader GetAggregateValues(DateTime startDate, DateTime endDate, SqlCeConnection databaseConnection)
        {
            string sqlQuery = string.Format(@"SELECT        SUM(crcErrors) AS totalCrcErrors, 
                                                            SUM(losErrors) AS totalLosErrors, 
                                                            SUM(lofErrors) AS totalLofErrors, 
                                                            SUM(erroredSeconds) AS totalErroredSeconds, 
                                                            AVG((crcErrors * 3600) / DATEDIFF(second, startTime, endTime)) AS avgCrcErrors, 
                                                            AVG((losErrors * 3600) /  DATEDIFF(second, startTime, endTime)) AS avgLosErrors,
                                                            AVG((lofErrors * 3600) /  DATEDIFF(second, startTime, endTime)) AS avgLofErrors,
                                                            AVG((erroredSeconds * 3600) /  DATEDIFF(second, startTime, endTime)) AS avgErroredSeconds

                                                FROM        Errors
                                                WHERE        (startTime > '{0:MM/dd/yyyy HH:mm:ss}') AND (endTime < '{1:MM/dd/yyyy HH:mm:ss}')", startDate, endDate);
            SqlCeCommand cmd = new SqlCeCommand(sqlQuery, databaseConnection);

            return cmd.ExecuteReader();
        }

        #endregion
    }
}
