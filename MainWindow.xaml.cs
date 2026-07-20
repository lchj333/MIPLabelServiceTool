using MIPLabelServiceTool.Helpers;
using MIPLabelServiceTool.Services;
using System.IO;
using System.Windows;
using static MIPLabelServiceTool.Helpers.ExtensionHelper;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;

namespace MIPLabelServiceTool
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _selectedFile = "";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void DropArea_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
        }

        private async void DropArea_Drop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files == null || files.Length == 0)
                return;

            _selectedFile = files[0];
            txtInput.Text = _selectedFile;

            // Clear previous output and update status
            txtOutput.Text = "";
            txtFileInfo.Text = "MIP 레이블 정보를 확인하는 중...";

            try
            {
                string info = await Task.Run(() => MipHelper.GetFileLabelInfo(_selectedFile));
                txtFileInfo.Text = info;
            }
            catch (Exception ex)
            {
                txtFileInfo.Text = $"MIP 레이블 정보를 분석하지 못했습니다: {ex.Message}";
            }
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFile) || !File.Exists(_selectedFile))
            {
                MessageBox.Show("파일을 먼저 드래그&드랍하여 로드해 주세요.");
                return;
            }

            string labelText = txtLabelInfo.Text?.Trim();
            if (string.IsNullOrEmpty(labelText))
            {
                MessageBox.Show("레이블 ID 또는 이름을 입력하세요.");
                return;
            }

            txtOutput.Text = "레이블 적용 중...";

            try
            {
                var searchResult = await Task.Run(() => {
                    bool found = MipService.SearchMipLabelInfo(labelText, out var info);
                    return new { Found = found, LabelInfo = info };
                });

                if (searchResult.Found)
                {
                    var newfileName = ExtensionHelper.ConvertFileName(_selectedFile, ExtensionMode.MIP);
                    var output = _selectedFile.Replace(Path.GetFileName(_selectedFile), newfileName);

                    var processResult = await Task.Run(() => {
                        bool success = MipService.getInstance().SetLabelProcess(_selectedFile, output, searchResult.LabelInfo, out var decResult);
                        return new { Success = success, DecResult = decResult };
                    });

                    if (!processResult.Success)
                    {
                        txtOutput.Text = "";
                        MessageBox.Show(processResult.DecResult.error_msg);
                    }
                    else
                    {
                        txtOutput.Text = output;
                        MessageBox.Show("레이블 적용 완료");
                    }
                }
                else
                {
                    txtOutput.Text = "";
                    MessageBox.Show("레이블을 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                txtOutput.Text = "";
                MessageBox.Show($"오류 발생: {ex.Message}");
            }
        }

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFile) || !File.Exists(_selectedFile))
            {
                MessageBox.Show("파일을 먼저 드래그&드랍하여 로드해 주세요.");
                return;
            }

            txtOutput.Text = "레이블 제거 중...";

            try
            {
                var newfileName = ExtensionHelper.ConvertFileName(_selectedFile, ExtensionMode.NORAML);
                var output = _selectedFile.Replace(Path.GetFileName(_selectedFile), newfileName);

                var processResult = await Task.Run(() => {
                    bool success = MipService.getInstance().DecryptFileProcess(_selectedFile, output, out var decResult);
                    return new { Success = success, DecResult = decResult };
                });

                if (!processResult.Success)
                {
                    txtOutput.Text = "";
                    MessageBox.Show(processResult.DecResult.error_msg);
                }
                else
                {
                    txtOutput.Text = output;
                    MessageBox.Show("레이블 제거 완료");
                }
            }
            catch (Exception ex)
            {
                txtOutput.Text = "";
                MessageBox.Show($"오류 발생: {ex.Message}");
            }
        }

    }
}
