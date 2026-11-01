#ifndef MAINWINDOW_H
#define MAINWINDOW_H

#include <QMainWindow>
#include <QThread>
#include <QTableWidgetItem>
#include <QFileInfo>
#include <QLabel>
#include <QMap>
#include <QMutex>
#include <QMetaType>
#include <QWaitCondition>

QT_BEGIN_NAMESPACE
class QLineEdit;
class QPushButton;
class QCheckBox;
class QTableWidget;
class QProgressBar;
class QLabel;
QT_END_NAMESPACE

struct FileItem {
    bool selected = true;
    QString fileName;
    QString suffix;
    qint64 size = 0;
    QString originalMd5;
    QString newMd5;
    QString status;
    QString fullPath;
};

Q_DECLARE_METATYPE(FileItem)
Q_DECLARE_METATYPE(QList<FileItem>)

class FileProcessor;
class Md5Calculator;

class MainWindow : public QMainWindow
{
    Q_OBJECT

public:
    explicit MainWindow(QWidget *parent = nullptr);
    ~MainWindow();

protected:
    void dragEnterEvent(QDragEnterEvent *event) override;
    void dropEvent(QDropEvent *event) override;

private slots:
    void onBrowseClicked();
    void onStartClicked();
    void onSelectAllClicked(bool checked);
    void onCheckBoxClicked();
    void onFileProcessed(const QString &filePath, const QString &status,
                         const QString &newMd5, const QString &error);
    void onProcessingFinished();
    void onMd5Calculated(int index, const QString &md5);
    void onMd5CalculationFinished();

private:
    void setupUi();
    void loadPaths(const QStringList &paths);
    void addFileItem(const QFileInfo &info);
    void createFileListItem(int row, const FileItem &item);
    void updateFileListItem(int row);
    void startModify(const QList<FileItem> &selected);
    void updateButtonState();

    QLineEdit *pathEdit;
    QPushButton *browseButton;
    QPushButton *startButton;
    QCheckBox *createBackupCheck;
    QCheckBox *selectAllCheck;
    QTableWidget *fileListWidget;
    QProgressBar *progressBar;
    QLabel *statusLabel;
    QLabel *fileCountLabel;
    QLabel *md5ProgressLabel;
    
    QThread *workerThread;
    FileProcessor *worker;
    Md5Calculator *md5Calculator;
    int processedCount;
    int totalCount;
    int successCount;
    int failCount;
    int md5CalculatedCount;
    
    QList<FileItem> fileItems;
    QMap<int, QCheckBox*> checkBoxes;
    
    bool m_paused;
};

class FileProcessor : public QObject
{
    Q_OBJECT

public:
    explicit FileProcessor(bool createBackup);
    void pause();
    void resume();

public slots:
    void processFiles(const QList<FileItem> &items);

signals:
    void fileProcessed(const QString &filePath, const QString &status,
                       const QString &newMd5, const QString &error);
    void finished();

private:
    bool m_createBackup;
    bool m_paused;
    QMutex m_mutex;
    QWaitCondition m_waitCondition;
};

class Md5Calculator : public QThread
{
    Q_OBJECT

public:
    explicit Md5Calculator(QObject *parent = nullptr);
    void setFiles(const QList<FileItem> &files);
    void stop();

signals:
    void md5Calculated(int index, const QString &md5);
    void finished();

protected:
    void run() override;

private:
    QList<FileItem> m_files;
    bool m_running;
    QMutex m_mutex;
};

#endif