using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace PocketDeck.Setup
{
    public class InstallerForm : Form
    {
        private TextBox txtPath;
        private CheckBox chkDesktop;
        private CheckBox chkStartMenu;
        private CheckBox chkLaunch;
        private Button btnBrowse;
        private Button btnInstall;
        private Button btnCancel;
        private ProgressBar progress;
        private Label lblStatus;

        public InstallerForm()
        {
            InitializeComponent();
            txtPath.Text = InstallEngine.DefaultInstallDir;
        }

        private void InitializeComponent()
        {
            BackColor = System.Drawing.Color.FromArgb(0x15, 0x15, 0x1d);
            ForeColor = System.Drawing.Color.FromArgb(0xf8, 0xf8, 0xf2);
            ClientSize = new System.Drawing.Size(480, 366);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "PocketDeck 安装程序";

            var title = new Label
            {
                Text = "PocketDeck",
                ForeColor = System.Drawing.Color.FromArgb(0xff, 0x53, 0x70),
                Font = new System.Drawing.Font("Microsoft YaHei UI", 16F, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(20, 16),
                AutoSize = true
            };
            var sub = new Label
            {
                Text = "SteamVR 手机浮窗 · 社区修改发行版",
                ForeColor = System.Drawing.Color.FromArgb(0xb4, 0xb4, 0xb4),
                Location = new System.Drawing.Point(22, 48),
                AutoSize = true
            };

            var lblPath = new Label
            {
                Text = "安装位置：",
                Location = new System.Drawing.Point(20, 96),
                AutoSize = true
            };
            txtPath = new TextBox
            {
                Location = new System.Drawing.Point(20, 120),
                Size = new System.Drawing.Size(340, 23),
                BackColor = System.Drawing.Color.FromArgb(0x25, 0x25, 0x2d),
                ForeColor = System.Drawing.Color.FromArgb(0xf8, 0xf8, 0xf2),
                BorderStyle = BorderStyle.FixedSingle
            };
            btnBrowse = new Button
            {
                Text = "浏览",
                Location = new System.Drawing.Point(372, 118),
                Size = new System.Drawing.Size(80, 27),
                BackColor = System.Drawing.Color.FromArgb(0x2d, 0x2d, 0x35),
                ForeColor = System.Drawing.Color.FromArgb(0xf8, 0xf8, 0xf2),
                FlatStyle = FlatStyle.Flat
            };
            btnBrowse.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(0x40, 0x40, 0x4a);
            btnBrowse.Click += BtnBrowse_Click;

            chkDesktop = new CheckBox
            {
                Text = "创建桌面快捷方式",
                Checked = true,
                Location = new System.Drawing.Point(22, 162),
                AutoSize = true
            };
            chkStartMenu = new CheckBox
            {
                Text = "创建开始菜单快捷方式",
                Checked = true,
                Location = new System.Drawing.Point(22, 190),
                AutoSize = true
            };
            chkLaunch = new CheckBox
            {
                Text = "安装完成后启动",
                Checked = true,
                Location = new System.Drawing.Point(22, 218),
                AutoSize = true
            };

            progress = new ProgressBar
            {
                Location = new System.Drawing.Point(20, 254),
                Size = new System.Drawing.Size(432, 18),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };
            lblStatus = new Label
            {
                Text = "",
                ForeColor = System.Drawing.Color.FromArgb(0xb4, 0xb4, 0xb4),
                Location = new System.Drawing.Point(20, 282),
                Size = new System.Drawing.Size(432, 28)
            };

            btnInstall = new Button
            {
                Text = "安装",
                Location = new System.Drawing.Point(312, 320),
                Size = new System.Drawing.Size(72, 30),
                BackColor = System.Drawing.Color.FromArgb(0xff, 0x53, 0x70),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold)
            };
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Click += BtnInstall_Click;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new System.Drawing.Point(388, 320),
                Size = new System.Drawing.Size(72, 30),
                BackColor = System.Drawing.Color.FromArgb(0x2d, 0x2d, 0x35),
                ForeColor = System.Drawing.Color.FromArgb(0xf8, 0xf8, 0xf2),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(0x40, 0x40, 0x4a);
            btnCancel.Click += (s, e) => Close();

            Controls.AddRange(new Control[] { title, sub, lblPath, txtPath, btnBrowse,
                chkDesktop, chkStartMenu, chkLaunch, progress, lblStatus, btnInstall, btnCancel });
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { Description = "选择安装目录", SelectedPath = txtPath.Text };
            if (dlg.ShowDialog(this) == DialogResult.OK) txtPath.Text = dlg.SelectedPath;
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            string dir = txtPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(dir))
            {
                MessageBox.Show(this, "请选择安装目录。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            btnInstall.Enabled = false;
            btnBrowse.Enabled = false;
            chkDesktop.Enabled = chkStartMenu.Enabled = chkLaunch.Enabled = false;
            progress.Visible = true;
            progress.Value = 0;
            lblStatus.Text = "准备安装…";

            var worker = new BackgroundWorker();
            worker.DoWork += (s, ev) =>
            {
                InstallEngine.Install(dir, chkDesktop.Checked, chkStartMenu.Checked, chkLaunch.Checked,
                    msg => Invoke((Action)(() => lblStatus.Text = msg)),
                    p => Invoke((Action)(() => progress.Value = p)));
            };
            worker.RunWorkerCompleted += (s, ev) =>
            {
                if (ev.Error != null)
                {
                    lblStatus.Text = "安装失败：" + ev.Error.Message;
                    MessageBox.Show(this, "安装失败：\n" + ev.Error.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnInstall.Enabled = true;
                    btnBrowse.Enabled = true;
                    chkDesktop.Enabled = chkStartMenu.Enabled = chkLaunch.Enabled = true;
                }
                else
                {
                    lblStatus.Text = "安装完成！";
                    MessageBox.Show(this, "PocketDeck 已安装完成。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
            };
            worker.RunWorkerAsync();
        }
    }
}
