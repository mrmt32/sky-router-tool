using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlServerCe;
using System.Data;
using System.Threading;
using System.Reflection;

namespace pHMb.Router
{
    /// <summary>
    /// Polls the router periodicaly for information and stores it
    /// </summary>
    public class RouterPoll
    {
        #region Private Variables
        private string _routerType;
        private Thread _pollThread;
        private IRouterConnection _routerConnection;
        private Interfaces.IRouterInterface _routerCommand;

        private bool _isExcecuting = false;
        private bool _isStopping = false;
        #endregion

        #region Private Methods
        private void StartPoll()
        {
            PollLoop();
        }

        private void PollLoop()
        {
            try
            {
                using (SqlCeConnection databaseConnection = new SqlCeConnection(Properties.Settings.Default.routerlogsConnectionString))
                {
                    databaseConnection.Open();
                    while (true)
                    {
                        try
                        {
                            _isExcecuting = true;

                            // Clear command cache
                            _routerCommand.Update();

                            // Do the logging
                            foreach (ILogger logger in Loggers)
                            {
                                logger.Log(_routerCommand, databaseConnection);

                                // Perform consolidations
                                foreach (ConsolidationPeriod period in ConsolidationPeriods)
                                {
                                    logger.Consolidate(DateTime.Now - period.Period, period.Resolution, databaseConnection);
                                }
                            }

                            // Check for cancelation
                            _isExcecuting = false;

                            if (_isStopping)
                            {
                                break;
                            }


                        }
                        catch (Exceptions.LoggerException ex)
                        {
                            OnLogEvent(String.Format("Exception while running logger '{0}': {1}\r\n", ex.LoggerName, ex));
                        }

                        // Sleep
                        Thread.Sleep(PollingInterval);
                     }
                }
            }
            catch (ThreadAbortException)
            {
                _routerCommand = null;
            }
        }
        #endregion

        #region Public Properties
        public List<ILogger> Loggers { get; set; }
        public List<ConsolidationPeriod> ConsolidationPeriods { get; private set; }
        public int PollingInterval { get; set; }
        #endregion

        #region Public Events
        public class LogEventArgs : EventArgs
        {
            public string LogText;
        }
        public event EventHandler<LogEventArgs> LogEvent;
        public void OnLogEvent(string text)
        {
            if (LogEvent != null)
            {
                LogEvent.Invoke(this, new LogEventArgs() { LogText = text });
            }
        } 
        #endregion

        #region Public Methods
        public RouterPoll(Interfaces.IRouterInterface routerCommand, string routerType)
        {
            _routerType = routerType;
            _routerCommand = routerCommand;
            PollingInterval = 60000;
            Loggers = new List<ILogger>();
            ConsolidationPeriods = new List<ConsolidationPeriod>();
        }

        /// <summary>
        /// Starts the polling loop
        /// </summary>
        public void Start()
        {
            if (_pollThread == null || !_pollThread.IsAlive)
            {
                ConsolidationPeriods = GetConsolidationRules();

                // Start polling thread
                _pollThread = new Thread(new ThreadStart(StartPoll));
                _pollThread.Name = "Polling Thread";
                _pollThread.Start();
            }
        }

        /// <summary>
        /// Stops the polling loop
        /// </summary>
        public void Stop()
        {
            if (_pollThread.IsAlive)
            {
                if (_isExcecuting)
                {
                    _isStopping = true;
                    if (!_pollThread.Join(2000))
                    {
                        _pollThread.Abort();
                        _pollThread.Join();
                    }
                }
                else
                {
                    _pollThread.Abort();
                    _pollThread.Join();
                }

                ConsolidationPeriods.Clear();
                _isStopping = false;
            }
        }

        public List<ConsolidationPeriod> GetConsolidationRules()
        {
            List<ConsolidationPeriod> consolidationPeriods = new List<ConsolidationPeriod>();
            using (SqlCeConnection sqlConnection = new SqlCeConnection(Properties.Settings.Default.settingsConnectionString))
            {
                sqlConnection.Open();

                string query = @"SELECT id, TimeBefore, Resolution
                                 FROM ConsolidationRules
                                 ORDER BY TimeBefore DESC";

                using (SqlCeCommand cmd = new SqlCeCommand(query, sqlConnection))
                {
                    using (SqlCeDataReader reader = cmd.ExecuteReader())
                    {
                        while(reader.Read())
                        {
                            ConsolidationPeriod consolidationPeriod = new ConsolidationPeriod();

                            consolidationPeriod.Id = (int)reader.GetValue(reader.GetOrdinal("id"));
                            consolidationPeriod.Period = new TimeSpan (0, 0, (int)reader.GetValue(reader.GetOrdinal("TimeBefore")));
                            consolidationPeriod.Resolution = new TimeSpan (0, 0, (int)reader.GetValue(reader.GetOrdinal("Resolution")));

                            consolidationPeriods.Add(consolidationPeriod);
                        }
                    }
                }
            }
            return consolidationPeriods;
        }

        public void RemoveConsolidationRule(int id)
        {
            using (SqlCeConnection sqlConnection = new SqlCeConnection(Properties.Settings.Default.settingsConnectionString))
            {
                sqlConnection.Open();

                string query = string.Format(@"DELETE FROM ConsolidationRules WHERE id='{0:G}'", id);

                using (SqlCeCommand cmd = new SqlCeCommand(query, sqlConnection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Updates or adds a new consolidation rule. To add a new rule leave the Id property of the 'period' parameter as 0.
        /// </summary>
        /// <param name="period"></param>
        public void UpdateConsolidationRule(ConsolidationPeriod period)
        {
            using (SqlCeConnection sqlConnection = new SqlCeConnection(Properties.Settings.Default.settingsConnectionString))
            {
                sqlConnection.Open();

                bool needToInsert = false;

                if (period.Id != 0)
                {
                    // See if the entry already exists
                    string query = string.Format(@"SELECT COUNT(*) FROM ConsolidationRules
                                                   WHERE id = '{0:G}'", period.Id);

                    using (SqlCeCommand cmd = new SqlCeCommand(query, sqlConnection))
                    {
                        if ((int)cmd.ExecuteScalar() >= 1)
                        {
                            // Entry exists, update it
                            query = string.Format(@"UPDATE ConsolidationRules SET TimeBefore = {0:G0}, Resolution = {1:G0} WHERE id={2:G0}",
                                period.Period.TotalSeconds, period.Resolution.TotalSeconds, period.Id);

                            using (SqlCeCommand updateCmd = new SqlCeCommand(query, sqlConnection))
                            {
                                updateCmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            needToInsert = true;
                        }
                    }
                }
                else
                {
                    needToInsert = true;
                }
                
                if (needToInsert)
                {
                    // Entry doesn't exist yet, add it
                    string query = string.Format(@"INSERT INTO ConsolidationRules (TimeBefore, Resolution) 
                                                   VALUES('{0:G0}', '{1:G0}')", period.Period.TotalSeconds, period.Resolution.TotalSeconds);

                    using (SqlCeCommand cmd = new SqlCeCommand(query, sqlConnection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Gets a new SqlCe connection to the logging database
        /// </summary>
        /// <returns>A new SqlCe connection</returns>
        public SqlCeConnection GetSqlConnection()
        {
            return new SqlCeConnection(Properties.Settings.Default.routerlogsConnectionString);
        }

        /// <summary>
        /// Retrieves a range of dates from a log as a SqlCeDataReader
        /// </summary>
        /// <param name="loggerName">Name of the logger to get the log from</param>
        /// <param name="startTime">Start date to retrieve</param>
        /// <param name="endTime">End date to retrieve</param>
        /// <returns></returns>
        public SqlCeDataReader Retrieve(string loggerName, DateTime startTime, DateTime endTime, SqlCeConnection databaseConnection)
        {
            return Loggers.First<ILogger>(x => x.Name == loggerName).Retrieve(startTime, endTime, databaseConnection);
        }

        /// <summary>
        /// Retrieves various aggregate values (logger depending) as a SqlCeDataReader
        /// </summary>
        /// <param name="loggerName">The name of the logger</param>
        /// <param name="startTime">Start date</param>
        /// <param name="endTime">End date</param>
        /// <returns></returns>
        public SqlCeDataReader GetAggregate(string loggerName, DateTime startTime, DateTime endTime, SqlCeConnection databaseConnection)
        {
            return Loggers.First<ILogger>(x => x.Name == loggerName).GetAggregateValues(startTime, endTime, databaseConnection);
        }

        /// <summary>
        /// Retrieves a range of dates from a log as an object
        /// </summary>
        /// <param name="loggerName">Name of the logger to get the log from</param>
        /// <param name="startTime">Start date to retrieve</param>
        /// <param name="endTime">End date to retrieve</param>
        /// <returns></returns>
        public List<Dictionary<string, object>> RetrieveAsObject(string loggerName, DateTime startTime, DateTime endTime)
        {
            List<Dictionary<string, object>> table = new List<Dictionary<string, object>>();

            using (SqlCeConnection databaseConnection = GetSqlConnection())
            {
                databaseConnection.Open();

                using (SqlCeDataReader reader = Retrieve(loggerName, startTime, endTime, databaseConnection))
                {
                    while (reader.Read())
                    {
                        Dictionary<string, object> row = new Dictionary<string, object>();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row.Add(reader.GetName(i), reader.GetValue(i));
                        }

                        table.Add(row);
                    }
                }
                
            }
            return table;
        }

        /// <summary>
        /// Retrieves various aggregate values (logger depending) as an object
        /// </summary>
        /// <param name="loggerName">The name of the logger</param>
        /// <param name="startTime">Start date</param>
        /// <param name="endTime">End date</param>
        /// <returns></returns>
        public List<Dictionary<string, object>> GetAggregateAsObject(string loggerName, DateTime startTime, DateTime endTime)
        {
            List<Dictionary<string, object>> table = new List<Dictionary<string, object>>();

            using (SqlCeConnection databaseConnection = GetSqlConnection())
            {
                databaseConnection.Open();

                using (SqlCeDataReader reader = GetAggregate(loggerName, startTime, endTime, databaseConnection))
                {
                    while (reader.Read())
                    {
                        Dictionary<string, object> row = new Dictionary<string, object>();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row.Add(reader.GetName(i), reader.GetValue(i));
                        }

                        table.Add(row);
                    }
                }
            }
            return table;
        }
        #endregion
    }

    public class ConsolidationPeriod
    {
        public int Id = 0;
        public TimeSpan Period;
        public TimeSpan Resolution;
    }
}
