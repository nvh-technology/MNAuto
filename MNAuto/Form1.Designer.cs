namespace MNAuto
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 800);
            this.Text = "MNAuto - Quản lý trình duyệt hàng loạt";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.MinimumSize = new System.Drawing.Size(1000, 600);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            
            // Tạo các controls
            this.lblProfileCount = new System.Windows.Forms.Label();
            this.txtProfileCount = new System.Windows.Forms.TextBox();
            this.lblWalletPassword = new System.Windows.Forms.Label();
            this.txtWalletPassword = new System.Windows.Forms.TextBox();
            this.btnCreateProfiles = new System.Windows.Forms.Button();
            this.chkSelectAll = new System.Windows.Forms.CheckBox();
            this.dgvProfiles = new System.Windows.Forms.DataGridView();
            this.btnInitializeSelected = new System.Windows.Forms.Button();
            this.btnStartSelected = new System.Windows.Forms.Button();
            // Loại bỏ nút "Mở trình duyệt"
            this.btnCloseSelected = new System.Windows.Forms.Button();
            this.btnDeleteSelected = new System.Windows.Forms.Button();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.lblLog = new System.Windows.Forms.Label();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.btnClearLog = new System.Windows.Forms.Button();
            this.btnExportAll = new System.Windows.Forms.Button();
            
            // Cấu hình các controls
            this.lblProfileCount.AutoSize = true;
            this.lblProfileCount.Location = new System.Drawing.Point(12, 15);
            this.lblProfileCount.Name = "lblProfileCount";
            this.lblProfileCount.Size = new System.Drawing.Size(118, 17);
            this.lblProfileCount.TabIndex = 0;
            this.lblProfileCount.Text = "Số lượng profile:";
            
            this.txtProfileCount.Location = new System.Drawing.Point(136, 12);
            this.txtProfileCount.Name = "txtProfileCount";
            this.txtProfileCount.Size = new System.Drawing.Size(100, 23);
            this.txtProfileCount.TabIndex = 1;
            this.txtProfileCount.Text = "5";
            
            // Mật khẩu ví
            this.lblWalletPassword.AutoSize = true;
            this.lblWalletPassword.Location = new System.Drawing.Point(260, 15);
            this.lblWalletPassword.Name = "lblWalletPassword";
            this.lblWalletPassword.Text = "Mật khẩu ví:";
            
            this.txtWalletPassword.Location = new System.Drawing.Point(360, 12);
            this.txtWalletPassword.Name = "txtWalletPassword";
            this.txtWalletPassword.Size = new System.Drawing.Size(180, 23);
            this.txtWalletPassword.UseSystemPasswordChar = false;

            // Nút tạo profile (dịch sang phải sau ô mật khẩu)
            this.btnCreateProfiles.Location = new System.Drawing.Point(560, 11);
            this.btnCreateProfiles.Name = "btnCreateProfiles";
            this.btnCreateProfiles.Size = new System.Drawing.Size(120, 25);
            this.btnCreateProfiles.TabIndex = 2;
            this.btnCreateProfiles.Text = "Tạo Profiles";
            this.btnCreateProfiles.UseVisualStyleBackColor = true;
            this.btnCreateProfiles.Click += new System.EventHandler(this.btnCreateProfiles_Click);
            
            this.chkSelectAll.AutoSize = true;
            this.chkSelectAll.Location = new System.Drawing.Point(12, 45);
            this.chkSelectAll.Name = "chkSelectAll";
            this.chkSelectAll.Size = new System.Drawing.Size(89, 21);
            this.chkSelectAll.TabIndex = 3;
            this.chkSelectAll.Text = "Chọn tất cả";
            this.chkSelectAll.UseVisualStyleBackColor = true;
            this.chkSelectAll.CheckedChanged += new System.EventHandler(this.chkSelectAll_CheckedChanged);
            
            this.dgvProfiles.AllowUserToAddRows = false;
            this.dgvProfiles.AllowUserToDeleteRows = false;
            this.dgvProfiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvProfiles.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this.dgvProfiles.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvProfiles.Location = new System.Drawing.Point(12, 70);
            this.dgvProfiles.Name = "dgvProfiles";
            this.dgvProfiles.ReadOnly = false;
            this.dgvProfiles.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dgvProfiles.RowTemplate.Height = 24;
            this.dgvProfiles.Size = new System.Drawing.Size(1176, 300);
            this.dgvProfiles.TabIndex = 4;
            this.dgvProfiles.CurrentCellDirtyStateChanged += new System.EventHandler(this.dgvProfiles_CurrentCellDirtyStateChanged);
            
            
            this.btnInitializeSelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnInitializeSelected.Location = new System.Drawing.Point(12, 380);
            this.btnInitializeSelected.Name = "btnInitializeSelected";
            this.btnInitializeSelected.Size = new System.Drawing.Size(120, 30);
            this.btnInitializeSelected.TabIndex = 5;
            this.btnInitializeSelected.Text = "Khởi tạo";
            this.btnInitializeSelected.UseVisualStyleBackColor = true;
            this.btnInitializeSelected.Click += new System.EventHandler(this.btnInitializeSelected_Click);
            
            this.btnStartSelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnStartSelected.Location = new System.Drawing.Point(138, 380);
            this.btnStartSelected.Name = "btnStartSelected";
            this.btnStartSelected.Size = new System.Drawing.Size(120, 30);
            this.btnStartSelected.TabIndex = 6;
            this.btnStartSelected.Text = "Mở trình duyệt";
            this.btnStartSelected.UseVisualStyleBackColor = true;
            this.btnStartSelected.Click += new System.EventHandler(this.btnStartSelected_Click);
            
            
            // Loại bỏ nút "Mở trình duyệt"
            
            this.btnCloseSelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnCloseSelected.Location = new System.Drawing.Point(390, 380);
            this.btnCloseSelected.Name = "btnCloseSelected";
            this.btnCloseSelected.Size = new System.Drawing.Size(120, 30);
            this.btnCloseSelected.TabIndex = 8;
            this.btnCloseSelected.Text = "Đóng";
            this.btnCloseSelected.UseVisualStyleBackColor = true;
            this.btnCloseSelected.Click += new System.EventHandler(this.btnCloseSelected_Click);
            
            this.btnDeleteSelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDeleteSelected.Location = new System.Drawing.Point(516, 380);
            this.btnDeleteSelected.Name = "btnDeleteSelected";
            this.btnDeleteSelected.Size = new System.Drawing.Size(120, 30);
            this.btnDeleteSelected.TabIndex = 9;
            this.btnDeleteSelected.Text = "Xóa";
            this.btnDeleteSelected.UseVisualStyleBackColor = true;
            this.btnDeleteSelected.Click += new System.EventHandler(this.btnDeleteSelected_Click);
            
            this.btnRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnRefresh.Location = new System.Drawing.Point(642, 380);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(120, 30);
            this.btnRefresh.TabIndex = 10;
            this.btnRefresh.Text = "Làm mới";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            
            this.btnExportAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnExportAll.Location = new System.Drawing.Point(768, 380);
            this.btnExportAll.Name = "btnExportAll";
            this.btnExportAll.Size = new System.Drawing.Size(150, 30);
            this.btnExportAll.TabIndex = 11;
            this.btnExportAll.Text = "Export All Profile";
            this.btnExportAll.UseVisualStyleBackColor = true;
            this.btnExportAll.Click += new System.EventHandler(this.btnExportAll_Click);
            
            this.lblLog.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblLog.AutoSize = true;
            this.lblLog.Location = new System.Drawing.Point(12, 420);
            this.lblLog.Name = "lblLog";
            this.lblLog.Size = new System.Drawing.Size(30, 17);
            this.lblLog.TabIndex = 10;
            this.lblLog.Text = "Log:";
            
            this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLog.Location = new System.Drawing.Point(12, 440);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(1176, 320);
            this.txtLog.TabIndex = 11;
            this.txtLog.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            
            this.btnClearLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClearLog.Location = new System.Drawing.Point(1068, 410);
            this.btnClearLog.Name = "btnClearLog";
            this.btnClearLog.Size = new System.Drawing.Size(120, 25);
            this.btnClearLog.TabIndex = 12;
            this.btnClearLog.Text = "Xóa Log";
            this.btnClearLog.UseVisualStyleBackColor = true;
            this.btnClearLog.Click += new System.EventHandler(this.btnClearLog_Click);
            
            // Thêm controls vào form
            this.Controls.Add(this.lblProfileCount);
            this.Controls.Add(this.txtProfileCount);
            this.Controls.Add(this.lblWalletPassword);
            this.Controls.Add(this.txtWalletPassword);
            this.Controls.Add(this.btnCreateProfiles);
            this.Controls.Add(this.chkSelectAll);
            this.Controls.Add(this.dgvProfiles);
            this.Controls.Add(this.btnInitializeSelected);
            this.Controls.Add(this.btnStartSelected);
            // Loại bỏ nút "Mở trình duyệt"
            this.Controls.Add(this.btnCloseSelected);
            this.Controls.Add(this.btnDeleteSelected);
            this.Controls.Add(this.btnRefresh);
            this.Controls.Add(this.btnExportAll);
            this.Controls.Add(this.lblLog);
            this.Controls.Add(this.txtLog);
            this.Controls.Add(this.btnClearLog);
        }

        #endregion

        private System.Windows.Forms.Label lblProfileCount;
        private System.Windows.Forms.TextBox txtProfileCount;
        private System.Windows.Forms.Label lblWalletPassword;
        private System.Windows.Forms.TextBox txtWalletPassword;
        private System.Windows.Forms.Button btnCreateProfiles;
        private System.Windows.Forms.CheckBox chkSelectAll;
        private System.Windows.Forms.DataGridView dgvProfiles;
        private System.Windows.Forms.Button btnInitializeSelected;
        private System.Windows.Forms.Button btnStartSelected;
        // Loại bỏ nút "Mở trình duyệt"
        private System.Windows.Forms.Button btnCloseSelected;
        private System.Windows.Forms.Button btnDeleteSelected;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnExportAll;
        private System.Windows.Forms.Label lblLog;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.Button btnClearLog;
    }
}
