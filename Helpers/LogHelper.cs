//using MIPLabelServiceTool.Models.AVIS_TRON;
//using Newtonsoft.Json;
//using System;
//using System.Data;
//using System.Data.SqlClient;
//using System.IO;
//using System.Text;

//namespace MIPLabelServiceTool.Helpers
//{
//    public static class LogHelper
//    {
//        private static readonly string eventLogPath;
//        private static readonly string errorLogPath;
//        private static readonly string moduleLogPath;

//        static LogHelper()
//        {
//            eventLogPath = ConfigHelper.GetValue("Paths:EventLog");
//            errorLogPath = ConfigHelper.GetValue("Paths:ErrorLog");
//            moduleLogPath = ConfigHelper.GetValue("Paths:ModuleLog");
//        }

//        public static string GetLogPath(string pathType)
//        {
//            string? LogPath = string.Empty;

//            switch (pathType)
//            {
//                case "event":
//                    LogPath = Directory.GetParent(Directory.GetCurrentDirectory()).ToString() + eventLogPath;
//                    break;
//                case "error":
//                    LogPath = Directory.GetParent(Directory.GetCurrentDirectory()).ToString() + errorLogPath;
//                    break;
//                case "module":
//                    LogPath = Directory.GetParent(Directory.GetCurrentDirectory()).ToString() + moduleLogPath;
//                    break;
//            }

//            if (!Directory.Exists(LogPath))
//            {
//                Directory.CreateDirectory(LogPath);
//            }

//            return LogPath;
//        }

//        public static void EventLogAppend(string logType, string eventMessage, string? eventTrace)
//        {

//            string EventFullPath = GetLogPath("event");

//            using (StreamWriter sw = new StreamWriter(EventFullPath + DateTime.Now.ToString("yyyyMMdd") + "_event.log", true, Encoding.Default))
//            {
//                sw.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + logType + "\r\n" + eventMessage + "\r\n" + eventTrace);
//                sw.Flush();
//                sw.Close();
//                sw.Dispose();
//            }
//        }

//        public static void ErrorLogAppend(string logType, string errorMessage, string? errorTrace)
//        {
//            string ErrorFullPath = GetLogPath("error");

//            using (StreamWriter sw = new StreamWriter(ErrorFullPath + DateTime.Now.ToString("yyyyMMdd") + "_error.log", true, Encoding.Default))
//            {
//                sw.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + logType + "\r\n" + errorMessage + "\r\n" + errorTrace);
//                sw.Flush();
//                sw.Close();
//                sw.Dispose();
//            }
//        }

//        public static void ModuleLogAppend(string moduleName, string logPhase, object message, string? trace = null)
//        {
//            string basePath = GetLogPath("module");
//            string fileName = $"{DateTime.Now:yyyyMMdd}_{moduleName}.log";
//            string fullPath = Path.Combine(basePath, fileName);

//            string serializedMessage = message is string str
//                ? str
//                : JsonConvert.SerializeObject(message, Formatting.Indented);

//            lock (GetLockObject(fullPath))
//            {
//                using (StreamWriter sw = new StreamWriter(fullPath, true, Encoding.UTF8))
//                {
//                    sw.WriteLine($"{DateTime.Now:HH:mm:ss} [{logPhase}]");
//                    sw.WriteLine(serializedMessage);
//                    if (!string.IsNullOrEmpty(trace))
//                        sw.WriteLine(trace);
//                    sw.WriteLine(); // 공백 줄
//                }
//            }
//        }

//        private static readonly Dictionary<string, object> _logLocks = new Dictionary<string, object>();
//        private static readonly object _logLocksGlobal = new object();

//        private static object GetLockObject(string filePath)
//        {
//            if (!_logLocks.ContainsKey(filePath))
//            {
//                lock (_logLocksGlobal)
//                {
//                    if (!_logLocks.ContainsKey(filePath))
//                    {
//                        _logLocks[filePath] = new object();
//                    }
//                }
//            }
//            return _logLocks[filePath];
//        }
//    }

//    public class LogManagerHelper
//    {
//        protected DataSet ds_before_log = new DataSet();
//        protected DataSet ds_after_log = new DataSet();

//        protected Dictionary<string, List<object>> list_before_log = new();
//        protected Dictionary<string, List<object>> list_after_log = new();

//        public LogManagerHelper()
//        {
//            ds_before_log.DataSetName = "Before";
//            ds_after_log.DataSetName = "After";
//        }

//        public void AddLogTable(string log_type, string log_name, DataTable dt)
//        {
//            dt.TableName = log_name;

//            if (log_type == "Before")
//                ds_before_log.Tables.Add(dt);
//            else if (log_type == "After")
//                ds_after_log.Tables.Add(dt);
//        }

//        public void AddLogList<T>(string logType, string logName, List<T> dataList)
//        {
//            var boxedList = dataList.Cast<object>().ToList();

//            if (logType == "Before")
//            {
//                list_before_log[logName] = boxedList;
//            }
//            else if (logType == "After")
//            {
//                list_after_log[logName] = boxedList;
//            }
//        }

//        public string DatatableToJSON(DataTable source)
//        {
//            var lst = source.AsEnumerable()
//                .Select(r => r.Table.Columns.Cast<DataColumn>()
//                        .Select(c => new KeyValuePair<string, object>(c.ColumnName, r[c.Ordinal])
//                       ).ToDictionary(z => z.Key, z => z.Value)
//                ).ToList();

//            string result = JsonConvert.SerializeObject(lst);
//            return JsonConvert.SerializeObject(lst);
//        }
//    }
//}
