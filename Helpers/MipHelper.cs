using Microsoft.Identity.Client;
using Microsoft.InformationProtection;
using Microsoft.InformationProtection.Exceptions;
using Microsoft.InformationProtection.File;
using System.Collections.ObjectModel;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Windows;

namespace MIPLabelServiceTool.Helpers
{
    public class MipHelper
    {
        public static readonly bool _enable = true;
        public static readonly bool _selfAuthMode = false; // 메소드 테스트용 셀프 인증 모드
        private static readonly string _tenantId = ConfigHelper.GetRequiredValue("MipSettings:TenantId");
        private static readonly string _clientId = ConfigHelper.GetRequiredValue("MipSettings:ClientId");
        private static readonly string _systemEmail = ConfigHelper.GetRequiredValue("MipSettings:SystemEmail");
        private static readonly string _locale = "ko-KR";
        private static readonly SemaphoreSlim _MipFileProfileSemaphore = new SemaphoreSlim(1, 1);
        private static CacheStorageType _cacheStorageType = CacheStorageType.OnDiskEncrypted;
        private static MipContext _mipContext;
        private static IFileProfile _mipFileProfile;
        private static IFileEngine _mipDecFileEngine;
        private static DateTime _engineLastCreated;

        /**
         * 비용이 큰 MIP 설정 객체를 한번만 인스턴스화 하여 추후 엔진 생성 시 재사용
         */
        private static async Task<IFileProfile> GetMipFileProfileInstance()
        {
            if (_mipFileProfile != null) return _mipFileProfile;

            try
            {
                // 비동기 락 진입
                await _MipFileProfileSemaphore.WaitAsync();
                // 락 기다리던 중 생성된 엔진 있는지 확인
                if (_mipFileProfile != null) return _mipFileProfile;

                if (_mipContext == null)
                {
                    // 1. 응용 프로그램 정보 설정
                    var appInfo = new ApplicationInfo
                    {
                        ApplicationId = _clientId,
                        ApplicationName = "M365ExportSystem",
                        ApplicationVersion = "1.0.0"
                    };

                    MIP.Initialize(MipComponent.File);

                    // 2. Create MipConfiguration Object
                    // ** 만약 CacheStorageType.OnDiskEncrypted 설정 상태에서 에러 발생 시 일단 CacheStorageType.InMemory로 먼저 변경 후 테스트 성공시 2번째 인자값 확인
                    var mipConfiguration = new MipConfiguration(appInfo, "mip_data", Microsoft.InformationProtection.LogLevel.Warning, false, _cacheStorageType);
                    // 3. MIP Context 생성 (설정 객체를 생성하여 인자로 삽입)
                    _mipContext = MIP.CreateMipContext(mipConfiguration);
                }

                if (_mipFileProfile == null)
                {
                    // 4. File Profile 설정
                    // 변경: 라이브러리에 4-인자 생성자가 없으므로 3-인자 생성자로 호출하며 3번째파라미터에 consentDelegate를 전달합니다.
                    // mipConfiguration 의 CacheStorageType 동일하게 설정
                    var profileSettings = new FileProfileSettings(
                        _mipContext,
                        _cacheStorageType,
                        new ConsentDelegateImplementation()
                    );
                    _mipFileProfile = await MIP.LoadFileProfileAsync(profileSettings);
                }
            }
            finally
            {
                // 반드시 실행되어야 하므로 finally에서 비동기 락 해제
                _MipFileProfileSemaphore.Release();
            }

            return _mipFileProfile;
        }

        /// <summary>
        /// MIP 레이블 제거용으로 이미 만들어둔 엔진을 불러온다.
        /// </summary>
        /// <remarks>
        /// 만들어둔 엔진이 없거나 만든 후 특정 시간만큼 소요되었다면 엔진 다시 생성.
        /// MIP 레이블 제거 위해서 어플리케이션 권한 중 "슈퍼 유저"권한 부여상태 확인 필요.
        /// </remarks>
        /// <returns>IFileEngine</returns>
        public static IFileEngine GetMipDecFileEngine()
        {
            if (_mipDecFileEngine == null || DateTime.Now.Subtract(_engineLastCreated).TotalHours > 2)
            {
                _mipDecFileEngine = CreateMipFileEngine();
                if (_mipDecFileEngine != null) _engineLastCreated = DateTime.Now;
            }

            return _mipDecFileEngine;
        }

        /// <summary>
        /// MIP 파일 핸들러를 생성하기 위해 필요한 MIP 파일 엔진을 생성하여 리턴
        /// </summary>
        /// <remarks>
        /// 파일 엔진을 생성하면 라이브러리는 캐시를 메모리에 저장한다.
        /// 빠른 로딩을 위해 레이블 제거에 필요한 엔진은 캐시에서 빠르게 로딩이 필요하지만
        /// 사용자 위임 위해 생성된 엔진은 <see cref="UnloadMipEngine(string)"/>을 호출하여 메모리 해제 필요.
        /// </remarks>
        /// <returns></returns>
        public static IFileEngine CreateMipFileEngine()
        {
            string clientSecret = ConfigHelper.GetRequiredValue("MipSettings:ClientSecret");
            // 1 ~ 4. MIP 엔진 프로파일 설정 가져오기
            IFileProfile mipFileProfile = Task.Run(() => GetMipFileProfileInstance()).GetAwaiter().GetResult();
            // 5. File Engine 설정 (시스템 관리자 계정 권한)
            // 수정: FileEngineSettings의 생성자 시그니처가 (string, IAuthDelegate, string, string) 등을 기대하므로
            // 두 번째 인자로 authDelegate를 전달하고 locale을 네 번째 인자로 유지합니다.
            IAuthDelegate authDelegate;
            // 어플리케이션 모드 (관리자 권한)
            authDelegate = new AuthDelegateImplementation(_clientId, _tenantId, clientSecret);

            var engineSettings = new FileEngineSettings("", authDelegate, "", _locale)
            {
                // Identity없이IFileEngine 객체를 생성하는 경우 에러 발생
                Identity = new Identity(_systemEmail),
            };
            return mipFileProfile.AddEngineAsync(engineSettings).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 입력 파일 Microsoft Information Purview 레이블 처리
        /// </summary>
        /// <remarks>
        /// M365파일에 비밀번호가 걸려있는 경우 파일 핸들러 생성 시 에러가 발생하므로
        /// 처리 이전 프로세스에서 비밀번호를 제거 후 업로드 하도록 유도해야 한다.
        /// MIP레이블이 걸려 있는 파일에 대해 레이블 제거 후 출력 디렉토리에 저장한다.
        /// </remarks>
        /// <param name="inputFilePath">입력파일 전체경로</param>
        /// <param name="outputDirPath">출력 디렉토리</param>
        /// <param name="fileName">출력 파일이름</param>
        /// <param name="originalName">메시지 출력 시 파일명</param>
        /// <param name="return_msg"></param>
        /// <param name="isInputFileDelete">default:false</param>
        /// <param name="isDelegateMode">(옵션)사용자권한위임모드</param>
        /// <param name="fileEngine">(옵션)사용자권한위임엔진</param>
        /// <param name="labelId">(옵션)레이블 ID</param>
        /// <returns>레이블 제거 성공 또는 일반 파일: true, otherwise: false</returns>
        public static bool ProcessDocument(string inputFilePath, string outputDirPath, string fileName, out string return_msg, Label? targetLabel = null)
        {
            return_msg = $"[파일명: {fileName}] ";

            // 원본 파일 복사를 위해 경로 설정 (원본 파일명 유지)
            string tempCopyFilePath = string.Empty;
            string tempFileName = $"MIPTEMP_{fileName}";

            try
            {
                /*** 동작 모드 판별 ***/
                if (!_enable)
                {
                    File.Copy(inputFilePath, Path.Combine(outputDirPath, fileName), true);
                    return_msg += "[성공] 테스트 모드";
                    return true;
                }

                /*** 실제 동작 시작 ***/
                // 원본 파일을 임시 위치로 복사
                tempCopyFilePath = Path.Combine(outputDirPath, tempFileName);
                File.Copy(inputFilePath, tempCopyFilePath, true);

                // 레이블 제거용 파일 엔진 객체를 불러온다
                var fileEngine = GetMipDecFileEngine();

                // 6. File Handler 생성 (문서 로드 및 감사 로그 활성화)
                // 3번째 인자는 감사 로그 활성화 유무 설정
                // -> true인 경우 레이블 검색에 대한 감사 로그를 마이크로소프트 서버로 전송.
                bool isNormalFile = false;
                using (var fileHandler = fileEngine.CreateFileHandlerAsync(tempCopyFilePath, tempCopyFilePath, true).GetAwaiter().GetResult())
                {
                    // 7. 레이블 처리 옵션 (작동 방식 & 메시지 설정)
                    var labelingOptions = new LabelingOptions()
                    {
                        IsDowngradeJustified = true, // 낮은 우선순위로 레이블 재 조정시 필요한 옵션
                        JustificationMessage = "MIPLabelServiceTool", // purview 사이트의 관리자 정책 설정에 따라 메시지가 필수사항일 수 있음.
                        AssignmentMethod = AssignmentMethod.Privileged, // Privileged : 레이블 강제 덮어쓰기, standard: 우선순위로 판별 적용
                    };

                    if (fileHandler.Label?.Label != null || targetLabel != null)
                    {
                        if (targetLabel != null)
                        {
                            // 8. 레이블 변경 예약
                            fileHandler.SetLabel(targetLabel, labelingOptions, new ProtectionSettings());
                        }
                        else
                        {
                            // 8. 레이블 제거 처리 예약
                            fileHandler.DeleteLabel(labelingOptions);
                        }

                        // 9. 최종 파일 커밋 (실제 파일 처리 메소드 호출)
                        fileHandler.CommitAsync(Path.Combine(outputDirPath, fileName));
                        return_msg += "[성공] 레이블 처리 성공";
                    }
                    else
                    {
                        isNormalFile = true;
                    }
                }

                if (isNormalFile)
                {   // fileHandler 프로세스 점유 문제로 밖에서 파일 이동
                    File.Move(tempCopyFilePath, Path.Combine(outputDirPath, fileName), true);
                    return_msg += "[성공처리] 일반 파일 (처리 안하고 파일 이동)";
                }

                return true;
            }
            catch (NotSupportedOperationException ex)
            {
                //LogHelper.ErrorLogAppend("Module/MipHelper/ProcessDocumentForExport", $"[{fileName}][성공처리] {ex.Message}", ex.StackTrace);
                File.Move(tempCopyFilePath, Path.Combine(outputDirPath, fileName), true);
                return_msg += "[성공처리] 지원되지 않는 파일 형식입니다. (단순 파일 복사 처리)";
                return true;
            }
            catch (NotSupportedException ex)
            {
                //LogHelper.ErrorLogAppend("Module/MipHelper/ProcessDocumentForExport", $"[{fileName}][성공처리] {ex.Message}", ex.StackTrace);
                File.Move(tempCopyFilePath, Path.Combine(outputDirPath, fileName), true);
                return_msg += "[성공처리] 지원되지 않는 파일 형식입니다. (단순 파일 복사 처리)";
                return true;
            }
            catch (ContentFormatNotSupportedException ex)
            {
                //LogHelper.ErrorLogAppend("Module/MipHelper/ProcessDocumentForExport", $"[{fileName}][성공처리] {ex.Message}", ex.StackTrace);
                File.Move(tempCopyFilePath, Path.Combine(outputDirPath, fileName), true);
                return_msg += "[성공처리] 지원되지 않는 파일 형식입니다. (단순 파일 복사 처리)";
                return true;
            }
            catch (Exception ex)
            {
                //LogHelper.ErrorLogAppend("Module/MipHelper/ProcessDocumentForExport", $"[{fileName}] {ex.Message}", ex.StackTrace);

                return_msg += ex switch
                {
                    AccessDeniedException => "[실패] 엑세스 권한이 없습니다.",
                    LicenseNotRegisteredException => "[실패] 등록되지 않은 라이센스 입니다. 관리자에게 문의하세요.",
                    BadInputException => "[실패] BadInputException 발생. 관리자에게 문의하세요.",
                    ConsentDeniedException => "[실패] 접근할 수 없는 파일입니다.",
                    DelegateResponseException => "[실패] 권한 위임 실패 에러 발생",
                    FileIOException => "[실패] 파일 처리에 실패했습니다.",
                    NetworkException => "[실패] MIP 네트워크 연결 실패",
                    NoPolicyException => "[실패] 존재하지 않는 정책입니다.",
                    PrivilegedRequiredException => "[실패] 레이블 재 할당 관련 에러 발생. 관리자에게 문의하세요.",
                    _ => "[실패] 내부 서버 오류 확인 필요"
                };
            }
            finally
            {
                // 임시 파일 삭제
                DeleteTempFile(tempCopyFilePath);
            }
            // catch 문 이후 false 리턴
            return false;
        }

        public static string GetFileLabelInfo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return "파일이 존재하지 않습니다.";
            }

            string tempCopyFilePath = Path.Combine(Path.GetTempPath(), $"MIP_READ_{Guid.NewGuid()}_{Path.GetFileName(filePath)}");
            try
            {
                File.Copy(filePath, tempCopyFilePath, true);
                var fileEngine = GetMipDecFileEngine();
                using (var fileHandler = fileEngine.CreateFileHandlerAsync(tempCopyFilePath, tempCopyFilePath, true).GetAwaiter().GetResult())
                {
                    var contentLabel = fileHandler.Label;
                    if (contentLabel == null || contentLabel.Label == null)
                    {
                        return "일반 파일입니다.";
                    }

                    var label = contentLabel.Label;
                    string name = label.Name ?? "";
                    string id = label.Id ?? "";
                    int priority = label.Sensitivity;
                    bool isProtected = contentLabel.IsProtectionAppliedFromLabel;

                    return $"레이블 이름: {name}\nGUID: {id}\n중요도: {priority}\nprotect 여부: {isProtected}";
                }
            }
            catch (Exception)
            {
                return "일반 파일입니다.";
            }
            finally
            {
                if (File.Exists(tempCopyFilePath))
                {
                    try { File.Delete(tempCopyFilePath); } catch { }
                }
            }
        }

        private static void DeleteTempFile(string filePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) File.Delete(filePath);
            }
            catch (Exception ex)
            {
                //LogHelper.ErrorLogAppend("Module/MipHelper/DeleteTempFile", $"{Path.GetFileName(filePath)} MIP 복호화 임시파일 삭제 실패", ex.StackTrace);
            }
        }

        public static bool SearchMipLabelInfo(string labelInfo, out Label result)
        {
            result = null;
            try
            {
                var fileEngine = GetMipDecFileEngine();
                try
                {
                    result = fileEngine.GetLabelById(labelInfo);
                    if (result != null)
                        return true;
                    else
                        return FindMipLabelByName(fileEngine.SensitivityLabels, labelInfo, out result);
                }
                catch
                {
                    return FindMipLabelByName(fileEngine.SensitivityLabels, labelInfo, out result);
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        public static bool FindMipLabelByName(ReadOnlyCollection<Label> labels, string labelName, out Label result)
        {
            result = null;
            foreach (var label in labels)
            {
                if (label.Name == labelName || label.Id == labelName)
                {
                    result = label;
                    return true;
                }
                else if (label.Children != null && label.Children.Count > 0)
                {
                    if (FindMipLabelByName(label.Children, labelName, out result))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    // --- 필수 인터페이스 구현체 ---
    public class AuthDelegateImplementation : IAuthDelegate
    {
        private readonly string _clientId;
        private readonly string _tenantId;
        private readonly string _clientSecret;

        public AuthDelegateImplementation(string clientId, string tenantId, string clientSecret)
        {
            _clientId = clientId;
            _tenantId = tenantId;
            _clientSecret = clientSecret;
        }

        public string AcquireToken(Identity identity, string authority, string resource, string claims)
        {
            try
            {
                // APP 클라이언트 인증 방식 (마스터)
                var app = ConfidentialClientApplicationBuilder.Create(_clientId)
                    .WithClientSecret(_clientSecret)
                    .WithAuthority(AzureCloudInstance.AzurePublic, _tenantId)
                    .Build();

                string[] scopes = [$"{resource.TrimEnd('/')}/.default"];
                var result = app.AcquireTokenForClient(scopes).ExecuteAsync().GetAwaiter().GetResult();
                //var result = Task.Run(() => app.AcquireTokenForClient(scopes).ExecuteAsync())?.Result;

                return result?.AccessToken;
            }
            catch (MsalServiceException ex)
            {
                throw new Exception($"MSAL 인증 실패: {ex.ErrorCode}, {ex.Message}", ex);
            }
        }
    }

    public class ConsentDelegateImplementation : IConsentDelegate
    {
        public Microsoft.InformationProtection.Consent GetUserConsent(string url)
        {
            return Microsoft.InformationProtection.Consent.Accept;
        }
    }
}
