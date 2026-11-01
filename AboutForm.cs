using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MD5Modifier
{
    /// <summary>
    /// 关于对话框：展示软件介绍、作者、项目地址、下载地址与防失联页（超链接）。
    /// 样式参考作者其他工具的“关于”界面，链接直接显示网址并可点击打开。
    /// </summary>
    public class AboutForm : Form
    {
        private const string AppName = "视频 MD5 修改工具";
        private const string AppDesc = "批量修改视频文件的 MD5 值";
        private const string Author = "对酒当歌分享";
        private const string SiteUrl = "https://www.haozy.top";
        private const string AntiLossUrl = "https://docs.qq.com/aio/DY2FqUFRrWXpxaUJY";
        private const string QuarkUrl = "https://pan.quark.cn/s/280a143de78b";
        private const string GithubUrl = "https://github.com/aiyobudui/MD5Modifier";

        private PictureBox iconBox;

        public AboutForm()
        {
            Text = "关于";
            Font = new Font("Microsoft YaHei", 9.5F);
            BackColor = Color.White;
            ForeColor = Color.FromArgb(0x30, 0x31, 0x33);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 384);

            // 顶部应用图标
            iconBox = new PictureBox
            {
                Size = new Size(40, 40),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Location = new Point(18, 16)
            };
            Load += (s, e) =>
            {
                if (Icon != null) iconBox.Image = Icon.ToBitmap();
            };
            Controls.Add(iconBox);

            var title = new Label
            {
                Text = AppName,
                Font = new Font("Microsoft YaHei", 15F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0x30, 0x31, 0x33),
                AutoSize = true,
                Location = new Point(70, 18)
            };
            Controls.Add(title);
            var desc = new Label
            {
                Text = AppDesc,
                Font = new Font("Microsoft YaHei", 9F),
                ForeColor = Color.FromArgb(0x60, 0x62, 0x66),
                AutoSize = true,
                Location = new Point(70, 44)
            };
            Controls.Add(desc);

            // 内容区
            var content = new Panel
            {
                Location = new Point(0, 70),
                Size = new Size(440, 314),
                BackColor = Color.White
            };
            Controls.Add(content);

            int y = 8;
            y = AddHeading(content, "核心特色", y);
            y = AddBullet(content, "视频 MD5 修改：文件末尾追加随机数据，改变 MD5 而无需重新编码", y);
            y = AddBullet(content, "批量处理：支持多文件多线程，可暂停 / 继续", y);
            y = AddBullet(content, "超长路径：支持 \\?\\ 长路径与中文路径", y);
            y = AddBullet(content, "自动备份：可选创建独立备份目录", y);

            y = AddField(content, "作者：", Author, y, false);
            y += 4;
            y = AddHeading(content, "海量免费资源分享", y);
            y = AddField(content, "网站：", SiteUrl, y, true);
            y = AddField(content, "防失联：", AntiLossUrl, y, true);
            y += 4;
            y = AddField(content, "夸克：", QuarkUrl, y, true);
            y = AddField(content, "GitHub 仓库：", GithubUrl, y, true);
        }

        private static int AddHeading(Panel parent, string text, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Microsoft YaHei", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0x30, 0x31, 0x33),
                AutoSize = true,
                Location = new Point(18, y)
            };
            parent.Controls.Add(lbl);
            return y + lbl.Height + 6;
        }

        private static int AddBullet(Panel parent, string text, int y)
        {
            var lbl = new Label
            {
                Text = "• " + text,
                Font = new Font("Microsoft YaHei", 9.5F),
                ForeColor = Color.FromArgb(0x60, 0x62, 0x66),
                AutoSize = true,
                MaximumSize = new Size(404, 0),
                Location = new Point(28, y)
            };
            parent.Controls.Add(lbl);
            return y + lbl.Height + 4;
        }

        private static int AddField(Panel parent, string label, string value, int y, bool asLink)
        {
            var lbl = new Label
            {
                Text = label,
                Font = new Font("Microsoft YaHei", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0x30, 0x31, 0x33),
                AutoSize = true,
                Location = new Point(18, y)
            };
            parent.Controls.Add(lbl);
            int vx = 18 + lbl.Width + 4;

            int h;
            if (asLink)
            {
                var link = new LinkLabel
                {
                    Text = value,                 // 直接显示网址
                    Font = new Font("Microsoft YaHei", 9.5F),
                    LinkColor = Color.FromArgb(0x40, 0x9E, 0xFF),
                    ActiveLinkColor = Color.FromArgb(0x66, 0xB1, 0xFF),
                    VisitedLinkColor = Color.FromArgb(0x40, 0x9E, 0xFF),
                    AutoSize = true,
                    MaximumSize = new Size(360, 0),
                    Location = new Point(vx, y + 1)
                };
                link.Tag = value;
                link.LinkClicked += (s, e) => OpenUrl((string)((LinkLabel)s).Tag);
                parent.Controls.Add(link);
                h = Math.Max(lbl.Height, link.Height);
            }
            else
            {
                var val = new Label
                {
                    Text = value,
                    Font = new Font("Microsoft YaHei", 9.5F),
                    ForeColor = Color.FromArgb(0x30, 0x31, 0x33),
                    AutoSize = true,
                    Location = new Point(vx, y)
                };
                parent.Controls.Add(val);
                h = Math.Max(lbl.Height, val.Height);
            }
            return y + h + 6;
        }

        private static void OpenUrl(string url)
        {
            try
            {
                string full = url;
                if (!full.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    full = "https://" + full;
                Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
