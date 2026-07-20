using System.IO;
using System.Text.RegularExpressions;

namespace MIPLabelServiceTool.Helpers
{
    public class ExtensionHelper
    {
        public static string ConvertFileName(string input, ExtensionMode mode)
        {
            var fullName = Path.GetFileName(input.ToLower());
            var extension = fullName.Substring(fullName.IndexOf("."));
            var fileName = fullName.Substring(0, fullName.IndexOf("."));
            string newFileName;

            switch (mode)
            {
                case ExtensionMode.NORAML:
                    if (Regex.IsMatch(extension, @"\.p(txt|jpg|jpeg)$"))
                    {
                        newFileName = fileName + "." + extension.Substring(2);
                    }
                    else if (Regex.IsMatch(extension, @"\.(ppt|xls|doc|pdf)$"))
                    {
                        newFileName = fullName;
                    }
                    else
                    {
                        newFileName = fullName.Replace(".pfile", "");
                    }
                    break;
                case ExtensionMode.MIP:
                    if (extension.EndsWith(".pfile"))
                    {
                        newFileName = fullName;
                    }
                    else if (Regex.IsMatch(extension, @"\.(txt|jpg|jpeg)$"))
                    {
                        newFileName = fileName + ".p" + extension.Substring(1);
                    }
                    else if (Regex.IsMatch(extension, @"\.(ppt|xls|doc|pdf)"))
                    {
                        newFileName = fullName;
                    }
                    else
                    {
                        newFileName = fullName + ".pfile";
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            return newFileName;
        }

        public enum ExtensionMode
        {
            NORAML,
            MIP
        }
    }

}
