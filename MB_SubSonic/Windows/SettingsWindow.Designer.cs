namespace MusicBeePlugin.Windows
{
    partial class SettingsWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.ButtonAbout = new System.Windows.Forms.Button();
            this.ButtonCancel = new System.Windows.Forms.Button();
            this.ButtonSave = new System.Windows.Forms.Button();
            this.LabelProtocol = new System.Windows.Forms.Label();
            this.TextBoxHostname = new System.Windows.Forms.TextBox();
            this.ComboBoxProtocol = new System.Windows.Forms.ComboBox();
            this.LabelHostname = new System.Windows.Forms.Label();
            this.LabelPath = new System.Windows.Forms.Label();
            this.TextBoxPath = new System.Windows.Forms.TextBox();
            this.LabelUsername = new System.Windows.Forms.Label();
            this.TextBoxUsername = new System.Windows.Forms.TextBox();
            this.TextBoxPassword = new System.Windows.Forms.TextBox();
            this.LabelPassword = new System.Windows.Forms.Label();
            this.LabelPort = new System.Windows.Forms.Label();
            this.TextBoxPort = new System.Windows.Forms.TextBox();
            this.LabelAuth = new System.Windows.Forms.Label();
            this.ComboBoxAuth = new System.Windows.Forms.ComboBox();
            this.CheckBoxTranscode = new System.Windows.Forms.CheckBox();
            this.ComboBoxBitrate = new System.Windows.Forms.ComboBox();
            this.LabelBitrate = new System.Windows.Forms.Label();
            this.GroupBoxTranscoding = new System.Windows.Forms.GroupBox();
            this.ButtonPing = new System.Windows.Forms.Button();
            this.GroupBoxServer = new System.Windows.Forms.GroupBox();
            this.CheckBoxCache = new System.Windows.Forms.CheckBox();
            this.ButtonDeleteCache = new System.Windows.Forms.Button();
            this.CheckBoxPreCache = new System.Windows.Forms.CheckBox();
            this.GroupBoxTranscoding.SuspendLayout();
            this.GroupBoxServer.SuspendLayout();
            this.SuspendLayout();
            // 
            // ButtonAbout
            // 
            this.ButtonAbout.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ButtonAbout.Location = new System.Drawing.Point(511, 12);
            this.ButtonAbout.Margin = new System.Windows.Forms.Padding(5);
            this.ButtonAbout.Name = "ButtonAbout";
            this.ButtonAbout.Size = new System.Drawing.Size(75, 30);
            this.ButtonAbout.TabIndex = 1;
            this.ButtonAbout.Text = "About";
            this.ButtonAbout.UseVisualStyleBackColor = true;
            this.ButtonAbout.Click += new System.EventHandler(this.ButtonAbout_Click);
            // 
            // ButtonCancel
            // 
            this.ButtonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ButtonCancel.Location = new System.Drawing.Point(511, 277);
            this.ButtonCancel.Margin = new System.Windows.Forms.Padding(5);
            this.ButtonCancel.Name = "ButtonCancel";
            this.ButtonCancel.Size = new System.Drawing.Size(75, 30);
            this.ButtonCancel.TabIndex = 2;
            this.ButtonCancel.Text = "Cancel";
            this.ButtonCancel.UseVisualStyleBackColor = true;
            this.ButtonCancel.Click += new System.EventHandler(this.ButtonCancel_Click);
            // 
            // ButtonSave
            // 
            this.ButtonSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ButtonSave.Location = new System.Drawing.Point(428, 277);
            this.ButtonSave.Margin = new System.Windows.Forms.Padding(5);
            this.ButtonSave.Name = "ButtonSave";
            this.ButtonSave.Size = new System.Drawing.Size(75, 30);
            this.ButtonSave.TabIndex = 3;
            this.ButtonSave.Text = "Save";
            this.ButtonSave.UseVisualStyleBackColor = true;
            this.ButtonSave.Click += new System.EventHandler(this.ButtonSave_Click);
            // 
            // LabelProtocol
            // 
            this.LabelProtocol.AutoSize = true;
            this.LabelProtocol.Location = new System.Drawing.Point(23, 37);
            this.LabelProtocol.Name = "LabelProtocol";
            this.LabelProtocol.Size = new System.Drawing.Size(49, 13);
            this.LabelProtocol.TabIndex = 4;
            this.LabelProtocol.Text = "Protocol:";
            // 
            // TextBoxHostname
            // 
            this.TextBoxHostname.Location = new System.Drawing.Point(87, 64);
            this.TextBoxHostname.Name = "TextBoxHostname";
            this.TextBoxHostname.Size = new System.Drawing.Size(151, 20);
            this.TextBoxHostname.TabIndex = 5;
            // 
            // ComboBoxProtocol
            // 
            this.ComboBoxProtocol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ComboBoxProtocol.FormattingEnabled = true;
            this.ComboBoxProtocol.Location = new System.Drawing.Point(87, 34);
            this.ComboBoxProtocol.Name = "ComboBoxProtocol";
            this.ComboBoxProtocol.Size = new System.Drawing.Size(75, 21);
            this.ComboBoxProtocol.TabIndex = 6;
            // 
            // LabelHostname
            // 
            this.LabelHostname.AutoSize = true;
            this.LabelHostname.Location = new System.Drawing.Point(23, 67);
            this.LabelHostname.Name = "LabelHostname";
            this.LabelHostname.Size = new System.Drawing.Size(58, 13);
            this.LabelHostname.TabIndex = 7;
            this.LabelHostname.Text = "Hostname:";
            // 
            // LabelPath
            // 
            this.LabelPath.AutoSize = true;
            this.LabelPath.Location = new System.Drawing.Point(23, 97);
            this.LabelPath.Name = "LabelPath";
            this.LabelPath.Size = new System.Drawing.Size(32, 13);
            this.LabelPath.TabIndex = 8;
            this.LabelPath.Text = "Path:";
            // 
            // TextBoxPath
            // 
            this.TextBoxPath.Location = new System.Drawing.Point(87, 94);
            this.TextBoxPath.Name = "TextBoxPath";
            this.TextBoxPath.Size = new System.Drawing.Size(151, 20);
            this.TextBoxPath.TabIndex = 9;
            // 
            // LabelUsername
            // 
            this.LabelUsername.AutoSize = true;
            this.LabelUsername.Location = new System.Drawing.Point(23, 127);
            this.LabelUsername.Name = "LabelUsername";
            this.LabelUsername.Size = new System.Drawing.Size(58, 13);
            this.LabelUsername.TabIndex = 10;
            this.LabelUsername.Text = "Username:";
            // 
            // TextBoxUsername
            // 
            this.TextBoxUsername.Location = new System.Drawing.Point(87, 124);
            this.TextBoxUsername.Name = "TextBoxUsername";
            this.TextBoxUsername.Size = new System.Drawing.Size(151, 20);
            this.TextBoxUsername.TabIndex = 11;
            // 
            // TextBoxPassword
            // 
            this.TextBoxPassword.Location = new System.Drawing.Point(87, 154);
            this.TextBoxPassword.Name = "TextBoxPassword";
            this.TextBoxPassword.PasswordChar = '*';
            this.TextBoxPassword.Size = new System.Drawing.Size(151, 20);
            this.TextBoxPassword.TabIndex = 12;
            // 
            // LabelPassword
            // 
            this.LabelPassword.AutoSize = true;
            this.LabelPassword.Location = new System.Drawing.Point(23, 157);
            this.LabelPassword.Name = "LabelPassword";
            this.LabelPassword.Size = new System.Drawing.Size(56, 13);
            this.LabelPassword.TabIndex = 13;
            this.LabelPassword.Text = "Password:";
            // 
            // LabelPort
            // 
            this.LabelPort.AutoSize = true;
            this.LabelPort.Location = new System.Drawing.Point(258, 67);
            this.LabelPort.Name = "LabelPort";
            this.LabelPort.Size = new System.Drawing.Size(29, 13);
            this.LabelPort.TabIndex = 14;
            this.LabelPort.Text = "Port:";
            // 
            // TextBoxPort
            // 
            this.TextBoxPort.Location = new System.Drawing.Point(293, 64);
            this.TextBoxPort.Name = "TextBoxPort";
            this.TextBoxPort.Size = new System.Drawing.Size(75, 20);
            this.TextBoxPort.TabIndex = 15;
            // 
            // LabelAuth
            // 
            this.LabelAuth.AutoSize = true;
            this.LabelAuth.Location = new System.Drawing.Point(23, 187);
            this.LabelAuth.Name = "LabelAuth";
            this.LabelAuth.Size = new System.Drawing.Size(78, 13);
            this.LabelAuth.TabIndex = 16;
            this.LabelAuth.Text = "Authentication:";
            // 
            // ComboBoxAuth
            // 
            this.ComboBoxAuth.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ComboBoxAuth.FormattingEnabled = true;
            this.ComboBoxAuth.Location = new System.Drawing.Point(116, 184);
            this.ComboBoxAuth.Name = "ComboBoxAuth";
            this.ComboBoxAuth.Size = new System.Drawing.Size(121, 21);
            this.ComboBoxAuth.TabIndex = 17;
            // 
            // CheckBoxTranscode
            // 
            this.CheckBoxTranscode.AutoSize = true;
            this.CheckBoxTranscode.Location = new System.Drawing.Point(18, 32);
            this.CheckBoxTranscode.Name = "CheckBoxTranscode";
            this.CheckBoxTranscode.Size = new System.Drawing.Size(114, 17);
            this.CheckBoxTranscode.TabIndex = 18;
            this.CheckBoxTranscode.Text = "Transcode as mp3";
            this.CheckBoxTranscode.UseVisualStyleBackColor = true;
            // 
            // ComboBoxBitrate
            // 
            this.ComboBoxBitrate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ComboBoxBitrate.FormattingEnabled = true;
            this.ComboBoxBitrate.Location = new System.Drawing.Point(52, 60);
            this.ComboBoxBitrate.Name = "ComboBoxBitrate";
            this.ComboBoxBitrate.Size = new System.Drawing.Size(100, 21);
            this.ComboBoxBitrate.TabIndex = 19;
            // 
            // LabelBitrate
            // 
            this.LabelBitrate.AutoSize = true;
            this.LabelBitrate.Location = new System.Drawing.Point(6, 63);
            this.LabelBitrate.Name = "LabelBitrate";
            this.LabelBitrate.Size = new System.Drawing.Size(40, 13);
            this.LabelBitrate.TabIndex = 20;
            this.LabelBitrate.Text = "Bitrate:";
            // 
            // GroupBoxTranscoding
            // 
            this.GroupBoxTranscoding.Controls.Add(this.ComboBoxBitrate);
            this.GroupBoxTranscoding.Controls.Add(this.CheckBoxTranscode);
            this.GroupBoxTranscoding.Controls.Add(this.LabelBitrate);
            this.GroupBoxTranscoding.Location = new System.Drawing.Point(406, 75);
            this.GroupBoxTranscoding.Name = "GroupBoxTranscoding";
            this.GroupBoxTranscoding.Size = new System.Drawing.Size(163, 111);
            this.GroupBoxTranscoding.TabIndex = 21;
            this.GroupBoxTranscoding.TabStop = false;
            this.GroupBoxTranscoding.Text = "Transcoding options";
            // 
            // ButtonPing
            // 
            this.ButtonPing.Location = new System.Drawing.Point(293, 178);
            this.ButtonPing.Name = "ButtonPing";
            this.ButtonPing.Size = new System.Drawing.Size(75, 30);
            this.ButtonPing.TabIndex = 22;
            this.ButtonPing.Text = "Ping Server";
            this.ButtonPing.UseVisualStyleBackColor = true;
            this.ButtonPing.Click += new System.EventHandler(this.ButtonPing_Click);
            // 
            // GroupBoxServer
            // 
            this.GroupBoxServer.Controls.Add(this.LabelProtocol);
            this.GroupBoxServer.Controls.Add(this.ButtonPing);
            this.GroupBoxServer.Controls.Add(this.TextBoxHostname);
            this.GroupBoxServer.Controls.Add(this.ComboBoxProtocol);
            this.GroupBoxServer.Controls.Add(this.ComboBoxAuth);
            this.GroupBoxServer.Controls.Add(this.LabelHostname);
            this.GroupBoxServer.Controls.Add(this.LabelAuth);
            this.GroupBoxServer.Controls.Add(this.LabelPath);
            this.GroupBoxServer.Controls.Add(this.TextBoxPort);
            this.GroupBoxServer.Controls.Add(this.TextBoxPath);
            this.GroupBoxServer.Controls.Add(this.LabelPort);
            this.GroupBoxServer.Controls.Add(this.LabelUsername);
            this.GroupBoxServer.Controls.Add(this.LabelPassword);
            this.GroupBoxServer.Controls.Add(this.TextBoxUsername);
            this.GroupBoxServer.Controls.Add(this.TextBoxPassword);
            this.GroupBoxServer.Location = new System.Drawing.Point(15, 12);
            this.GroupBoxServer.Name = "GroupBoxServer";
            this.GroupBoxServer.Size = new System.Drawing.Size(385, 236);
            this.GroupBoxServer.TabIndex = 23;
            this.GroupBoxServer.TabStop = false;
            this.GroupBoxServer.Text = "Subsonic Server";
            // 
            // CheckBoxCache
            // 
            this.CheckBoxCache.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.CheckBoxCache.AutoSize = true;
            this.CheckBoxCache.Location = new System.Drawing.Point(15, 254);
            this.CheckBoxCache.Name = "CheckBoxCache";
            this.CheckBoxCache.Size = new System.Drawing.Size(128, 17);
            this.CheckBoxCache.TabIndex = 24;
            this.CheckBoxCache.Text = "Cache server indexes";
            this.CheckBoxCache.UseVisualStyleBackColor = true;
            // 
            // ButtonDeleteCache
            // 
            this.ButtonDeleteCache.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.ButtonDeleteCache.Location = new System.Drawing.Point(15, 277);
            this.ButtonDeleteCache.Name = "ButtonDeleteCache";
            this.ButtonDeleteCache.Size = new System.Drawing.Size(128, 30);
            this.ButtonDeleteCache.TabIndex = 25;
            this.ButtonDeleteCache.Text = "Delete Cache";
            this.ButtonDeleteCache.UseVisualStyleBackColor = true;
            this.ButtonDeleteCache.Click += new System.EventHandler(this.ButtonDeleteCache_Click);
            // 
            // CheckBoxPreCache
            // 
            this.CheckBoxPreCache.AutoSize = true;
            this.CheckBoxPreCache.Location = new System.Drawing.Point(172, 254);
            this.CheckBoxPreCache.Name = "CheckBoxPreCache";
            this.CheckBoxPreCache.Size = new System.Drawing.Size(142, 17);
            this.CheckBoxPreCache.TabIndex = 26;
            this.CheckBoxPreCache.Text = "Pre-Cache all recursively";
            this.CheckBoxPreCache.UseVisualStyleBackColor = true;
            // 
            // SettingsWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(598, 319);
            this.Controls.Add(this.CheckBoxPreCache);
            this.Controls.Add(this.ButtonDeleteCache);
            this.Controls.Add(this.CheckBoxCache);
            this.Controls.Add(this.GroupBoxServer);
            this.Controls.Add(this.GroupBoxTranscoding);
            this.Controls.Add(this.ButtonSave);
            this.Controls.Add(this.ButtonCancel);
            this.Controls.Add(this.ButtonAbout);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "SettingsWindow";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Subsonic Settings Window";
            this.GroupBoxTranscoding.ResumeLayout(false);
            this.GroupBoxTranscoding.PerformLayout();
            this.GroupBoxServer.ResumeLayout(false);
            this.GroupBoxServer.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button ButtonAbout;
        private System.Windows.Forms.Button ButtonCancel;
        private System.Windows.Forms.Button ButtonSave;
        private System.Windows.Forms.Label LabelProtocol;
        private System.Windows.Forms.TextBox TextBoxHostname;
        private System.Windows.Forms.ComboBox ComboBoxProtocol;
        private System.Windows.Forms.Label LabelHostname;
        private System.Windows.Forms.Label LabelPath;
        private System.Windows.Forms.TextBox TextBoxPath;
        private System.Windows.Forms.Label LabelUsername;
        private System.Windows.Forms.TextBox TextBoxUsername;
        private System.Windows.Forms.TextBox TextBoxPassword;
        private System.Windows.Forms.Label LabelPassword;
        private System.Windows.Forms.Label LabelPort;
        private System.Windows.Forms.TextBox TextBoxPort;
        private System.Windows.Forms.Label LabelAuth;
        private System.Windows.Forms.ComboBox ComboBoxAuth;
        private System.Windows.Forms.CheckBox CheckBoxTranscode;
        private System.Windows.Forms.ComboBox ComboBoxBitrate;
        private System.Windows.Forms.Label LabelBitrate;
        private System.Windows.Forms.GroupBox GroupBoxTranscoding;
        private System.Windows.Forms.Button ButtonPing;
        private System.Windows.Forms.GroupBox GroupBoxServer;
        private System.Windows.Forms.CheckBox CheckBoxCache;
        private System.Windows.Forms.Button ButtonDeleteCache;
        private System.Windows.Forms.CheckBox CheckBoxPreCache;
    }
}