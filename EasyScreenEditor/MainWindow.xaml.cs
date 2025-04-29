using System.IO;
using System.Text.Json;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

using EasyScreenEditor.Utils;
using EasyScreenEditor.Forms;

namespace EasyScreenEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Declare
        protected ScreenHelper screenHelper;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.InitClassItems();
        }

        #region Initialization
        private void InitClassItems()
        {
            this.screenHelper = new ScreenHelper(this.Dispatcher);
            dataGrid_ScreenFileList.ItemsSource = this.screenHelper.ObservableScreenInfos;
        }
        #endregion

        private void btn_Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (!this.ConfirmAfterClick(confirmMessage: "Confirm refresh?")) return;

            if (!this.screenHelper.FillScreenFileInfos(txt_SourcePath.Text))
            {
                string errorMessage = this.screenHelper.GetCurrentMessage();

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    MessageBox.Show(errorMessage, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show("Something went wrong, please check the log.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void btn_SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var screenInfo in this.screenHelper.ObservableScreenInfos)
            {
                screenInfo.IsSelected = true;
            }
            dataGrid_ScreenFileList.Items.Refresh();
        }

        private void btn_DisselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var screenInfo in this.screenHelper.ObservableScreenInfos)
            {
                screenInfo.IsSelected = false;
            }
            dataGrid_ScreenFileList.Items.Refresh();
        }

        private void btn_Process_Click(object sender, RoutedEventArgs e)
        {
            if (!this.ConfirmAfterClick(confirmMessage: "Confirm process?")) return;

            Dictionary<string, Dictionary<string, object>> controlPropertyReplacementMap;
            try
            {
                if (File.Exists(txt_PropertyValueReplacement.Text))
                {
                    using (StreamReader sr = new StreamReader(txt_PropertyValueReplacement.Text))
                    {
                        controlPropertyReplacementMap = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(sr.ReadToEnd());
                    }
                }
                else
                {
                    controlPropertyReplacementMap = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(string.Format("{{{0}}}", txt_PropertyValueReplacement.Text));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Invalid replacement data.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

                LogWriter.WriteLog($"Invalid replacement data: {txt_PropertyValueReplacement.Text}", LogWriter.MessageType.Error);
                LogWriter.WriteLog($"Error: {ex}", LogWriter.MessageType.Error);
                return;
            }

            if (controlPropertyReplacementMap == null || controlPropertyReplacementMap.Count == 0)
            {
                MessageBox.Show("No replacement data.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string exportFilePath = null;
            if (!string.IsNullOrEmpty(txt_ExportPath.Text))
            {
                try
                {
                    if (Directory.Exists(txt_ExportPath.Text))
                    {
                        exportFilePath = txt_ExportPath.Text;
                    }
                }
                catch (Exception ex) 
                {

                }
            }
            if (string.IsNullOrEmpty(exportFilePath)) exportFilePath = @"..\@exports";

            if (this.screenHelper.ExportModifiedScreenFiles(controlPropertyReplacementMap, exportPath: exportFilePath))
            {
                MessageBox.Show("Replacement done!", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                string errorMessage = this.screenHelper.GetCurrentMessage();

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    MessageBox.Show(errorMessage, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show("Something went wrong, please check the log.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private bool ConfirmAfterClick(string confirmMessage = "Comfirm to proceed?")
        {
            return MessageBox.Show(confirmMessage, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
    }
}