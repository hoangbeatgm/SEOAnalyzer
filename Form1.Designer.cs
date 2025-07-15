
using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace SEOAnalyzer
{
    partial class Form1
    {
        private Button btnAnalyzeSEO;

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            showUrlsite = new Label();
            showPropertyID = new Label();
            menuStrip1 = new MenuStrip();
            settingsToolStripMenuItem = new ToolStripMenuItem();
            toolStripComboBox = new ToolStripComboBox();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // showUrlsite
            // 
            showUrlsite.AutoSize = true;
            showUrlsite.BackColor = Color.Yellow;
            showUrlsite.Font = new Font("Microsoft Sans Serif", 7.8F, FontStyle.Italic | FontStyle.Underline, GraphicsUnit.Point, 0);
            showUrlsite.Location = new Point(862, 0);
            showUrlsite.Name = "showUrlsite";
            showUrlsite.Size = new Size(70, 16);
            showUrlsite.TabIndex = 0;
            showUrlsite.Text = "http:/urlsite";
            // 
            // showPropertyID
            // 
            showPropertyID.AutoSize = true;
            showPropertyID.BackColor = Color.Yellow;
            showPropertyID.Font = new Font("Microsoft Sans Serif", 7.8F, FontStyle.Italic | FontStyle.Underline, GraphicsUnit.Point, 0);
            showPropertyID.Location = new Point(862, 22);
            showPropertyID.Name = "showPropertyID";
            showPropertyID.Size = new Size(70, 16);
            showPropertyID.TabIndex = 1;
            showPropertyID.Text = "propertyID";
            // 
            // menuStrip1
            // 
            menuStrip1.AutoSize = false;
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { settingsToolStripMenuItem, toolStripComboBox });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(949, 35);
            menuStrip1.TabIndex = 2;
            menuStrip1.Text = "menuStrip1";
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.AutoSize = false;
            settingsToolStripMenuItem.Font = new Font("Verdana", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            settingsToolStripMenuItem.Size = new Size(250, 35);
            settingsToolStripMenuItem.Text = "⚙️ Quản lý danh sách Website";
            settingsToolStripMenuItem.Click += settingsToolStripMenuItem_Click;
            // 
            // toolStripComboBox
            // 
            toolStripComboBox.AutoSize = false;
            toolStripComboBox.Name = "toolStripComboBox";
            toolStripComboBox.Size = new Size(220, 28);
            toolStripComboBox.Text = "🌐 Chuyển Website thống kê";
            toolStripComboBox.SelectedIndexChanged += toolStripComboBox_SelectedIndexChanged;
            toolStripComboBox.Click += toolStripComboBox_Click;
            // 
            // Form1
            // 
            AutoSize = true;
            ClientSize = new Size(949, 770);
            Controls.Add(showPropertyID);
            Controls.Add(showUrlsite);
            Controls.Add(menuStrip1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = menuStrip1;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            Load += Form1_Load;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
        private Label showUrlsite;
        private Label showPropertyID;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem settingsToolStripMenuItem;
        private ToolStripComboBox toolStripComboBox;
    }
}