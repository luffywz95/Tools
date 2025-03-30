using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using System.Configuration;

namespace DDS2RPV
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Declare
        public ObservableCollection<DDS2RPVHelper.TableInfo> ObservableTableFileInfos { get; set; }
        #endregion

        #region Properties
        protected DDS2RPVHelper helper;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            this.InitDataClassItems();
        }

        private void InitDataClassItems()
        {
            this.ObservableTableFileInfos = new ObservableCollection<DDS2RPVHelper.TableInfo>();
            dataGrid_FileList.ItemsSource = this.ObservableTableFileInfos;

            this.helper = new DDS2RPVHelper(ConfigurationManager.AppSettings["TranslationResourceFile"] ?? string.Empty);
        }

        #region Event Functions
        private void btn_Refresh_Click(object sender, RoutedEventArgs e)
        {
            this.ObservableTableFileInfos.Clear();

            string path = tbox_DDSSourcePath.Text;
            if (string.IsNullOrEmpty(path) || (!Directory.Exists(path) && !File.Exists(path)))
            {
                MessageBox.Show("DDS source path not found.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FileInfo[] ddsFileInfos = null;
            if (Directory.Exists(path))
            {
                ddsFileInfos = new DirectoryInfo(path).GetFiles("*.dds");
            }
            else
            {
                ddsFileInfos = new FileInfo[] { new FileInfo(path) };
            }

            if (ddsFileInfos == null || ddsFileInfos.Length == 0)
            {
                MessageBox.Show("No DDS file found.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DDS2RPVHelper.TableInfo tableInfo;
            foreach (var ddsFileInfo in ddsFileInfos)
            {
                if (this.helper.GetDDSFileTableInfo(ddsFileInfo, out tableInfo))
                {
                    ObservableTableFileInfos.Add(tableInfo);
                }
            }
        }

        private void btn_GenerateFile_Click(object sender, RoutedEventArgs e)
        {
            //var selectedItems = dataGrid_FileList.Items.SourceCollection.Cast<DDS2RPVHelper.TableInfo>().Where(fi => fi.IsSelect);
            var selectedItems = this.ObservableTableFileInfos.Where(fi => fi.IsSelect);

            var selectedProjectCode = (from rad in panel_ProjectCodes.Children.OfType<RadioButton>()
                                           where rad.IsChecked == true
                                           select rad.Content.ToString()).ElementAt(0);

            if (selectedItems.Count() > 0)
            {
                if (!this.helper.Initialize(selectedProjectCode))
                {
                    MessageBox.Show(this.helper.CurrentMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (this.helper.ExportRpvFiles(selectedItems, @"..\@exports"))
                {
                    MessageBox.Show("Successfully generated RPV files.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(this.helper.CurrentMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid)
            {
                var dataGridObj = ((DataGrid)sender);

                switch (dataGridObj.Name)
                {
                    case nameof(dataGrid_FileList):
                        var selectedItem = this.ObservableTableFileInfos[((DataGrid)sender).SelectedIndex];
                        tbox_ApproxRecordSize.Text = "" + this.helper.GetApproximateRecordSize(selectedItem);
                        break;
                }

                //CollectionViewSource.GetDefaultView(dataGridObj.ItemsSource).Refresh();
                dataGridObj.Items.Refresh();
            }
        }
        #endregion
    }
}