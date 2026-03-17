#ifndef CUSTOMCHECKBOX_H
#define CUSTOMCHECKBOX_H

#include <QCheckBox>
#include <QPainter>
#include <QPainterPath>
#include <QStyleOptionButton>

class CustomCheckBox : public QCheckBox
{
public:
    explicit CustomCheckBox(QWidget *parent = nullptr) : QCheckBox(parent) {}
    explicit CustomCheckBox(const QString &text, QWidget *parent = nullptr) : QCheckBox(text, parent) {}
    
protected:
    void paintEvent(QPaintEvent *event) override
    {
        Q_UNUSED(event);
        
        QPainter painter(this);
        painter.setRenderHint(QPainter::Antialiasing, false);
        
        int size = 13;
        int x = 0;
        int y = (height() - size) / 2;
        
        QRect boxRect(x, y, size - 1, size - 1);
        
        // 画正方形边框（灰色，无阴影）
        painter.setPen(QPen(QColor(153, 153, 153), 1));
        painter.setBrush(Qt::NoBrush);
        painter.drawRect(boxRect);
        
        // 如果选中，画对勾（灰黑色）
        if (isChecked()) {
            painter.setRenderHint(QPainter::Antialiasing, true);
            painter.setPen(QPen(QColor(80, 80, 80), 1.5, Qt::SolidLine, Qt::RoundCap, Qt::RoundJoin));
            
            // 更美观的对勾
            QPainterPath checkPath;
            checkPath.moveTo(boxRect.left() + 2.5, boxRect.top() + 6);
            checkPath.lineTo(boxRect.left() + 5.5, boxRect.bottom() - 2.5);
            checkPath.lineTo(boxRect.right() - 2, boxRect.top() + 3);
            
            painter.drawPath(checkPath);
        }
        
        // 绘制文字
        if (!text().isEmpty()) {
            QRect textRect = rect();
            textRect.setLeft(boxRect.right() + 6);
            painter.setFont(font());
            painter.setPen(QPen(palette().text().color(), 1));
            painter.drawText(textRect, Qt::AlignVCenter | Qt::TextSingleLine, text());
        }
    }
    
    QSize sizeHint() const override
    {
        if (!text().isEmpty()) {
            return QSize(13 + 6 + fontMetrics().horizontalAdvance(text()), 13);
        }
        return QSize(13, 13);
    }
};

#endif
