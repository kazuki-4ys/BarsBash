using BarsTool;
using BfstpTool;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace BarsBash
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string defaultWindowTitle = "BarsBash v0.1 / BGM.bars patcher for MK8DX";
        private const string waitingWindowTitle = "Please wait...";
        private bool askUserForOverwrite = false;
        private Bars targetBars = null;
        private string targetBarsPath = "";
        private byte[] targetBfstpBytes = null;
        private List<string> tracksString = null;

        public MainWindow()
        {
            InitializeComponent();
            this.Title = defaultWindowTitle;
        }
        private string ReplaceExtension(string src, string origExt, string replaceExt)
        {
            int origExtLength = origExt.Length;
            if (origExtLength > src.Length) return src;
            if (src.Substring(src.Length - origExtLength, origExtLength) != origExt) return src;
            return src.Substring(0, src.Length - origExtLength) + replaceExt;
        }
        private void UpdateBfstmList()
        {
            tracksString = new List<string>();
            for (uint i = 0; i < targetBars.Audio.Length; i++)
            {
                if (targetBars.Audio[i].fileName.Substring(targetBars.Audio[i].fileName.Length - 6, 6) != ".bfstp") continue;
                tracksString.Add(ReplaceExtension(targetBars.Audio[i].fileName, ".bfstp", ".bfstm"));
            }
            tracksString.Sort();
            trackSelect.ItemsSource = tracksString;
            trackSelect.SelectedIndex = 0;
        }
        private string OpenFile(string filter)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = filter;
            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            return "";
        }
        private void BarsLoadButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] bytes;
            string loadBarsPath = OpenFile("BARS file (*.bars)|*.bars");
            if (loadBarsPath.Length == 0) return;
            try
            {
                bytes = File.ReadAllBytes(loadBarsPath);
            }
            catch
            {
                MessageBox.Show("Cannot read " + loadBarsPath);
                return;
            }
            Bars loadedBars = new Bars(bytes);
            if (!loadedBars.valid || !loadedBars.ContainBfstp())
            {
                MessageBox.Show("Invalid BARS file");
                return;
            }
            targetBars = loadedBars;
            BarsPathShow.Text = loadBarsPath;
            targetBarsPath = loadBarsPath;
            UpdateBfstmList();
            if (targetBfstpBytes != null) PatchButton.IsEnabled = true;
        }
        private void TrackSelect_SelectionChanged(object sender, RoutedEventArgs e)
        {
            return;
        }

        private void BfstmLoadButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] bytes;
            string loadBfstmPath = OpenFile("BFSTM file (*.bfstm)|*.bfstm");
            if (loadBfstmPath.Length == 0) return;
            try
            {
                bytes = File.ReadAllBytes(loadBfstmPath);
            }
            catch
            {
                MessageBox.Show("Cannot read " + loadBfstmPath);
                return;
            }
            Bfstp loadedBfstm = new Bfstp(bytes);
            if (!loadedBfstm.valid)
            {
                MessageBox.Show("Invalid BFSTM file");
                return;
            }
            byte[] targetBfstp = loadedBfstm.SaveForMK8DX();
            if(targetBfstp == null)
            {
                MessageBox.Show("BFSTM is too short");
                return;
            }
            BfstmPathShow.Text = loadBfstmPath;
            targetBfstpBytes = targetBfstp;
            if(targetBars != null)PatchButton.IsEnabled = true;
        }

        private void PatchButton_Click(object sender, RoutedEventArgs e)
        {
            if(askUserForOverwrite == false)
            {
                if (MessageBox.Show("Are you sure you want to overwrite BARS file?", "", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No) return;
            }
            this.Title = waitingWindowTitle;
            DisableAllButton();
            askUserForOverwrite = true;
            SimpleFile patchTarget = targetBars.FindAudioFileByFileName(ReplaceExtension(trackSelect.Text, ".bfstm", ".bfstp"));
            patchTarget.data = targetBfstpBytes;
            byte[] barsDest = targetBars.Save();
            try
            {
                File.WriteAllBytes(targetBarsPath, barsDest);
            }
            catch
            {
                MessageBox.Show("Cannot write to " + targetBarsPath);
                return;
            }
            this.Title = defaultWindowTitle;
            EnableAllButton();
            MessageBox.Show(targetBarsPath + " has been patched successfully.");
        }
        private void DisableAllButton()
        {
            BarsLoadButton.IsEnabled = false;
            BfstmLoadButton.IsEnabled = false;
            trackSelect.IsEnabled = false;
            PatchButton.IsEnabled = false;
        }
        private void EnableAllButton()
        {
            BarsLoadButton.IsEnabled = true;
            BfstmLoadButton.IsEnabled = true;
            trackSelect.IsEnabled = true;
            PatchButton.IsEnabled = true;
        }
    }
}
