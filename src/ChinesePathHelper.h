#ifndef CHINESEPATHHELPER_H
#define CHINESEPATHHELPER_H

#include <QString>
#include <QDir>
#include <QFileInfo>

class ChinesePathHelper
{
public:
    static QString toLongPath(const QString &path)
    {
        if (path.isEmpty()) return path;
        QString native = QDir::toNativeSeparators(path);
        if (native.startsWith("\\\\?\\")) return native;
        if (native.startsWith("\\\\")) {
            return "\\\\?\\UNC\\" + native.mid(2);
        }
        return "\\\\?\\" + native;
    }

    static QString createBackupDirForFile(const QString &filePath)
    {
        QFileInfo info(filePath);
        QString baseDir = info.absolutePath();
        QString bakDirName = "Bak";
        QString bakPath = baseDir + "/" + bakDirName;
        int counter = 1;
        while (QFileInfo::exists(bakPath)) {
            bakPath = baseDir + QString("/Bak%1").arg(counter, 2, 10, QChar('0'));
            ++counter;
        }
        QDir().mkpath(bakPath);
        return bakPath;
    }
};

#endif