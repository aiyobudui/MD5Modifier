#ifndef FILEMD5STRATEGY_H
#define FILEMD5STRATEGY_H

#include <QString>
#include <QByteArray>
#include <QCryptographicHash>
#include <QFile>
#include <QFileInfo>
#include <QRandomGenerator>
#include "ChinesePathHelper.h"

class FileMd5Strategy
{
public:
    static bool modifyFile(const QString &filePath,
                           bool createBackup,
                           QString &outNewMd5,
                           QString &outBackupPath,
                           QString &errorMessage);

    static QString calculateMd5(const QString &filePath);

private:
    static bool modifyVideoFile(const QString &filePath);
    static bool appendDataToFile(const QString &filePath, const QByteArray &data);
    static bool isFileLocked(const QString &filePath);
};

#endif