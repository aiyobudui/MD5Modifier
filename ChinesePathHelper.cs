using System;
using System.IO;

namespace MD5Modifier
{
    /// <summary>
    /// 中文/超长路径处理与备份目录工具。
    /// 等价原 Qt 项目 ChinesePathHelper：为路径添加 \\?\ 长路径前缀，
    /// 并为单个文件生成自动命名的备份目录 Bak / Bak01 / Bak02 ...
    /// </summary>
    internal static class ChinesePathHelper
    {
        /// <summary>
        /// 将原路径转换为 Windows 长路径（\\?\ 前缀），以支持超过 MAX_PATH 的中文/深层路径。
        /// UNC 路径转换为 \\?\UNC\ 形式。
        /// </summary>
        public static string ToLongPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            string native = path.Replace('/', '\\');
            if (native.StartsWith("\\\\?\\"))
                return native;
            if (native.StartsWith("\\\\"))
                return "\\\\?\\UNC\\" + native.Substring(2);
            return "\\\\?\\" + native;
        }

        /// <summary>
        /// 为指定文件所在的目录创建备份文件夹。若 Bak 已存在则依次尝试 Bak01、Bak02 ...
        /// 返回创建的备份目录完整路径。
        /// </summary>
        public static string CreateBackupDirForFile(string filePath)
        {
            string baseDir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(baseDir))
                baseDir = ".";

            string bakPath = Path.Combine(baseDir, "Bak");
            int counter = 1;
            while (Directory.Exists(bakPath))
            {
                bakPath = Path.Combine(baseDir, string.Format("Bak{0:00}", counter));
                counter++;
            }

            // 以长路径前缀创建，支持超过 MAX_PATH 的目录
            Directory.CreateDirectory(ToLongPath(bakPath));
            return bakPath;
        }
    }
}
