using MySql.Data.MySqlClient;
using NetWorkLibrary.Utility;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace NetWorkLibrary.Database
{
    public sealed class MysqlManager : IDisposable
    {
        private static readonly string[] emptyStrs = Array.Empty<string>();

        bool isQuerying = false;
        MySqlConnection connection;
        MySqlConnection asyncConnection;
        Timer timer;
        public void Init(string host, string user, string password, string database, int port)
        {
            string connStr = $"data source={host};database={database};user id={user};password={password};pooling=true;charset=utf8;SslMode=None;";
            connection = new MySqlConnection(connStr);
            asyncConnection = new MySqlConnection(connStr);

            try
            {
                connection.Open();
                asyncConnection.Open();
                LogManager.Instance.Log(LogType.Message, "Mysql Successfully connected to {0}:{1}:{2}", host, port, database);
                timer = new Timer(OnTimer, null, 0, 600 * 1000);
            }
            catch (MySqlException e)
            {
                LogManager.Instance.Log(LogType.Error, "{0}", e.Message);
            }
        }

        private void OnTimer(object state)
        {
            connection.Ping();
            asyncConnection.Ping();
        }

        public bool Execute(string sql, string[] paramNames = null, params object[] paramValues)
        {
            MySqlCommand cmd = new MySqlCommand(sql, connection);
            if (paramNames != null)
            {
                if (paramNames.Length != paramValues.Length)
                {
                    LogManager.Instance.Log(LogType.Warning, "Execute {0} Error: param is not equal.", sql);
                    return false;
                }

                for (int i = 0; i < paramNames.Length; i++)
                {
                    cmd.Parameters.AddWithValue(paramNames[i], paramValues[i]);
                }
            }

            try
            {
                lock (connection)
                {
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (MySqlException e)
            {
                LogManager.Instance.Log(LogType.Error, "Execute {0} MysqlError: {1}.", sql, e.Message);
            }
            catch (Exception e)
            {
                LogManager.Instance.Log(LogType.Error, "Execute {0} Error: {1}.", sql, e.Message);
            }

            return false;
        }

        public MysqlResult Query(string sql, string[] paramNames = null, params object[] paramValues)
        {
            MysqlResult result = new MysqlResult();
            MySqlCommand cmd = new MySqlCommand(sql, connection);

            if (paramNames != null)
            {
                if (paramNames.Length != paramValues.Length)
                {
                    LogManager.Instance.Log(LogType.Warning, "Query {0} Error: param is not equal.", sql);
                    return result;
                }

                for (int i = 0; i < paramNames.Length; i++)
                {
                    cmd.Parameters.AddWithValue(paramNames[i], paramValues[i]);
                }
            }

            try
            {
                lock (connection)
                {
                    var reader = cmd.ExecuteReader(CommandBehavior.Default);
                    result.Load(reader);
                    reader.Close();
                }
                result.RowCount = result.Rows.Count;
                result.ColumnCount = result.Columns.Count;
            }
            catch (MySqlException e)
            {
                LogManager.Instance.Log(LogType.Error, "Query {0} MysqlError: {1}.", sql, e.Message);
            }
            catch (Exception e)
            {
                LogManager.Instance.Log(LogType.Error, "Query {0} Error: {1}.", sql, e.Message);
            }

            return result;
        }

        public async void QueryAysnc(Action<MysqlResult> CallBack, string sql, string[] paramNames = null, params object[] paramValues)
        {
            if (paramNames != null)
            {
                if (paramNames.Length != paramValues.Length)
                {
                    LogManager.Instance.Log(LogType.Warning, "QueryAysnc {0} Error: param is not equal.", sql);
                    return;
                }
            }

            while (isQuerying)
            {
                await Task.Delay(10);
            }
            isQuerying = true;

            MysqlResult result = new MysqlResult();
            MySqlCommand cmd = new MySqlCommand(sql, connection);
            if (paramNames != null)
            {
                for (int i = 0; i < paramNames.Length; i++)
                {
                    cmd.Parameters.AddWithValue(paramNames[i], paramValues[i]);
                }
            }

            try
            {
                var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default);
                result.Load(reader);
                reader.Close();
                isQuerying = false;
                result.RowCount = result.Rows.Count;
                result.ColumnCount = result.Columns.Count;
                CallBack(result);
            }
            catch (MySqlException e)
            {
                LogManager.Instance.Log(LogType.Error, "QueryAysnc {0} MysqlError: {1}.", sql, e.Message);
            }
            catch (Exception e)
            {
                LogManager.Instance.Log(LogType.Error, "QueryAysnc {0} Error: {1}.", sql, e.Message);
            }
        }

        #region 存储过程查询

        public bool PExecute(string procedureName, string[] paramNames = null, params object[] paramValues)
        {
            MySqlCommand cmd = new MySqlCommand(procedureName, connection);
            if (paramNames != null)
            {
                if (paramNames.Length != paramValues.Length)
                {
                    LogManager.Instance.Log(LogType.Warning, "PExecute {0} Error: param is not equal.", procedureName);
                    return false;
                }

                cmd.CommandType = CommandType.StoredProcedure;
                for (int i = 0; i < paramNames.Length; i++)
                {
                    cmd.Parameters.AddWithValue(paramNames[i], paramValues[i]);
                }
            }

            try
            {
                lock (connection)
                {
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (MySqlException e)
            {
                LogManager.Instance.Log(LogType.Error, "PExecute {0} MysqlError: {1}.", procedureName, e.Message);
            }
            catch (Exception e)
            {
                LogManager.Instance.Log(LogType.Error, "PExecute {0} Error: {1}.", procedureName, e.Message);
            }

            return false;
        }

        public MysqlResult PQuery(string procedureName, string[] paramNames = null, params object[] paramValues)
        {
            MysqlResult result = new MysqlResult();
            MySqlCommand cmd = new MySqlCommand(procedureName, connection);
            if (paramNames != null)
            {
                if (paramNames.Length != paramValues.Length)
                {
                    LogManager.Instance.Log(LogType.Warning, "PQuery {0} Error: param is not equal.", procedureName);
                    return result;
                }

                cmd.CommandType = CommandType.StoredProcedure;
                for (int i = 0; i < paramNames.Length; i++)
                {
                    cmd.Parameters.AddWithValue(paramNames[i], paramValues[i]);
                }
            }

            try
            {
                lock (connection)
                {
                    var reader = cmd.ExecuteReader(CommandBehavior.Default);
                    result.Load(reader);
                    reader.Close();
                }
                result.RowCount = result.Rows.Count;
                result.ColumnCount = result.Columns.Count;
            }
            catch (MySqlException e)
            {
                LogManager.Instance.Log(LogType.Error, "PQuery {0} MysqlError: {1}.", procedureName, e.Message);
            }
            catch (Exception e)
            {
                LogManager.Instance.Log(LogType.Error, "PQuery {0} Error: {1}.", procedureName, e.Message);
            }

            return result;
        }

        public async void PQueryAysnc(Action<MysqlResult> CallBack, string procedureName, string[] paramNames = null, params object[] paramValues)
        {
            if (paramNames != null)
            {
                if (paramNames.Length != paramValues.Length)
                {
                    LogManager.Instance.Log(LogType.Warning, "PQueryAysnc {0} Error: param is not equal.", procedureName);
                    return;
                }
            }

            while (isQuerying)
            {
                await Task.Delay(10);
            }
            isQuerying = true;

            MysqlResult result = new MysqlResult();
            MySqlCommand cmd = new MySqlCommand(procedureName, connection);
            if (paramNames != null)
            {
                cmd.CommandType = CommandType.StoredProcedure;
                for (int i = 0; i < paramNames.Length; i++)
                {
                    cmd.Parameters.AddWithValue(paramNames[i], paramValues[i]);
                }
            }
            

            try
            {
                var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default);
                result.Load(reader);
                reader.Close();
                isQuerying = false;
                result.RowCount = result.Rows.Count;
                result.ColumnCount = result.Columns.Count;
                CallBack(result);
            }
            catch (MySqlException e)
            {
                LogManager.Instance.Log(LogType.Error, "PQueryAysnc {0} MysqlError: {1}.", procedureName, e.Message);
            }
            catch (Exception e)
            {
                LogManager.Instance.Log(LogType.Error, "PQueryAysnc {0} Error: {1}.", procedureName, e.Message);
            }
        }
        #endregion //存储过程查询

        public void Dispose()
        {
            isQuerying = false;
            timer.Dispose();
            connection.Dispose();
            asyncConnection.Dispose();
        }
    }
}