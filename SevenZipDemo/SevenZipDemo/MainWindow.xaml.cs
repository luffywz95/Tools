using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;

using SevenZip;
using Path = System.IO.Path;

namespace SevenZipDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.FillComboBox();
        }

        private void FillComboBox()
        {
            combo_ZipChunkSize.ItemsSource = new[] { "5M", "10M", "100M", "500M", "1GB", "10GB" };

            var CompressionLevels_Except = new[] { CompressionLevel.None };
            var CompressionLevels = Enum.GetValues(typeof(CompressionLevel)).Cast<CompressionLevel>().Except(CompressionLevels_Except);
            combo_CompressionLevel.ItemsSource = new List<string>(CompressionLevels.Select(_ => _.ToString())).ToArray();
            combo_CompressionLevel.Text = nameof(CompressionLevel.Normal);

            var CompressionMethods = Enum.GetValues(typeof(CompressionMethod)).Cast<CompressionMethod>();
            combo_CompressionMethod.ItemsSource = new List<string>(CompressionMethods.Select(_ => _.ToString())).ToArray();
            combo_CompressionMethod.Text = nameof(CompressionMethod.Default);
        }

        private void btn_Process_Click(object sender, RoutedEventArgs e)
        {
            string sourceFilePath, dest7zFilePath;
            sourceFilePath = txt_SourcePath.Text;
            dest7zFilePath = txt_Destination.Text;

            if (string.IsNullOrEmpty(sourceFilePath) || string.IsNullOrEmpty(dest7zFilePath))
            {
                MessageBox.Show("Please provide both source and destination paths.");
                return;
            }

            if (!Directory.Exists(sourceFilePath) && !File.Exists(sourceFilePath))
            {
                MessageBox.Show("Source directory or file does not exist.");
                return;
            }

            if (Directory.Exists(dest7zFilePath))
            {
                if (File.Exists(sourceFilePath))
                {
                    dest7zFilePath = Path.Combine(dest7zFilePath, Path.GetFileName(sourceFilePath) + ".7z");
                }
                else
                {   // if (Directory.Exists(sourceFilePath))
                    dest7zFilePath = Path.Combine(dest7zFilePath, Path.GetFileName(sourceFilePath.TrimEnd(new[] { '\\', '/' })) + ".7z");
                }
            }
            else
            {
                if (Directory.Exists(Path.GetDirectoryName(dest7zFilePath)))
                {
                    dest7zFilePath = !dest7zFilePath.EndsWith(".7z") ? dest7zFilePath + ".7z" : dest7zFilePath;
                }
                else
                {
                    MessageBox.Show("Destination directory does not exist.");
                    return;
                }
            }

            bool includeSubfolderFiles = chk_IncludeSubfolderFiles.IsChecked == true;

            string[] files;
            if (Directory.Exists(sourceFilePath))
            {
                if (includeSubfolderFiles)
                {
                    files = Directory.GetFiles(sourceFilePath, "*", SearchOption.AllDirectories);
                }
                else
                {
                    files = Directory.GetFiles(sourceFilePath, "*", SearchOption.TopDirectoryOnly);
                }

                if (files.Length == 0)
                {
                    MessageBox.Show("No files found in the specified directory.");
                    return;
                }
            }
            else
            {
                 files = new[] { sourceFilePath };
            }

            // Set the library path for 7-Zip
            {
                string sevenZipLibraryPath = ConfigurationManager.AppSettings["SevenZipLibraryPath"];
                if (!string.IsNullOrEmpty(sevenZipLibraryPath))
                {
                    SevenZipBase.SetLibraryPath(sevenZipLibraryPath);
                }
                else
                {
                    if (File.Exists("./7z.dll"))
                    {
                        sevenZipLibraryPath = Path.Combine(Directory.GetCurrentDirectory(), "7z.dll");
                    }
                    else if (File.Exists("./7z64.dll"))
                    {
                        sevenZipLibraryPath = Path.Combine(Directory.GetCurrentDirectory(), "7z64.dll");
                    }
                    else if (File.Exists("./x64/7z.dll"))
                    {
                        sevenZipLibraryPath = Path.Combine(Directory.GetCurrentDirectory(), "x64", "7z.dll");
                    }
                    else
                    {
                        MessageBox.Show("7z.dll not found. Please set the library path in the app.config file.");
                        return;
                    }

                    SevenZipBase.SetLibraryPath(sevenZipLibraryPath);
                }
            }

            var sevenZipCompressor = new SevenZipCompressor();
            sevenZipCompressor.CompressionMode = CompressionMode.Create;
            sevenZipCompressor.ArchiveFormat = OutArchiveFormat.SevenZip;
            //sevenZipCompressor.CompressionLevel = CompressionLevel.Normal;
            //sevenZipCompressor.CompressionMethod = CompressionMethod.Default;

            string chunkSize = combo_ZipChunkSize.Text.Trim(new[] { '\r', '\n', ' ' });
            string compressionLevelText = combo_CompressionLevel.Text.Trim(new[] { '\r', '\n', ' ' });
            string compressionMethodText = combo_CompressionMethod.Text.Trim(new[] { '\r', '\n', ' ' });

            CompressionMethod compressionMethod; CompressionLevel compressionLevel;
            if (Enum.TryParse(compressionLevelText, out compressionLevel))
            {
                sevenZipCompressor.CompressionLevel = compressionLevel;
            }
            if (Enum.TryParse(compressionMethodText, out compressionMethod))
            {
                sevenZipCompressor.CompressionMethod = compressionMethod;
            }

            if (!string.IsNullOrEmpty(chunkSize))
            {
                var match_ChunkSize = Regex.Match(chunkSize, @"^(?<Size>\d+)(?<Unit>\w*).*?$");
                if (match_ChunkSize.Success)
                {
                    long size; long.TryParse(match_ChunkSize.Groups["Size"].Value, out size);
                    string unit = match_ChunkSize.Groups["Unit"].Value;

                    switch (unit.ToUpper())
                    {
                        case "KB":
                        case "K":
                            size *= 1024;
                            break;
                        case "MB":
                        case "M":
                            size *= 1024 * 1024;
                            break;
                        case "GB":
                        case "G":
                            size *= 1024 * 1024 * 1024;
                            break;
                        case "BYTE":
                        case "B":
                        case "":
                            break;
                        default:
                            size = -1;
                            break;
                    }
                    if (size != -1) sevenZipCompressor.VolumeSize = size;
                }
                else
                {
                    MessageBox.Show("Invalid chunk size format. Use '1KB', '2MB', etc.");
                }
            }

            HashSet<string> sourceFileFullNames = new HashSet<string>();
            string tmpDirName = null;
            if (chk_AppendTimestamp.IsChecked == true)
            {
                var zipFileMap = files.ToDictionary(file => file, file =>
                {
                    string newFile = file.Replace(sourceFilePath, string.Empty);
                    return Path.Combine(
                        Path.GetDirectoryName(newFile),
                        $"{Path.GetFileNameWithoutExtension(newFile)}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(newFile)}");
                });

                do
                {
                    if (File.Exists(sourceFilePath))
                    {
                        tmpDirName = Path.Combine(Path.GetDirectoryName(sourceFilePath), $"tmp-{Guid.NewGuid().ToString("N").Substring(0, 6)}");
                    }
                    else
                    {
                        tmpDirName = Path.Combine(sourceFilePath, $"tmp-{Guid.NewGuid().ToString("N").Substring(0, 6)}");
                    }
                } while (Directory.Exists(tmpDirName));
                Directory.CreateDirectory(tmpDirName);

                foreach (var kvp in zipFileMap)
                {
                    string newFileName = Path.Combine(tmpDirName, kvp.Value.TrimStart(new[] { '\\', '/' }));
                    if (!Directory.Exists(Path.GetDirectoryName(newFileName)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(newFileName));
                    }
                    File.Copy(kvp.Key, newFileName);
                    sourceFileFullNames.Add(newFileName);
                }
            }
            else
            {
                sourceFileFullNames = new HashSet<string>(files);
            }

            DateTime? procStartTime, procEndTime;
            procStartTime = procEndTime = null;

            var zipPassword = txt_ZipPassword.Text.Trim(new[] { '\r', '\n', ' ' });
            try
            {
                if (!string.IsNullOrEmpty(zipPassword))
                {
                    procStartTime = DateTime.Now;
                    sevenZipCompressor.CompressFilesEncrypted(dest7zFilePath, zipPassword, sourceFileFullNames.ToArray());
                    procEndTime = DateTime.Now;
                }
                else
                {
                    procStartTime = DateTime.Now;
                    sevenZipCompressor.CompressFiles(dest7zFilePath, sourceFileFullNames.ToArray());
                    procEndTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"\"Compression failed. Error occurs:\n{ex}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(tmpDirName) && Directory.Exists(tmpDirName))
                {
                    Directory.Delete(tmpDirName, true);
                }

                if (procStartTime.HasValue && procEndTime.HasValue)
                {
                    long sourceFileSize = 0;
                    long dest7zFileSize = 0;

                    if (Directory.Exists(sourceFilePath))
                    {
                        sourceFileSize = Directory.EnumerateFiles(sourceFilePath, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
                    }
                    else
                    {
                        foreach (var fileFullName in sourceFileFullNames)
                        {
                            sourceFileSize += new FileInfo(fileFullName).Length;
                        }
                    }

                    if (sevenZipCompressor.VolumeSize > 0)
                    {
                        if (Directory.Exists(Path.GetDirectoryName(dest7zFilePath)))
                        {
                            dest7zFileSize = Directory.EnumerateFiles(Path.GetDirectoryName(dest7zFilePath), $"{Path.GetFileName(dest7zFilePath)}.*", SearchOption.TopDirectoryOnly)
                                .Sum(file =>
                                {
                                    if (Int32.TryParse(Path.GetExtension(file).TrimStart(new[] { '.' }), out int _)) return new FileInfo(file).Length;
                                    return 0;
                                });
                        }
                    }
                    else
                    {
                        if (File.Exists(dest7zFilePath))
                        {
                            dest7zFileSize = new FileInfo(dest7zFilePath).Length;
                        }
                    }

                    MessageBox.Show($"Compression completed successfully.\n" +
                                    $"Compressed file: {dest7zFilePath}\n" +
                                    $"Total files: {sourceFileFullNames.Count}\n" +
                                    $"Total size: {GetFileSizeWithSizeSuffix(sourceFileSize)}\n" +
                                    $"Compressed file size: {GetFileSizeWithSizeSuffix(dest7zFileSize)} (Compression rate: {((double)dest7zFileSize / sourceFileSize) * 100:0.##}%)\n" +
                                    $"Total time: {(procEndTime.Value - procStartTime.Value).TotalSeconds.ToString("F2")} sec");
                }
            }
        }

        #region Other Methods

        private readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        private string GetFileSizeWithSizeSuffix(long sizeInByte, int decimalPlaces = 2)
        {
            if (decimalPlaces < 0) throw new ArgumentOutOfRangeException("decimalPlaces");
            if (sizeInByte < 0) return "-" + this.GetFileSizeWithSizeSuffix(-sizeInByte, decimalPlaces);
            if (sizeInByte == 0) return string.Format("{0:n" + decimalPlaces + "} bytes", 0);

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(sizeInByte, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)sizeInByte / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }
        #endregion
    }
}
