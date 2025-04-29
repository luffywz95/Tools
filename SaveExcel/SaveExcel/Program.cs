using System;
using System.Linq;
using System.IO;
using Aspose.Cells;
using System.Diagnostics;

namespace SaveExcel
{
    internal class Program
    {
        #region Declare
        private struct Message
        {
            public const string Success = "[SUCCESS] \"{_}\" was converted to Excel.";
            public const string Failure = "[FAILURE] \"{_}\"\r\n{_}.";
        }
        #endregion

        static void Main(string[] args)
        {
            Aspose.Cells.License license = new Aspose.Cells.License();
            license.SetLicense("Aspose.Total.lic");

            Debugger.Launch();

            string path = string.Empty;
            if (args.Length > 0)
            {
                path = args[0];
            }
            else return;

            if (path != string.Empty)
            {
                if (Directory.Exists(path))
                {
                    FileInfo[] excelFiles = new DirectoryInfo(path).GetFiles();

                    foreach (FileInfo fileInfo in excelFiles)
                    {
                        if (IsExcelFile(fileInfo.FullName, out string message))
                        {
                            SaveFileAsExcel(fileInfo.FullName);
                        }
                        else
                        {
                            WriteOneLogMsg(Message.Failure, fileInfo.FullName, message);
                        }
                    }
                }
                else
                {
                    FileInfo fileInfo = new FileInfo(path);
                    if (IsExcelFile(fileInfo.FullName, out string message))
                    {
                        SaveFileAsExcel(fileInfo.FullName);
                    }
                    else
                    {
                        WriteOneLogMsg(Message.Failure, fileInfo.FullName, message);
                    }
                }
            }

            Console.Read();
        }

        private static bool SaveFileAsExcel(string fileName)
        {
            try
            {
                using (Workbook wb = new Workbook(fileName))
                {
                    wb.Save(fileName);
                }
                WriteOneLogMsg(Message.Success, fileName);
                return true;
            }
            catch (Exception ex)
            {
                WriteOneLogMsg(Message.Failure, fileName, ex.Message);
                return false;
            }
        }

        private static bool IsExcelFile(string path, out string message)
        {
            message = string.Empty;

            byte[] xlsSignature = { 0xD0, 0xCF, 0x11, 0xE0 };
            byte[] xlsxSignature = { 0x50, 0x4B, 0x03, 0x04 };

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[4];
                    fs.Read(buffer, 0, 4);

                    bool isExcelFile = buffer.SequenceEqual(xlsSignature) || buffer.SequenceEqual(xlsxSignature);
                    if (!isExcelFile)
                    {
                        message = "It is not an Excel file.";
                    }

                    return isExcelFile;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static void WriteOneLogMsg(string message, params object[] vars)
        {
            string resultMsg;
            if (vars == null)
            {
                resultMsg = message.Replace("{_}", "");
            }
            else
            {
                string[] splits = message.Split(new[] { "{_}" }, StringSplitOptions.None);
                resultMsg = string.Empty;

                for (int i = 0; i < splits.Length; i++)
                {
                    if (i <= vars.Length - 1)
                    {
                        resultMsg += splits[i] + vars[i];
                    }
                    else
                    {
                        resultMsg += splits[i];
                    }
                }
            }
            Console.WriteLine(resultMsg);
        }
    }
}
