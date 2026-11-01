#include "MainWindow.h"
#include "FileMd5Strategy.h"
#include "CustomCheckBox.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLineEdit>
#include <QPushButton>
#include <QCheckBox>
#include <QTableWidget>
#include <QProgressBar>
#include <QLabel>
#include <QHeaderView>
#include <QDragEnterEvent>
#include <QDropEvent>
#include <QMimeData>
#include <QFileDialog>
#include <QDir>
#include <QMessageBox>
#include <QDateTime>
#include <QFile>
#include <QTextStream>
#include <QCoreApplication>
#include <QTableWidgetItem>
#include <QFontMetrics>
#include <QMouseEvent>
#include <QTimer>
#include <QThreadPool>
#include <QApplication>
#include <QIcon>

FileProcessor::FileProcessor(bool createBackup)
    : m_createBackup(createBackup)
    , m_paused(false)
{
}

void FileProcessor::pause()
{
    QMutexLocker locker(&m_mutex);
    m_paused = true;
}

void FileProcessor::resume()
{
    QMutexLocker locker(&m_mutex);
    m_paused = false;
    m_waitCondition.wakeAll();
}

void FileProcessor::processFiles(const QList<FileItem> &items)
{
    qDebug() << "processFiles called with" << items.size() << "items";
    
    for (const auto &item : items) {
        {
            QMutexLocker locker(&m_mutex);
            while (m_paused) {
                m_waitCondition.wait(&m_mutex);
            }
        }
        
        qDebug() << "Processing file:" << item.fullPath;
        qDebug() << "  selected:" << item.selected;
        qDebug() << "  suffix:" << item.suffix;
        
        QString newMd5, backupPath, error;
        bool ok = FileMd5Strategy::modifyFile(item.fullPath, m_createBackup,
                                               newMd5, backupPath, error);
        
        qDebug() << "  result:" << ok << "newMd5:" << newMd5 << "error:" << error;
        
        if (ok) {
            emit fileProcessed(item.fullPath, QString::fromLocal8Bit("成功"), newMd5, QString());
        } else {
            emit fileProcessed(item.fullPath, QString::fromLocal8Bit("失败"), QString(), error);
        }
    }
    emit finished();
}

Md5Calculator::Md5Calculator(QObject *parent)
    : QThread(parent)
    , m_running(true)
{
}

void Md5Calculator::setFiles(const QList<FileItem> &files)
{
    QMutexLocker locker(&m_mutex);
    m_files = files;
}

void Md5Calculator::stop()
{
    QMutexLocker locker(&m_mutex);
    m_running = false;
}

void Md5Calculator::run()
{
    for (int i = 0; i < m_files.size(); ++i) {
        QMutexLocker locker(&m_mutex);
        if (!m_running) {
            break;
        }
        locker.unlock();
        
        QString md5 = FileMd5Strategy::calculateMd5(m_files[i].fullPath);
        emit md5Calculated(i, md5);
    }
    emit finished();
}

MainWindow::MainWindow(QWidget *parent)
    : QMainWindow(parent)
    , pathEdit(nullptr)
    , browseButton(nullptr)
    , startButton(nullptr)
    , createBackupCheck(nullptr)
    , selectAllCheck(nullptr)
    , fileListWidget(nullptr)
    , progressBar(nullptr)
    , statusLabel(nullptr)
    , fileCountLabel(nullptr)
    , md5ProgressLabel(nullptr)
    , workerThread(nullptr)
    , worker(nullptr)
    , md5Calculator(nullptr)
    , processedCount(0)
    , totalCount(0)
    , successCount(0)
    , failCount(0)
    , m_paused(false)
{
    setupUi();
    setAcceptDrops(true);
    
    QThreadPool::globalInstance()->setMaxThreadCount(4);
}

MainWindow::~MainWindow()
{
    if (workerThread) {
        workerThread->quit();
        workerThread->wait();
        delete workerThread;
    }
    if (md5Calculator) {
        md5Calculator->stop();
        md5Calculator->wait();
        delete md5Calculator;
    }
}

void MainWindow::updateButtonState()
{
    if (m_paused) {
        startButton->setText(QString::fromLocal8Bit("继续修改"));
        startButton->setStyleSheet(
            "QPushButton { background-color: #E6A23C; color: white; border-radius: 6px; padding: 8px; font-weight: bold; }"
            "QPushButton:hover { background-color: #f3b95f; }"
        );
    } else if (workerThread && workerThread->isRunning()) {
        startButton->setText(QString::fromLocal8Bit("暂停修改"));
        startButton->setStyleSheet(
            "QPushButton { background-color: #E6A23C; color: white; border-radius: 6px; padding: 8px; font-weight: bold; }"
            "QPushButton:hover { background-color: #f3b95f; }"
        );
    } else {
        startButton->setText(QString::fromLocal8Bit("开始修改"));
        startButton->setStyleSheet(
            "QPushButton { background-color: #67C23A; color: white; border-radius: 6px; padding: 8px; font-weight: bold; }"
            "QPushButton:hover { background-color: #85ce61; }"
        );
    }
}

void MainWindow::setupUi()
{
    QWidget *central = new QWidget(this);
    QVBoxLayout *mainLayout = new QVBoxLayout(central);
    mainLayout->setSpacing(10);
    mainLayout->setContentsMargins(10, 10, 10, 10);

    QHBoxLayout *topLayout = new QHBoxLayout();
    
    QLabel *pathLabel = new QLabel(QString::fromLocal8Bit("路径源："));
    topLayout->addWidget(pathLabel);
    
    pathEdit = new QLineEdit();
    pathEdit->setPlaceholderText(QString::fromLocal8Bit("拖拽文件/文件夹到此处，或点击浏览选择"));
    pathEdit->setReadOnly(true);
    pathEdit->setStyleSheet("QLineEdit { padding: 5px; background-color: #F5F7FA; border: 1px solid #E5E6EB; border-radius: 4px; }");
    
    browseButton = new QPushButton(QString::fromLocal8Bit("浏览"));
    browseButton->setFixedWidth(80);
    browseButton->setStyleSheet(
        "QPushButton { background-color: #409EFF; color: white; border-radius: 6px; padding: 8px; font-weight: bold; }"
        "QPushButton:hover { background-color: #66b1ff; }"
    );
    
    startButton = new QPushButton(QString::fromLocal8Bit("开始修改"));
    startButton->setFixedWidth(100);
    startButton->setStyleSheet(
        "QPushButton { background-color: #67C23A; color: white; border-radius: 6px; padding: 8px; font-weight: bold; }"
        "QPushButton:hover { background-color: #85ce61; }"
    );
    
    topLayout->addWidget(pathEdit);
    topLayout->addWidget(browseButton);
    topLayout->addWidget(startButton);
    mainLayout->addLayout(topLayout);

    QHBoxLayout *optionLayout = new QHBoxLayout();
    
    QHBoxLayout *leftOptionLayout = new QHBoxLayout();
    leftOptionLayout->addSpacing(15);
    selectAllCheck = new CustomCheckBox(QString::fromLocal8Bit("全选"));
    selectAllCheck->setChecked(true);
    connect(selectAllCheck, &QCheckBox::clicked, this, &MainWindow::onSelectAllClicked);
    leftOptionLayout->addWidget(selectAllCheck);
    leftOptionLayout->addSpacing(20);
    createBackupCheck = new CustomCheckBox(QString::fromLocal8Bit("创建备份"));
    leftOptionLayout->addWidget(createBackupCheck);
    leftOptionLayout->addStretch();
    
    QHBoxLayout *rightOptionLayout = new QHBoxLayout();
    
    md5ProgressLabel = new QLabel(QString::fromLocal8Bit(""));
    md5ProgressLabel->setStyleSheet("QLabel { color: #409EFF; padding: 5px; }");
    md5ProgressLabel->setAlignment(Qt::AlignRight | Qt::AlignVCenter);
    
    fileCountLabel = new QLabel(QString::fromLocal8Bit("文件数: 0"));
    fileCountLabel->setStyleSheet("QLabel { color: #606266; padding: 5px; }");
    fileCountLabel->setAlignment(Qt::AlignRight | Qt::AlignVCenter);
    
    rightOptionLayout->addWidget(md5ProgressLabel);
    rightOptionLayout->addWidget(fileCountLabel);
    
    optionLayout->addLayout(leftOptionLayout);
    optionLayout->addStretch();
    optionLayout->addLayout(rightOptionLayout);
    mainLayout->addLayout(optionLayout);

    // 创建表格
    fileListWidget = new QTableWidget();
    fileListWidget->setColumnCount(7);
    fileListWidget->setHorizontalHeaderLabels(QStringList() 
        << QString::fromLocal8Bit("选择")
        << QString::fromLocal8Bit("文件名称")
        << QString::fromLocal8Bit("后缀")
        << QString::fromLocal8Bit("大小")
        << QString::fromLocal8Bit("原始MD5")
        << QString::fromLocal8Bit("新MD5")
        << QString::fromLocal8Bit("状态"));
    
    fileListWidget->horizontalHeader()->setStretchLastSection(true);
    fileListWidget->setSelectionBehavior(QAbstractItemView::SelectRows);
    fileListWidget->setSelectionMode(QAbstractItemView::NoSelection);
    fileListWidget->setEditTriggers(QAbstractItemView::NoEditTriggers);
    fileListWidget->verticalHeader()->setVisible(false);
    fileListWidget->setShowGrid(false);
    fileListWidget->setAlternatingRowColors(false);
    
    // 设置所有列可拖拽调整
    for (int i = 0; i < 7; i++) {
        fileListWidget->horizontalHeader()->setSectionResizeMode(i, QHeaderView::Interactive);
    }
    
    // 设置默认列宽
    fileListWidget->setColumnWidth(0, 45);   // 选择
    fileListWidget->setColumnWidth(1, 360);  // 文件名称
    fileListWidget->setColumnWidth(2, 65);   // 后缀
    fileListWidget->setColumnWidth(3, 80);   // 大小
    fileListWidget->setColumnWidth(4, 260);  // 原始MD5
    fileListWidget->setColumnWidth(5, 260);  // 新MD5
    fileListWidget->setColumnWidth(6, 80);   // 状态
    
    fileListWidget->setStyleSheet(
        "QTableWidget { border: 1px solid #E5E6EB; border-top: none; background-color: white; }"
        "QTableWidget::item { padding: 5px; border-bottom: 1px solid #E5E6EB; }"
        "QTableWidget::item:hover { background-color: #B0E0E6; }"
        "QHeaderView::section { background-color: #E5E6EB; padding: 8px; border: none; border-right: 1px solid #D0D1D5; border-bottom: 1px solid #E5E6EB; font-weight: bold; color: #303133; }"
        "QHeaderView::section:hover { background-color: #D5D6DA; }"
    );
    
    mainLayout->addWidget(fileListWidget);

    QHBoxLayout *statusLayout = new QHBoxLayout();
    progressBar = new QProgressBar();
    progressBar->setVisible(false);
    progressBar->setStyleSheet("QProgressBar { border: 1px solid #E5E6EB; border-radius: 4px; text-align: center; }"
                               "QProgressBar::chunk { background-color: #67C23A; border-radius: 4px; }");
    
    statusLabel = new QLabel(QString::fromLocal8Bit("就绪"));
    statusLabel->setAlignment(Qt::AlignRight | Qt::AlignVCenter);
    statusLabel->setStyleSheet("QLabel { color: #606266; padding: 5px; }");
    
    statusLayout->addWidget(progressBar);
    statusLayout->addWidget(statusLabel);
    mainLayout->addLayout(statusLayout);

    setCentralWidget(central);
    
    setMinimumSize(800, 400);
    resize(1200, 600);
    setWindowTitle(QString::fromLocal8Bit("视频 MD5 修改工具"));
    setWindowIcon(QIcon("assets/app.ico"));
    
    connect(browseButton, &QPushButton::clicked, this, &MainWindow::onBrowseClicked);
    connect(startButton, &QPushButton::clicked, this, &MainWindow::onStartClicked);
}

void MainWindow::dragEnterEvent(QDragEnterEvent *event)
{
    if (event->mimeData()->hasUrls()) {
        event->acceptProposedAction();
    }
}

void MainWindow::dropEvent(QDropEvent *event)
{
    const QList<QUrl> urls = event->mimeData()->urls();
    if (!urls.isEmpty()) {
        QStringList paths;
        for (const QUrl &url : urls) {
            QString path = url.toLocalFile();
            QFileInfo info(path);
            if (info.exists()) {
                paths.append(path);
            }
        }
        if (!paths.isEmpty()) {
            loadPaths(paths);
        }
    }
}

void MainWindow::onBrowseClicked()
{
    QFileDialog dialog(this);
    dialog.setFileMode(QFileDialog::Directory);
    dialog.setOption(QFileDialog::ShowDirsOnly, false);
    dialog.setWindowTitle(QString::fromLocal8Bit("选择文件夹"));
    
    if (dialog.exec()) {
        QStringList folders = dialog.selectedFiles();
        if (!folders.isEmpty()) {
            loadPaths(folders);
        }
    }
}

void MainWindow::onStartClicked()
{
    if (!workerThread || !workerThread->isRunning()) {
        // 还没有开始处理，开始处理
        QList<FileItem> selectedItems;
        for (int i = 0; i < fileItems.size(); ++i) {
            if (fileItems[i].selected) {
                selectedItems.append(fileItems[i]);
            }
        }
        
        if (selectedItems.isEmpty()) {
            QMessageBox::warning(this, QString::fromLocal8Bit("提示"), 
                               QString::fromLocal8Bit("请先选择要修改的文件。"));
            return;
        }
        
        int ret = QMessageBox::question(this, QString::fromLocal8Bit("确认"), 
                                        QString::fromLocal8Bit("确定要修改选中的 %1 个文件吗？").arg(selectedItems.size()),
                                        QMessageBox::Yes | QMessageBox::No);
        if (ret == QMessageBox::Yes) {
            successCount = 0;
            failCount = 0;
            m_paused = false;
            
            startModify(selectedItems);
            updateButtonState();
        }
    } else {
        // 已经在处理中，切换暂停/继续
        m_paused = !m_paused;
        if (m_paused) {
            worker->pause();
            statusLabel->setText(QString::fromLocal8Bit("已暂停"));
        } else {
            worker->resume();
            statusLabel->setText(QString::fromLocal8Bit("继续处理..."));
        }
        updateButtonState();
    }
}

void MainWindow::onSelectAllClicked(bool checked)
{
    for (int i = 0; i < fileItems.size(); ++i) {
        fileItems[i].selected = checked;
        if (checkBoxes.contains(i)) {
            checkBoxes[i]->blockSignals(true);
            checkBoxes[i]->setChecked(checked);
            checkBoxes[i]->blockSignals(false);
        }
    }
}

void MainWindow::onCheckBoxClicked()
{
    QCheckBox *checkBox = qobject_cast<QCheckBox*>(sender());
    if (checkBox) {
        int row = checkBox->property("row").toInt();
        if (row >= 0 && row < fileItems.size()) {
            fileItems[row].selected = checkBox->isChecked();
            
            // 检查是否所有文件都被选中
            bool allSelected = true;
            for (const auto &item : fileItems) {
                if (!item.selected) {
                    allSelected = false;
                    break;
                }
            }
            
            // 更新全选复选框状态
            if (selectAllCheck) {
                selectAllCheck->blockSignals(true);
                selectAllCheck->setChecked(allSelected);
                selectAllCheck->blockSignals(false);
            }
        }
    }
}

void MainWindow::loadPaths(const QStringList &paths)
{
    if (md5Calculator) {
        md5Calculator->stop();
        md5Calculator->wait();
        delete md5Calculator;
        md5Calculator = nullptr;
    }
    
    fileItems.clear();
    fileListWidget->setRowCount(0);
    checkBoxes.clear();
    md5CalculatedCount = 0;
    m_paused = false;
    updateButtonState();
    
    QStringList folderPaths;
    QStringList filePaths;
    
    for (const QString &path : paths) {
        QFileInfo info(path);
        if (info.isDir()) {
            folderPaths.append(path);
        } else if (info.isFile()) {
            filePaths.append(path);
        }
    }
    
    for (const QString &folderPath : folderPaths) {
        QDir dir(folderPath);
        dir.setFilter(QDir::Files | QDir::NoDotAndDotDot | QDir::NoSymLinks);
        dir.setSorting(QDir::Name);
        
        QFileInfoList fileInfoList = dir.entryInfoList();
        for (const QFileInfo &info : fileInfoList) {
            addFileItem(info);
        }
    }
    
    for (const QString &filePath : filePaths) {
        QFileInfo info(filePath);
        addFileItem(info);
    }
    
    QString displayPath;
    if (folderPaths.size() == 1 && filePaths.isEmpty()) {
        displayPath = folderPaths.first();
    } else if (folderPaths.isEmpty() && filePaths.size() == 1) {
        displayPath = filePaths.first();
    } else if (folderPaths.size() > 0 || filePaths.size() > 0) {
        displayPath = QString::fromLocal8Bit("已选择 %1 个文件夹和 %2 个文件")
                        .arg(folderPaths.size()).arg(filePaths.size());
    }
    pathEdit->setText(displayPath);
    
    for (int i = 0; i < fileItems.size(); ++i) {
        createFileListItem(i, fileItems[i]);
    }
    
    fileCountLabel->setText(QString::fromLocal8Bit("文件数: %1").arg(fileItems.size()));
    fileCountLabel->setAlignment(Qt::AlignRight | Qt::AlignVCenter);
    
    if (fileItems.size() > 0) {
        md5ProgressLabel->setText(QString::fromLocal8Bit("正在计算MD5: 0/%1").arg(fileItems.size()));
        
        md5Calculator = new Md5Calculator(this);
        md5Calculator->setFiles(fileItems);
        connect(md5Calculator, &Md5Calculator::md5Calculated, this, &MainWindow::onMd5Calculated);
        connect(md5Calculator, &Md5Calculator::finished, this, &MainWindow::onMd5CalculationFinished);
        md5Calculator->start();
    }
}

void MainWindow::addFileItem(const QFileInfo &info)
{
    QString suffix = info.suffix().toLower();
    
    QStringList videoFormats = {"mp4", "mkv", "avi", "mov", "wmv", "flv", 
                                "mpg", "mpeg", "webm", "m4v", "ts", "m2ts"};
    
    if (!videoFormats.contains(suffix)) {
        return;
    }
    
    FileItem item;
    item.selected = true;
    item.fileName = info.fileName();
    item.suffix = info.suffix();
    item.size = info.size();
    item.originalMd5 = QString::fromLocal8Bit("计算中...");
    item.newMd5 = "";
    item.status = QString::fromLocal8Bit("等待处理");
    item.fullPath = info.filePath();
    
    fileItems.append(item);
}

void MainWindow::createFileListItem(int row, const FileItem &item)
{
    fileListWidget->insertRow(row);
    fileListWidget->setRowHeight(row, 35);
    
    // 选择列 - 复选框
    CustomCheckBox *checkBox = new CustomCheckBox();
    checkBox->setChecked(item.selected);
    checkBox->setProperty("row", row);
    connect(checkBox, &QCheckBox::clicked, this, &MainWindow::onCheckBoxClicked);
    QWidget *checkWidget = new QWidget();
    QHBoxLayout *checkLayout = new QHBoxLayout(checkWidget);
    checkLayout->addWidget(checkBox);
    checkLayout->setAlignment(Qt::AlignCenter);
    checkLayout->setContentsMargins(0, 0, 0, 0);
    fileListWidget->setCellWidget(row, 0, checkWidget);
    checkBoxes[row] = checkBox;
    
    // 文件名称
    QTableWidgetItem *nameItem = new QTableWidgetItem(item.fileName);
    nameItem->setTextAlignment(Qt::AlignCenter | Qt::AlignVCenter);
    nameItem->setToolTip(item.fileName);
    fileListWidget->setItem(row, 1, nameItem);
    
    // 后缀
    QTableWidgetItem *suffixItem = new QTableWidgetItem(item.suffix);
    suffixItem->setTextAlignment(Qt::AlignCenter | Qt::AlignVCenter);
    suffixItem->setForeground(QColor("#909399"));
    fileListWidget->setItem(row, 2, suffixItem);
    
    // 大小
    QString sizeStr = QLocale::system().formattedDataSize(item.size);
    QTableWidgetItem *sizeItem = new QTableWidgetItem(sizeStr);
    sizeItem->setTextAlignment(Qt::AlignCenter | Qt::AlignVCenter);
    fileListWidget->setItem(row, 3, sizeItem);
    
    // 原始MD5
    QString originalMd5Text = item.originalMd5;
    if (originalMd5Text.length() > 24) {
        originalMd5Text = originalMd5Text.left(24);
    }
    QTableWidgetItem *originalMd5Item = new QTableWidgetItem(originalMd5Text);
    originalMd5Item->setTextAlignment(Qt::AlignCenter | Qt::AlignVCenter);
    originalMd5Item->setForeground(QColor("#909399"));
    originalMd5Item->setToolTip(item.originalMd5);
    fileListWidget->setItem(row, 4, originalMd5Item);
    
    // 新MD5
    QTableWidgetItem *newMd5Item = new QTableWidgetItem("");
    newMd5Item->setTextAlignment(Qt::AlignCenter | Qt::AlignVCenter);
    newMd5Item->setForeground(QColor("#909399"));
    fileListWidget->setItem(row, 5, newMd5Item);
    
    // 状态
    QTableWidgetItem *statusItem = new QTableWidgetItem(item.status);
    statusItem->setTextAlignment(Qt::AlignCenter | Qt::AlignVCenter);
    statusItem->setForeground(QColor("#909399"));
    fileListWidget->setItem(row, 6, statusItem);
}

void MainWindow::updateFileListItem(int row)
{
    if (row < 0 || row >= fileItems.size()) return;
    
    // 更新新MD5
    QString newMd5 = fileItems[row].newMd5;
    if (!newMd5.isEmpty()) {
        QString displayMd5 = newMd5;
        if (displayMd5.length() > 24) {
            displayMd5 = displayMd5.left(24);
        }
        QTableWidgetItem *newMd5Item = fileListWidget->item(row, 5);
        if (newMd5Item) {
            newMd5Item->setText(displayMd5);
            newMd5Item->setForeground(QColor("#67C23A"));
            newMd5Item->setToolTip(newMd5);
        }
    }
    
    // 更新状态
    QTableWidgetItem *statusItem = fileListWidget->item(row, 6);
    if (statusItem) {
        statusItem->setText(fileItems[row].status);
        if (fileItems[row].status == QString::fromLocal8Bit("成功")) {
            statusItem->setForeground(QColor("#67C23A"));
            QFont font = statusItem->font();
            font.setBold(true);
            statusItem->setFont(font);
        } else if (fileItems[row].status == QString::fromLocal8Bit("失败")) {
            statusItem->setForeground(QColor("#F56C6C"));
            QFont font = statusItem->font();
            font.setBold(true);
            statusItem->setFont(font);
        }
    }
    
    // 更新原始MD5
    QString originalMd5 = fileItems[row].originalMd5;
    if (!originalMd5.isEmpty() && originalMd5 != QString::fromLocal8Bit("计算中...")) {
        QString displayMd5 = originalMd5;
        if (displayMd5.length() > 24) {
            displayMd5 = displayMd5.left(24);
        }
        QTableWidgetItem *originalMd5Item = fileListWidget->item(row, 4);
        if (originalMd5Item) {
            originalMd5Item->setText(displayMd5);
            originalMd5Item->setToolTip(originalMd5);
        }
    }
}

void MainWindow::startModify(const QList<FileItem> &selected)
{
    totalCount = selected.size();
    processedCount = 0;
    successCount = 0;
    failCount = 0;
    
    progressBar->setVisible(true);
    progressBar->setRange(0, totalCount);
    progressBar->setValue(0);
    statusLabel->setText(QString::fromLocal8Bit("正在处理: 0/%1 | 成功: 0 | 失败: 0").arg(totalCount));
    
    browseButton->setEnabled(false);
    
    workerThread = new QThread(this);
    worker = new FileProcessor(createBackupCheck->isChecked());
    worker->moveToThread(workerThread);
    
    connect(workerThread, &QThread::finished, worker, &QObject::deleteLater);
    connect(worker, &FileProcessor::fileProcessed, this, &MainWindow::onFileProcessed);
    connect(worker, &FileProcessor::finished, this, &MainWindow::onProcessingFinished);
    
    workerThread->start();
    QMetaObject::invokeMethod(worker, "processFiles", Qt::QueuedConnection,
                              Q_ARG(QList<FileItem>, selected));
    
    updateButtonState();
}

void MainWindow::onFileProcessed(const QString &filePath, const QString &status,
                                 const QString &newMd5, const QString &error)
{
    Q_UNUSED(error);
    
    // 如果暂停了，就停止处理
    if (m_paused) {
        return;
    }
    
    for (int i = 0; i < fileItems.size(); ++i) {
        if (fileItems[i].fullPath == filePath) {
            fileItems[i].status = QString::fromLocal8Bit("已处理");
            fileItems[i].newMd5 = newMd5;
            
            updateFileListItem(i);
            
            if (status == QString::fromLocal8Bit("成功")) {
                successCount++;
            } else {
                failCount++;
            }
            break;
        }
    }
    
    processedCount++;
    progressBar->setValue(processedCount);
    statusLabel->setText(QString::fromLocal8Bit("正在处理: %1/%2 | 成功: %3 | 失败: %4")
                        .arg(processedCount).arg(totalCount).arg(successCount).arg(failCount));
    
    QApplication::processEvents();
}

void MainWindow::onProcessingFinished()
{
    progressBar->setVisible(false);
    browseButton->setEnabled(true);
    m_paused = false;
    
    workerThread->quit();
    workerThread->wait();
    delete workerThread;
    workerThread = nullptr;
    
    updateButtonState();
    
    QString resultText = QString::fromLocal8Bit("处理完成 | 成功: %1 | 失败: %2 | 共 %3 个文件")
                    .arg(successCount).arg(failCount).arg(totalCount);
    
    statusLabel->setText(resultText);
    
    QMessageBox::information(this, QString::fromLocal8Bit("完成"), 
                            QString::fromLocal8Bit("处理完成\n成功: %1 个文件\n失败: %2 个文件")
                            .arg(successCount).arg(failCount));
}

void MainWindow::onMd5Calculated(int index, const QString &md5)
{
    if (index >= 0 && index < fileItems.size()) {
        fileItems[index].originalMd5 = md5;
        
        QTableWidgetItem *originalMd5Item = fileListWidget->item(index, 4);
        if (originalMd5Item) {
            QString displayMd5 = md5;
            if (displayMd5.length() > 24) {
                displayMd5 = displayMd5.left(24);
            }
            originalMd5Item->setText(displayMd5);
            originalMd5Item->setToolTip(md5);
        }
        
        md5CalculatedCount++;
        md5ProgressLabel->setText(QString::fromLocal8Bit("正在计算MD5: %1/%2")
                                 .arg(md5CalculatedCount).arg(fileItems.size()));
        
        QApplication::processEvents();
    }
}

void MainWindow::onMd5CalculationFinished()
{
    md5ProgressLabel->setText(QString::fromLocal8Bit("MD5计算完成"));
    QTimer::singleShot(2000, [this]() {
        md5ProgressLabel->setText(QString::fromLocal8Bit(""));
    });
}