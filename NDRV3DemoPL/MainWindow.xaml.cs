using Paks.Zip;
using CriFsV2Lib;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace NDRV3DemoPL
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        string[] steam = { @"SOFTWARE\VALVE\", @"SOFTWARE\Wow6432Node\Valve\" };
        string[] cpkFiles = { "partition_data_win_demo_us.cpk", "partition_resident_win_demo.cpk" };
        string gamepath = @"\steamapps\common\Danganronpa V3 Killing Harmony Demo";
        string exefilename = @"\Dangan3Win.exe";
        string path;
        bool gameexist = false;
        bool allowClose = true;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                SearchSteam(steam[0]);
            }
            catch { }
            if (!gameexist)
            {
                try
                {
                    SearchSteam(steam[1]);
                }
                catch { }
            }
            if (!gameexist)
            {
                textBlockInfo.Text = "Nie udało się automatycznie znaleźć folderu z grą :(";
                textBlockInfo.Foreground = Brushes.Red;
            }
            else
            {
                textBoxBrowse.Text = path;
                textBlockInfo.Text = "Automatycznie znaleziono folder z grą";
                textBlockInfo.Foreground = Brushes.Green;
            }
            textBlockInfo.Visibility = Visibility.Visible;
        }

        private void SearchSteam(string keypath)
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(keypath);
            foreach (string subKeyString in key.GetSubKeyNames())
            {
                using (RegistryKey subKey = key.OpenSubKey(subKeyString))
                {
                    try
                    {
                        path = subKey.GetValue("InstallPath").ToString();
                        path += gamepath;
                        if (File.Exists(path + exefilename))
                        {
                            gameexist = true;
                            break;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnRead_Click(object sender, RoutedEventArgs e)
        {
            string temp = Path.GetTempPath() + "NDRV3DemoPL.PRZECZYTAJ MNIE.tmp";
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("NDRV3DemoPL.Resources.PRZECZYTAJ MNIE.txt"))
            {
                using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write))
                {
                    resource.CopyTo(file);
                }
            }
            Process.Start("notepad.exe", temp);
        }

        private void btnInstall_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(textBoxBrowse.Text))
            {
                MessageBox.Show("Nie wybrano żadnego folderu.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string directory = textBoxBrowse.Text + @"\data\win_demo";

            foreach (var cpkFile in cpkFiles)
            {
                if (!File.Exists(directory + "/" + cpkFile))
                {
                    MessageBox.Show("Nie znaleziono pliku " + cpkFile + "\nSprawdź poprawność ścieżki oraz spójność plików gry!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }


            btnBrowse.Visibility = Visibility.Hidden;
            btnInstall.Visibility = Visibility.Hidden;
            btnRead.Visibility = Visibility.Hidden;

            allowClose = false;
            textBlockInfo.Visibility = Visibility.Hidden;
            textBoxBrowse.Visibility = Visibility.Hidden;
            textBlock1.Text = "Rozpoczynanie instalacji...";

            progressBar.Visibility = Visibility.Visible;


            var thread = new Thread(new ThreadStart(() => { Install(directory); }));
            thread.Start();
        }

        private void Install(string directory)
        {
            try
            {
                foreach (var cpkFile in cpkFiles)
                {
                    CpkExtractor(directory, cpkFile);
                }

                using (var zip = new ZipExtractor(Assembly.GetExecutingAssembly().GetManifestResourceStream("NDRV3DemoPL.Resources.data.zip")))
                {
                    zip.OnExtractStatus += Zip_OnExtractStatus;
                    zip.Extract();
                }

                File.Delete(directory + "/" + cpkFiles[1]);
                File.Delete(directory + "/" + cpkFiles[0]);

                Dispatcher.BeginInvoke(new Action(delegate ()
                {
                    allowClose = true;
                    textBlock1.Text = "Instalacja zakończona";
                    btnCancel.Content = "Zakończ";
                }));
            }
            catch (Exception ex)
            {
                if (ex is UnauthorizedAccessException)
                {
                    MessageBox.Show(ex.Message + "\nUruchom instalator jako administrator.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show(ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                Dispatcher.BeginInvoke(new Action(delegate ()
                {
                    allowClose = true;
                    Close();
                }));
            }
        }

        private void CpkExtractor(string directory, string nameFile)
        {
            using (FileStream cpkStream = new FileStream(directory + "/" + nameFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int i = 0;
                using (var cpkReader = CriFsLib.Instance.CreateCpkReader(cpkStream, true))
                {
                    var innerFiles = cpkReader.GetFiles();
                    foreach (var file in innerFiles)
                    {
                        string outputPath = directory;
                        if (file.Directory is not null)
                        {
                            outputPath = Path.Combine(outputPath, file.Directory);
                            Directory.CreateDirectory(outputPath);
                        }
                        outputPath = Path.Combine(outputPath, file.FileName);

                        var data = cpkReader.ExtractFile(file);
                        FileStream outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                        outStream.Write(data.Span);
                        outStream.Flush();
                        outStream.Close();
                        i++;
                        switch (nameFile)
                        {
                            case "partition_data_win_demo_us.cpk":
                                UpdateProgressBar(i, innerFiles.Length, 0);
                                break;
                            case "partition_resident_win_demo.cpk":
                                UpdateProgressBar(i, innerFiles.Length, 1);
                                break;
                        }
                    }
                }
            }
        }

        private void Zip_OnExtractStatus(object sender, ProgressEventArgs e)
        {
            UpdateProgressBar(e.EntriesExtracted, e.EntriesTotal, 2);
        }

        private void UpdateProgressBar(int EntriesExtracted, int EntriesTotal, int step)
        {
            Dispatcher.BeginInvoke(new Action(delegate ()
            {
                int progress = 100 * EntriesExtracted / EntriesTotal;
                progressBar.Value = progress + step * 100;
                switch (step)
                {
                    case 0:
                        textBlock1.Text = "[Krok 1 z 3] Rozpakowywanie partition_data_win_demo_us.cpk..." + progress + "%";
                        break;
                    case 1:
                        textBlock1.Text = "[Krok 2 z 3] Rozpakowywanie partition_resident_win_demo.cpk..." + progress + "%";
                        break;
                    case 2:
                        textBlock1.Text = "[Krok 3 z 3] Instalowanie spolszczenia..." + progress + "%";
                        break;
                }
            }));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!allowClose)
            {
                if (MessageBoxResult.No == MessageBox.Show("Czy chcesz przerwać instalację spolszczenia?", "Uwaga!", MessageBoxButton.YesNo, MessageBoxImage.Warning))
                {
                    e.Cancel = true;
                }
            }
        }

        private void textBoxBrowse_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            textBlockInfo.Visibility = Visibility.Hidden;
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new System.Windows.Forms.FolderBrowserDialog();
            fbd.SelectedPath = textBoxBrowse.Text;
            fbd.Description = "Wybierz folder z grą.";
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBoxBrowse.Text = fbd.SelectedPath;
            }
        }
    }
}
