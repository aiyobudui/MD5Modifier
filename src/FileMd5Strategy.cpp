#include "FileMd5Strategy.h"
#include <QDebug>
#include <windows.h>
#include <restartmanager.h>
#include <io.h>
#include <fcntl.h>

bool FileMd5Strategy::modifyFile(const QString &filePath,
                                 bool createBackup,
                                 QString &outNewMd5,
                                 QString &outBackupPath,
                                 QString &errorMessage)
{
    qDebug() << "modifyFile called:" << filePath;
    
    QFileInfo fileInfo(filePath);
    if (!fileInfo.exists()) {
        errorMessage = QString::fromLocal8Bit("文件不存在");
        qDebug() << "File not exists";
        return false;
    }

    if (isFileLocked(filePath)) {
        errorMessage = QString::fromLocal8Bit("文件被占用");
        qDebug() << "File is locked";
        return false;
    }

    if (createBackup) {
        QString backupDir = ChinesePathHelper::createBackupDirForFile(filePath);
        QString backupFilePath = backupDir + "/" + fileInfo.fileName();
        if (QFile::copy(filePath, backupFilePath)) {
            outBackupPath = backupFilePath;
        } else {
            errorMessage = QString::fromLocal8Bit("备份失败");
            return false;
        }
    }

    QString suffix = fileInfo.suffix().toLower();
    bool ok = false;

    qDebug() << "File suffix:" << suffix;

    if (suffix == "mp4" || suffix == "mkv" || suffix == "avi" ||
        suffix == "mov" || suffix == "wmv" || suffix == "flv" ||
        suffix == "mpg" || suffix == "mpeg" || suffix == "webm" ||
        suffix == "m4v" || suffix == "ts" || suffix == "m2ts") {
        ok = modifyVideoFile(filePath);
    }
    else {
        errorMessage = QString::fromLocal8Bit("不支持的文件格式: .") + suffix + QString::fromLocal8Bit("（仅支持视频文件）");
        return false;
    }

    if (!ok) {
        errorMessage = QString::fromLocal8Bit("修改失败");
        return false;
    }

    outNewMd5 = calculateMd5(filePath);
    return true;
}

QString FileMd5Strategy::calculateMd5(const QString &filePath)
{
    QFile file(ChinesePathHelper::toLongPath(filePath));
    if (!file.open(QIODevice::ReadOnly)) {
        return QString();
    }
    
    QCryptographicHash hash(QCryptographicHash::Md5);
    
    const qint64 bufferSize = 8 * 1024 * 1024;  // 8MB 缓冲区
    QByteArray buffer;
    
    while (!file.atEnd()) {
        buffer = file.read(bufferSize);
        hash.addData(buffer);
    }
    
    return QString(hash.result().toHex());
}

bool FileMd5Strategy::modifyVideoFile(const QString &filePath)
{
    qDebug() << "modifyVideoFile called for:" << filePath;
    
    qint32 randomNum = QRandomGenerator::global()->bounded(0, 2147483647);
    QByteArray data;
    data.append(reinterpret_cast<const char*>(&randomNum), sizeof(randomNum));
    
    qDebug() << "Data size:" << data.size() << "bytes";
    
    bool result = appendDataToFile(filePath, data);
    qDebug() << "appendDataToFile result:" << result;
    
    return result;
}

bool FileMd5Strategy::appendDataToFile(const QString &filePath, const QByteArray &data)
{
    QString longPath = ChinesePathHelper::toLongPath(filePath);
    qDebug() << "appendDataToFile: longPath =" << longPath;
    
    HANDLE hFile = CreateFileW(
        reinterpret_cast<LPCWSTR>(longPath.utf16()),
        FILE_APPEND_DATA | SYNCHRONIZE,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        NULL,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,
        NULL
    );
    
    if (hFile == INVALID_HANDLE_VALUE) {
        DWORD error = GetLastError();
        qDebug() << "Failed to open file with Windows API, error:" << error;
        
        QFile file(longPath);
        if (!file.open(QIODevice::Append | QIODevice::WriteOnly)) {
            qDebug() << "Qt fallback also failed:" << file.errorString();
            return false;
        }
        qint64 written = file.write(data);
        file.flush();
        file.close();
        return written == data.size();
    }
    
    qDebug() << "File opened with Windows API";
    
    LARGE_INTEGER fileSize;
    GetFileSizeEx(hFile, &fileSize);
    qDebug() << "File size before write:" << fileSize.QuadPart;
    
    DWORD bytesWritten = 0;
    BOOL success = WriteFile(hFile, data.constData(), data.size(), &bytesWritten, NULL);
    
    qDebug() << "WriteFile result:" << success << "bytes written:" << bytesWritten;
    
    FlushFileBuffers(hFile);
    CloseHandle(hFile);
    
    QFileInfo info(filePath);
    qDebug() << "File size after write:" << info.size();
    
    return success && bytesWritten == (DWORD)data.size();
}

bool FileMd5Strategy::isFileLocked(const QString &filePath)
{
    QString longPath = ChinesePathHelper::toLongPath(filePath);
    
    HANDLE hFile = CreateFileW(
        reinterpret_cast<LPCWSTR>(longPath.utf16()),
        FILE_APPEND_DATA,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        NULL,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,
        NULL
    );
    
    if (hFile == INVALID_HANDLE_VALUE) {
        DWORD error = GetLastError();
        qDebug() << "File locked check: error =" << error;
        return true;
    }
    
    CloseHandle(hFile);
    return false;
}