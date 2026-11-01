#include <QCoreApplication>
#include <QImage>
#include <QImageReader>
#include <QFile>
#include <QFileInfo>
#include <QDataStream>
#include <QDebug>
#include <QList>
#include <QBuffer>

bool createIco(const QString &inputPath, const QString &outputPath)
{
    QImageReader reader(inputPath);
    if (!reader.canRead()) {
        qDebug() << "Cannot read image:" << inputPath;
        return false;
    }
    
    QImage originalImage = reader.read();
    if (originalImage.isNull()) {
        qDebug() << "Failed to read image";
        return false;
    }
    
    qDebug() << "Original size:" << originalImage.size();
    
    QList<int> sizes = {16, 24, 32, 48, 64, 96, 128, 192, 256};
    QList<QByteArray> pngDataList;
    
    for (int size : sizes) {
        QImage scaled = originalImage.scaled(size, size, Qt::KeepAspectRatio, Qt::SmoothTransformation);
        QByteArray pngData;
        QBuffer buffer(&pngData);
        buffer.open(QIODevice::WriteOnly);
        scaled.save(&buffer, "PNG");
        pngDataList.append(pngData);
        qDebug() << "Created" << size << "x" << size << "icon";
    }
    
    QFile file(outputPath);
    if (!file.open(QIODevice::WriteOnly)) {
        qDebug() << "Cannot open output file:" << outputPath;
        return false;
    }
    
    // ICO Header
    quint16 reserved = 0;
    quint16 type = 1;
    quint16 count = static_cast<quint16>(sizes.size());
    
    file.write(reinterpret_cast<const char*>(&reserved), 2);
    file.write(reinterpret_cast<const char*>(&type), 2);
    file.write(reinterpret_cast<const char*>(&count), 2);
    
    // Directory entries
    quint32 offset = 6 + count * 16;
    
    for (int i = 0; i < sizes.size(); ++i) {
        quint8 width = sizes[i] >= 256 ? 0 : static_cast<quint8>(sizes[i]);
        quint8 height = width;
        quint8 colors = 0;
        quint8 reserved2 = 0;
        quint16 planes = 1;
        quint16 bpp = 32;
        quint32 imageSize = static_cast<quint32>(pngDataList[i].size());
        
        file.write(reinterpret_cast<const char*>(&width), 1);
        file.write(reinterpret_cast<const char*>(&height), 1);
        file.write(reinterpret_cast<const char*>(&colors), 1);
        file.write(reinterpret_cast<const char*>(&reserved2), 1);
        file.write(reinterpret_cast<const char*>(&planes), 2);
        file.write(reinterpret_cast<const char*>(&bpp), 2);
        file.write(reinterpret_cast<const char*>(&imageSize), 4);
        file.write(reinterpret_cast<const char*>(&offset), 4);
        
        offset += imageSize;
    }
    
    // PNG data
    for (const QByteArray &pngData : pngDataList) {
        file.write(pngData);
    }
    
    file.close();
    
    qDebug() << "Icon saved to:" << outputPath;
    qDebug() << "File size:" << QFileInfo(outputPath).size() << "bytes";
    
    return true;
}

int main(int argc, char *argv[])
{
    QCoreApplication app(argc, argv);
    
    QString inputPath = "assets/APP.png";
    QString outputPath = "assets/app.ico";
    
    if (createIco(inputPath, outputPath)) {
        qDebug() << "Done!";
        return 0;
    } else {
        qDebug() << "Failed!";
        return 1;
    }
}
