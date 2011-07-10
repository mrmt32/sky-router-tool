using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlServerCe;

namespace pHMb.Router
{
    public interface ILogger
    {
        string Name { get; }
        void Log(Interfaces.IRouterInterface routerCommand, SqlCeConnection databaseConnection);
        void Consolidate(DateTime limit, TimeSpan resolution, SqlCeConnection databaseConnection);
        SqlCeDataReader Retrieve(DateTime startDate, DateTime endDate, SqlCeConnection databaseConnection);
        SqlCeDataReader GetAggregateValues(DateTime startDate, DateTime endDate, SqlCeConnection databaseConnection);
    }
}
