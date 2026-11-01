# 视频 MD5 修改工具

一个基于 Qt6 + C++17 开发的 Windows 桌面应用程序，专门用于批量修改视频文件的 MD5 哈希值。

## 下载地址

**夸克网盘**：<a href="https://pan.quark.cn/s/31e3824fdf09" target="_blank"><https://pan.quark.cn/s/31e3824fdf09></a>

## 功能特性

- **批量处理**：支持同时修改多个视频文件的 MD5
- **拖拽支持**：可直接拖拽文件或文件夹到窗口
- **自动备份**：可选创建备份文件到 `Bak` 文件夹
- **实时进度**：显示处理进度和成功/失败数量
- **暂停/继续**：支持暂停和继续处理
- **MD5 显示**：自动计算并显示原始 MD5 和新 MD5
- **自动过滤**：非视频文件自动过滤，不显示在列表中
- **高清图标**：支持 Windows 11 高分屏显示
- **表格视图**：使用 QTableWidget 显示文件列表，支持拖拽调整列宽
- **全选功能**：一键全选/取消全选所有文件

## 支持的视频格式

| 格式           | 说明               |
| ------------ | ---------------- |
| .mp4         | MPEG-4 视频        |
| .mkv         | Matroska 视频      |
| .avi         | AVI 视频           |
| .mov         | QuickTime 视频     |
| .wmv         | Windows Media 视频 |
| .flv         | Flash 视频         |
| .mpg / .mpeg | MPEG 视频          |
| .webm        | WebM 视频          |
| .m4v         | MPEG-4 视频        |
| .ts          | MPEG 传输流         |
| .m2ts        | Blu-ray 视频       |

> **注意**：非视频文件会自动过滤，不会显示在列表中。

## 技术原理

### MD5 修改原理

在视频文件末尾追加 4 字节随机数据，播放器会自动忽略这些数据，不影响视频播放。

### 文件写入

使用 Windows API 直接写入文件：

- `CreateFileW` - 打开文件
- `WriteFile` - 写入数据
- `FlushFileBuffers` - 确保数据写入磁盘

### 暂停功能

使用 `QWaitCondition` 实现真正的线程暂停，确保线程安全。

## 编译说明

### 环境要求

- Qt 6.x
- C++17
- MinGW-w64 (MSYS2)
- Windows 系统

### 编译步骤

**方式一：使用批处理脚本**

```bash
build.bat
```

**方式二：使用 qmake**

```bash
qmake MD5Modifier.pro
mingw32-make
```

**方式三：使用 Qt Creator**

1. 打开 `MD5Modifier.pro`
2. 配置编译套件
3. 点击构建

## 使用说明

1. **添加文件**：拖拽文件或文件夹到窗口，或点击"浏览"按钮选择
2. **选择文件**：勾选需要修改的视频文件，或使用"全选"复选框一键全选
3. **备份选项**：勾选"创建备份"以保留原始文件
4. **开始修改**：点击"开始修改"按钮
5. **暂停/继续**：处理过程中可点击"暂停修改"暂停，再点击"继续修改"继续
6. **查看结果**：处理完成后查看成功/失败数量

## 注意事项

1. **建议备份**：建议重要文件开启备份功能
2. **文件大小**：修改后文件会增加 4 字节
3. **多次修改**：每次修改都会追加新数据，文件会逐渐变大
4. **MD5 变化**：每次修改后的 MD5 都是随机的，无法预测
5. **视频播放**：修改后的视频文件可正常播放

## 项目结构

```
MD5Modifier/
├── src/                      # 源代码
│   ├── main.cpp             # 程序入口
│   ├── MainWindow.h         # 主窗口头文件
│   ├── MainWindow.cpp       # 主窗口实现
│   ├── FileMd5Strategy.h    # MD5 修改策略头文件
│   ├── FileMd5Strategy.cpp  # MD5 修改策略实现
│   ├── ChinesePathHelper.h  # 中文路径处理工具
│   ├── CustomCheckBox.h     # 自定义复选框
│   └── create_icon_qt.cpp   # 图标生成工具
├── assets/                   # 资源文件
│   ├── APP.png              # 原始图标图片
│   └── app.ico              # 程序图标（多尺寸）
├── MD5Modifier.pro          # Qt 项目文件
├── build.bat                # 编译脚本
├── ico_qt.bat               # 图标生成脚本
└── LICENSE                  # 开源协议
```

## 开源技术

本项目基于以下开源技术开发：

### 核心框架

| 技术                                      | 说明              | 许可证       |
| --------------------------------------- | --------------- | --------- |
| [Qt 6](https://www.qt.io/)              | 跨平台 C++ 图形界面框架  | LGPL v3   |
| [MinGW-w64](https://www.mingw-w64.org/) | Windows GCC 编译器 | ZPL / MIT |

### Qt 模块

| 模块           | 说明     |
| ------------ | ------ |
| QtCore       | 核心非图形类 |
| QtWidgets    | 图形界面组件 |
| QtGui        | 图形界面基础 |
| QtConcurrent | 多线程并发  |

## 许可证

本项目采用 [MIT License](LICENSE) 开源协议。

## 作者

BY：对酒当歌分享
