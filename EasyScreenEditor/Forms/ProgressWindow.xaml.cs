using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EasyScreenEditor.Forms
{
    /// <summary>
    /// Interaction logic for ProgressWindow.xaml
    /// </summary>
    public partial class ProgressWindow : Window, IDisposable
    {
        #region Declare
        public Task progressTask { get; set; }
        public Progress<double> progress { get; set; }
        #endregion

        public ProgressWindow()
        {
            InitializeComponent();

            if (this.Parent == null)
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            else
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
        }

        ~ProgressWindow()
        {
            this.Dispose(false);
        }

        public void InitializeProgressBar(double max = 100)
        {
            CommonProgressBar.Maximum = max;
        }

        protected override void OnActivated(EventArgs e)
        {
            if (this.progressTask != null && this.progressTask.Status == TaskStatus.Created)
            {
                CommonProgressBar.Value = 0;
                ProgressIndicator.Text = "0 %";

                this.progress = new Progress<double>(value =>
                {
                    double progressInPercent = value / CommonProgressBar.Maximum * 100;

                    CommonProgressBar.Value = value;
                    ProgressIndicator.Text = $"Complete: {value} / {CommonProgressBar.Maximum} ({progressInPercent:#0.00} %)";
                });

                this.progressTask.Start();
                this.progressTask.ContinueWith((task) =>
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        this.Close();
                    });
                });
            }
            base.OnActivated(e);
        }

        public void Dispose()
        {
            this.Dispose(_contentLoaded);
            try
            {
                GC.SuppressFinalize(this);
            } catch { }
        }

        private void Dispose(bool disposing)
        {
            if (!disposing)
            {
                this.progressTask = null;
                this.progress = null;
            }

            if (!this.IsActive)
            {
                this.Close();
            }
        }
    }
}
