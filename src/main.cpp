#include <QApplication>
#include <QFont>
#include <QMetaType>
#include "MainWindow.h"

int main(int argc, char *argv[])
{
    qRegisterMetaType<FileItem>("FileItem");
    qRegisterMetaType<QList<FileItem>>("QList<FileItem>");
    
    QApplication app(argc, argv);
    
    QFont font(QString::fromLocal8Bit("Microsoft YaHei"), 9);
    app.setFont(font);
    
    MainWindow w;
    w.show();
    
    return app.exec();
}