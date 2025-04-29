using EasyScreenEditor.Forms;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EasyScreenEditor.Utils
{
    public class ScreenHelper
    {
        #region Declare
        public ObservableCollection<ScreenInfo> ObservableScreenInfos { get; set; }

        private string currentMessage { get; set; } = string.Empty;
        private Dispatcher dispatcher { get; set; }
        #endregion

        #region Properties
        #endregion

        #region Classes
        public class ScreenInfo
        {
            public string FileName { get; set; }
            public string FileFullName { get; set; }
            public string ScreenID { get; set; }
            public string MainFormName { get; set; }

            public bool IsSelected { get; set; } = false;

            public ScreenInfo()
            {
                FileName = FileFullName = ScreenID = MainFormName = string.Empty;
            }
        }
        #endregion

        public ScreenHelper(Dispatcher dispatcher)
        {
            this.ObservableScreenInfos = new ObservableCollection<ScreenInfo>();
            this.dispatcher = dispatcher;
        }

        #region Public Methods
        public string GetCurrentMessage()
        {
            return this.currentMessage;
        }

        public bool FillScreenFileInfos(string path)
        {
            this.currentMessage = string.Empty;

            ObservableScreenInfos.Clear();

            if (string.IsNullOrEmpty(path))
            {
                this.currentMessage = "The source path not found.";
                return false;
            }

            FileInfo[] screenFileInfos = null;
            if (Directory.Exists(path))
            {
                screenFileInfos = new DirectoryInfo(path).GetFiles("*.s", SearchOption.TopDirectoryOnly);
            }
            else
            {
                if (File.Exists(path) && path.EndsWith(".s"))
                {
                    screenFileInfos = [new FileInfo(path)];
                }
                else
                {
                    this.currentMessage = "The source path not found.";
                    return false;
                }
            }

            if (screenFileInfos == null || screenFileInfos.Length == 0)
            {
                this.currentMessage = "No screen file found.";
                return false;
            }

            using (var progressWindow = new ProgressWindow())
            {
                double finishedTaskCount = 0;

                progressWindow.InitializeProgressBar(screenFileInfos.Length);
                progressWindow.progressTask = new Task(() =>
                {
                    foreach (var fileInfo in screenFileInfos)
                    {
                        ScreenInfo screenInfo; string errorMessage;
                        if (GetScreenFileInfo(fileInfo, out screenInfo, out errorMessage))
                        {
                            this.dispatcher.Invoke(() =>
                            {
                                this.ObservableScreenInfos.Add(screenInfo);
                            });
                        }
                        else
                        {
                            LogWriter.WriteLog(errorMessage, LogWriter.MessageType.Error);
                        }
                        finishedTaskCount++;

                        Task.Delay(50).Wait();
                        ((IProgress<double>)progressWindow.progress).Report(finishedTaskCount);
                    }
                });

                progressWindow.ShowDialog();
            }

            //this.ObservableScreenInfos = new ObservableCollection<ScreenInfo>(resultList);
            return this.ObservableScreenInfos.Count > 0;
        }

        public bool ExportModifiedScreenFiles(Dictionary<string, Dictionary<string, object>> controlPropertyReplacementMap, string exportPath = @"..\@exports")
        {
            this.currentMessage = string.Empty;

            var selectedScreenInfos = ObservableScreenInfos.Where(_ => _.IsSelected).ToList();
            if (selectedScreenInfos.Count == 0)
            {
                this.currentMessage = "No screen selected.";
                return false;
            }

            if (!Directory.Exists(exportPath))
            {
                Directory.CreateDirectory(exportPath);
            }

            using (var progressWindow = new ProgressWindow())
            {
                double finishedTaskCount = 0;

                progressWindow.InitializeProgressBar(selectedScreenInfos.Count);
                progressWindow.progressTask = new Task(() =>
                {
                    foreach (var screenInfo in selectedScreenInfos)
                    {
                        if (!File.Exists(screenInfo.FileFullName)) continue;

                        string content = string.Empty;
                        bool isModified = false;

                        using (var sr = new StreamReader(screenInfo.FileFullName))
                        {
                            content = sr.ReadToEnd();

                            foreach (var cpr in controlPropertyReplacementMap)
                            {
                                foreach (var pr in cpr.Value)
                                {
                                    var pattern = string.Format(@"({0}\s*=\s*)(?<{0}1>\d+)([^}}]*?[^(0-9a-zA-Z)]{1})|([^(0-9a-zA-Z)]{1}[^}}]*?{0}\s*=\s*)(?<{0}2>\d+)", pr.Key, cpr.Key);

                                    try
                                    {
                                        content = Regex.Replace(content, pattern, new MatchEvaluator(match =>
                                        {
                                            if (!string.IsNullOrEmpty(match.Groups[pr.Key + "1"].Value))
                                            {
                                                isModified = true;
                                                return match.Groups[1].Value + pr.Value + match.Groups[2].Value;
                                            }
                                            else if (!string.IsNullOrEmpty(match.Groups[pr.Key + "2"].Value))
                                            {
                                                isModified = true;
                                                return match.Groups[3].Value + pr.Value;
                                            }
                                            return match.Value;
                                        }));
                                    }
                                    catch (Exception ex)
                                    {
                                        LogWriter.WriteLog(string.Format("Replacement process went wrong on: {0}", screenInfo.FileName), LogWriter.MessageType.Error);
                                        LogWriter.WriteLog(string.Format("Error: {0}", ex), LogWriter.MessageType.Error);
                                    }
                                }
                            }
                        }

                        if (isModified)
                        {
                            if (!string.IsNullOrEmpty(content))
                            {
                                try
                                {
                                    using (var sw = new StreamWriter(Path.Combine(exportPath, screenInfo.FileName)))
                                    {
                                        sw.Write(content);
                                        sw.Flush();

                                        this.CopyScreenTxtFile(screenInfo, exportPath);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogWriter.WriteLog(string.Format("Failed to create screen file: {0}", screenInfo.FileName), LogWriter.MessageType.Error);
                                    LogWriter.WriteLog(string.Format("Error: {0}", ex), LogWriter.MessageType.Error);
                                }
                            }
                        }
                        finishedTaskCount++;

                        Task.Delay(50).Wait();
                        ((IProgress<double>)progressWindow.progress).Report(finishedTaskCount);
                    }
                });

                progressWindow.ShowDialog();
            }

            return true;
        }
        #endregion

        #region Local Methods
        private bool GetScreenFileInfo(FileInfo fileInfo, out ScreenInfo resultScreenInfo, out string errorMessage)
        {
            errorMessage = string.Empty;

            resultScreenInfo = new ScreenInfo();
            try
            {
                resultScreenInfo.FileName = fileInfo.Name;
                resultScreenInfo.FileFullName = fileInfo.FullName;
                resultScreenInfo.ScreenID = Path.GetFileNameWithoutExtension(fileInfo.Name);

                using (StreamReader sr = new StreamReader(fileInfo.FullName))
                {
                    var content = sr.ReadToEnd();
                    var match_MainFormName = Regex.Match(content, @"MainWindow.*?[^}}]*(?:Caption\s*\=\s*\""(?<MainFormName>.*?)\"")");
                    if (match_MainFormName != null && match_MainFormName.Success)
                    {
                        resultScreenInfo.MainFormName = match_MainFormName.Groups["MainFormName"].Value;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = string.Format("Failed to load file: {0}. Error: {1}", fileInfo.FullName, ex);
                return false;
            }

            return true;
        }

        private bool CopyScreenTxtFile(ScreenInfo screenInfo, string exportDir)
        {
            var txtFilePath = Path.ChangeExtension(screenInfo.FileFullName, ".txt");
            if (!string.IsNullOrEmpty(txtFilePath))
            {
                if (File.Exists(txtFilePath))
                {
                    try
                    {
                        File.Copy(txtFilePath, Path.Combine(exportDir, Path.GetFileName(txtFilePath)), true);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogWriter.WriteLog(string.Format("Failed to copy screen text file from: {0}", txtFilePath), LogWriter.MessageType.Error);
                        LogWriter.WriteLog(string.Format("Error: {0}", ex), LogWriter.MessageType.Error);
                    }
                }
            }
            return false;
        }
        #endregion
    }
}
