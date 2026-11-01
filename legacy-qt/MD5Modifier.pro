QT += core gui widgets concurrent
CONFIG += c++17
CONFIG -= app_bundle

TARGET = MD5Modifier
TEMPLATE = app

SOURCES = \
    src/main.cpp \
    src/MainWindow.cpp \
    src/FileMd5Strategy.cpp

HEADERS = \
    src/MainWindow.h \
    src/FileMd5Strategy.h \
    src/ChinesePathHelper.h

win32 {
    LIBS += -luser32 -lshell32 -lrstrtmgr
    DEFINES += _WIN32_WINNT=0x0A00
    RC_ICONS = assets/app.ico
}

DESTDIR = $$OUT_PWD
OBJECTS_DIR = $$OUT_PWD/obj
MOC_DIR = $$OUT_PWD/moc

QMAKE_CXXFLAGS += -std=c++17

win32-g++* {
    QMAKE_CXXFLAGS += -finput-charset=UTF-8 -fexec-charset=GBK
    QMAKE_LFLAGS += -finput-charset=UTF-8 -fexec-charset=GBK
}

win32-msvc* {
    QMAKE_CXXFLAGS += /utf-8
}

message("========================================")
message("Building MD5Modifier with Qt6")
message("Qt version: $$QT_VERSION")
message("Sources: $$SOURCES")
message("Headers: $$HEADERS")
message("========================================")
