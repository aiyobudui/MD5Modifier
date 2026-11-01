using System;
using System.IO;
using System.Security.Cryptography;

namespace MD5Modifier
{
    /// <summary>
    /// 文件 MD5 计算与修改策略。等价原 Qt 项目 FileMd5Strategy。
    /// 原理：在视频文件末尾追加 4 字节随机数据（播放器自动忽略），从而改变 MD5。
    /// </summary>
    internal static class FileMd5Strategy
    {
        private static readonly string[] VideoFormats =
        {
            "mp4", "mkv", "avi", "mov", "wmv", "flv",
            "mpg", "mpeg", "webm", "m4v", "ts", "m2ts"
        };

        /// <summary>
        /// 修改单个文件：检查存在/占用 -> 可选备份 -> 校验视频格式 -> 追加随机数据 -> 重算 MD5。
        /// </summary>
        public static bool ModifyFile(string filePath, bool createBackup,
            out string outNewMd5, out string outBackupPath, out string errorMessage)
        {
            outNewMd5 = string.Empty;
            outBackupPath = string.Empty;
            errorMessage = string.Empty;

            if (!File.Exists(filePath))
            {
                errorMessage = "文件不存在";
                return false;
            }

            if (IsFileLocked(filePath))
            {
                errorMessage = "文件被占用";
                return false;
            }

            if (createBackup)
            {
                string backupDir = ChinesePathHelper.CreateBackupDirForFile(filePath);
                string backupFilePath = Path.Combine(backupDir, Path.GetFileName(filePath));
                try
                {
                    File.Copy(ChinesePathHelper.ToLongPath(filePath),
                              ChinesePathHelper.ToLongPath(backupFilePath), false);
                    outBackupPath = backupFilePath;
                }
                catch (Exception)
                {
                    errorMessage = "备份失败";
                    return false;
                }
            }

            string suffix = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
            bool ok;
            if (Array.Exists(VideoFormats, s => s == suffix))
            {
                ok = ModifyVideoFile(filePath);
            }
            else
            {
                errorMessage = "不支持的文件格式: ." + suffix + "（仅支持视频文件）";
                return false;
            }

            if (!ok)
            {
                errorMessage = "修改失败";
                return false;
            }

            outNewMd5 = CalculateMd5(filePath);
            return true;
        }

        /// <summary>
        /// 使用 8MB 缓冲区计算文件 MD5，返回小写十六进制字符串。失败返回空字符串。
        /// </summary>
        public static string CalculateMd5(string filePath)
        {
            string longPath = ChinesePathHelper.ToLongPath(filePath);
            const int bufferSize = 8 * 1024 * 1024; // 8MB
            byte[] buffer = new byte[bufferSize];

            try
            {
                using (MD5 md5 = MD5.Create())
                {
                    using (FileStream fs = new FileStream(
                        longPath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite, bufferSize, FileOptions.SequentialScan))
                    {
                        int read;
                        while ((read = fs.Read(buffer, 0, bufferSize)) > 0)
                        {
                            md5.TransformBlock(buffer, 0, read, null, 0);
                        }
                        md5.TransformFinalBlock(buffer, 0, 0);
                    }

                    return BitConverter.ToString(md5.Hash)
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static bool ModifyVideoFile(string filePath)
        {
            byte[] data = new byte[4];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }
            return AppendDataToFile(filePath, data);
        }

        private static bool AppendDataToFile(string filePath, byte[] data)
        {
            string longPath = ChinesePathHelper.ToLongPath(filePath);
            try
            {
                using (FileStream fs = new FileStream(
                    longPath, FileMode.Open, FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete, 4096,
                    FileOptions.WriteThrough))
                {
                    fs.Seek(0, SeekOrigin.End);
                    fs.Write(data, 0, data.Length);
                    fs.Flush();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 检测文件是否被其他进程占用（以共享读方式尝试打开，失败视为占用）。
        /// 注意：仅以读共享方式探测，避免将只读文件误判为占用。
        /// </summary>
        public static bool IsFileLocked(string filePath)
        {
            string longPath = ChinesePathHelper.ToLongPath(filePath);
            try
            {
                using (new FileStream(
                    longPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
