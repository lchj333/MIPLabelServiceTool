
using Microsoft.InformationProtection;
using MIPLabelServiceTool.Helpers;
using MIPLabelServiceTool.Models;
using System.IO;
using System.Windows;
using static MIPLabelServiceTool.Helpers.ExtensionHelper;

namespace MIPLabelServiceTool.Services
{
    internal class MipService
    {
        private static MipService? _mipService = null;

        public static MipService getInstance()
        {
            if (_mipService == null)
            {
                _mipService = new MipService();
            }
            return _mipService;
        }

        public bool SetLabelProcess(string srcFilePath, string outputFilePath, Label targetLabel, out DecResult decResult)
        {
            var return_msg = string.Empty;
            bool isDecryptSuccess = false;
            var tempSrcFilePath = srcFilePath;
            string outputFileName = Path.GetFileName(outputFilePath);
            Console.WriteLine(outputFileName);
            
            string outputDirPath = outputFilePath.Replace(outputFileName, "");
            Console.WriteLine(outputDirPath);
            decResult = new DecResult("DRM", true, $"[파일명: {outputFileName}] 복호화 처리가 정상 처리되었습니다.");
            MessageBox.Show($@"
                outputDirPath: {outputDirPath}
                outputFileName: {outputFileName}
            ");

            try
            {
                isDecryptSuccess = MipHelper.ProcessDocument(srcFilePath, outputDirPath, outputFileName, out return_msg, targetLabel: targetLabel);
                if (!isDecryptSuccess)
                {
                    //LogHelper.ErrorLogAppend("Module/DrmData/FileDecrypt", $"[{outputFileName}] 파일 복호화 실패", return_msg);
                    decResult = new DecResult("MIP", isDecryptSuccess, isDecryptSuccess ? string.Empty : return_msg);
                    return false;
                }
            }
            finally
            {// 다음 복호화를 위해 생성한 임시 파일 있으면 삭제
                if (tempSrcFilePath.Equals(srcFilePath) == false)
                {
                    try
                    {
                        if (File.Exists(tempSrcFilePath)) File.Delete(tempSrcFilePath);
                    }
                    catch (Exception ex)
                    {
                        //LogHelper.ErrorLogAppend("Module/DrmData/DecryptFileProcess", $"{outputFileName} 복호화 임시파일 삭제 실패", ex.StackTrace);
                    }
                }
            }

            return isDecryptSuccess;
        }

        /// <remarks>
        ///     여기서 호출되는 복호화 메소드는 일반 파일 처리시 성공처리이며 일반 파일 처리 시 파일 복사로 동작해야 정상 작동을 보장함.
        /// </remarks>
        /// <param name="srcFilePath">디렉토리 경로 + 파일 이름</param>
        /// <param name="dstDirPath">결과 디렉토리 경로</param>
        /// <param name="fileName">결과 파일 이름</param>
        /// <param name="originalName">원본 파일 이름</param>
        /// <param name="DecResult"/>
        /// <returns>복호화 모두 성공 : TRUE, otherwise : FALSE</returns>
        public bool DecryptFileProcess(string srcFilePath, string outputFilePath, out DecResult decResult)
        {
            var return_msg = string.Empty;
            bool isDecryptSuccess = false;
            var tempSrcFilePath = srcFilePath;
            string outputFileName = Path.GetFileName(outputFilePath);
            string outputDirPath = outputFilePath.Replace(outputFileName, "");
            decResult = new DecResult("MIP", true, $"[파일명: {outputFileName}] 정상 처리되었습니다.");

            try
            {
                isDecryptSuccess = MipHelper.ProcessDocument(tempSrcFilePath, outputDirPath, outputFileName, out return_msg);
                if (isDecryptSuccess == false)
                {
                    //LogHelper.ErrorLogAppend("Module/DrmData/FileDecrypt", $"[{outputFileName}] 파일 복호화 실패", return_msg);
                    decResult = new DecResult("MIP", isDecryptSuccess, isDecryptSuccess ? string.Empty : return_msg);
                    return false;
                }
                else
                {
                    // MIP 파일 확장자일 경우 원본 확장자로 업데이트
                    string changedExtFileName = ExtensionHelper.ConvertFileName(outputFileName, ExtensionMode.NORAML);
                    File.Move(Path.Combine(outputDirPath, outputFileName), Path.Combine(outputDirPath, changedExtFileName));
                }
            }
            finally
            {
                // 다음 복호화를 위해 생성한 임시 파일 있으면 삭제
                if (tempSrcFilePath.Equals(srcFilePath) == false)
                {
                    try
                    {
                        if (File.Exists(tempSrcFilePath)) File.Delete(tempSrcFilePath);
                    }
                    catch (Exception ex)
                    {
                        //LogHelper.ErrorLogAppend("Module/DrmData/DecryptFileProcess", $"{outputFileName} 복호화 임시파일 삭제 실패", ex.StackTrace);
                    }
                }
            }

            return isDecryptSuccess;
        }

        public static bool SearchMipLabelInfo(string labelInfo, out Label result)
        {
            return MipHelper.SearchMipLabelInfo(labelInfo, out result);
        }
    }
}
