using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyScreenEditor.Utils
{
    public class LogWriter
    {
        public enum MessageType
        {
            Information = 0,
            Error,
            Warning,
        }

        private static readonly Dictionary<MessageType, string> MESSAGE_TYPE_TEXT_MAP = new Dictionary<MessageType, string>()
        {
            { MessageType.Information, "INFO" },
            { MessageType.Error, "ERROR" },
            { MessageType.Warning, "WARNING" },
        };

        public static void WriteLog(string message, MessageType msgType)
        {
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogFiles", $"Log@{DateTime.Today}.txt");

            try
            {
                string resultMessagee = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - [{MESSAGE_TYPE_TEXT_MAP[msgType]}] {message}";
                File.AppendAllText(logFilePath, message + Environment.NewLine);
            }
            catch (DirectoryNotFoundException ex)
            {
                if (!Directory.Exists(Path.GetDirectoryName(logFilePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));
                    WriteLog(message, msgType);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
