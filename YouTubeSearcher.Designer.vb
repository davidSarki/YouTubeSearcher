<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class YouTubeSearcher
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New ComponentModel.Container()
        srchString = New TextBox()
        logtxt = New TextBox()
        btnGetUrl = New Button()
        chkDebugMode = New CheckBox()
        btnGetStats = New Button()
        txtAPI = New TextBox()
        chkBasic = New CheckBox()
        chkAPI = New CheckBox()
        SplitContainer1 = New SplitContainer()
        LangCombo = New ComboBox()
        btnSTOP = New Button()
        SetProcessTime = New DateTimePicker()
        btnProcessALLMP3 = New Button()
        btnGetSelected = New Button()
        btnUncheckSelected = New Button()
        btnCheckSelected = New Button()
        btnLoad = New Button()
        btnSave = New Button()
        btnRefresh = New Button()
        btnRemoveFolder = New Button()
        btnClrTvw = New Button()
        btnAddFolder = New Button()
        tvwFolders = New TreeView()
        TvwImageList = New ImageList(components)
        Splitter2 = New Splitter()
        Splitter1 = New Splitter()
        SQLString = New TextBox()
        StatusStrip1 = New StatusStrip()
        ProgressBar1 = New ToolStripProgressBar()
        StatusText = New ToolStripStatusLabel()
        ToolStripDropDownButton1 = New ToolStripDropDownButton()
        btnSaveSettings = New ToolStripMenuItem()
        btnLoadSettings = New ToolStripMenuItem()
        Timer1 = New Timer(components)
        btnProcessCustom = New Button()
        CType(SplitContainer1, ComponentModel.ISupportInitialize).BeginInit()
        SplitContainer1.Panel1.SuspendLayout()
        SplitContainer1.Panel2.SuspendLayout()
        SplitContainer1.SuspendLayout()
        StatusStrip1.SuspendLayout()
        SuspendLayout()
        ' 
        ' srchString
        ' 
        srchString.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        srchString.Location = New Point(4, 140)
        srchString.Margin = New Padding(4, 3, 4, 3)
        srchString.Name = "srchString"
        srchString.Size = New Size(881, 23)
        srchString.TabIndex = 0
        ' 
        ' logtxt
        ' 
        logtxt.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        logtxt.Location = New Point(4, 3)
        logtxt.Margin = New Padding(4, 3, 4, 3)
        logtxt.Multiline = True
        logtxt.Name = "logtxt"
        logtxt.ScrollBars = ScrollBars.Vertical
        logtxt.Size = New Size(881, 112)
        logtxt.TabIndex = 1
        ' 
        ' btnGetUrl
        ' 
        btnGetUrl.Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        btnGetUrl.Location = New Point(590, 170)
        btnGetUrl.Margin = New Padding(4, 3, 4, 3)
        btnGetUrl.Name = "btnGetUrl"
        btnGetUrl.Size = New Size(154, 35)
        btnGetUrl.TabIndex = 2
        btnGetUrl.Text = "get URL"
        btnGetUrl.UseVisualStyleBackColor = True
        ' 
        ' chkDebugMode
        ' 
        chkDebugMode.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        chkDebugMode.AutoSize = True
        chkDebugMode.Location = New Point(10, 180)
        chkDebugMode.Margin = New Padding(4, 3, 4, 3)
        chkDebugMode.Name = "chkDebugMode"
        chkDebugMode.Size = New Size(61, 19)
        chkDebugMode.TabIndex = 3
        chkDebugMode.Text = "Debug"
        chkDebugMode.UseVisualStyleBackColor = True
        ' 
        ' btnGetStats
        ' 
        btnGetStats.Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        btnGetStats.Location = New Point(751, 170)
        btnGetStats.Margin = New Padding(4, 3, 4, 3)
        btnGetStats.Name = "btnGetStats"
        btnGetStats.Size = New Size(131, 35)
        btnGetStats.TabIndex = 4
        btnGetStats.Text = "get Stats"
        btnGetStats.UseVisualStyleBackColor = True
        ' 
        ' txtAPI
        ' 
        txtAPI.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        txtAPI.Location = New Point(279, 177)
        txtAPI.Margin = New Padding(4, 3, 4, 3)
        txtAPI.Name = "txtAPI"
        txtAPI.Size = New Size(304, 23)
        txtAPI.TabIndex = 6
        txtAPI.Text = "AIzaSyAiGwLj2rZK7WCdVEd7kGWZFqnX_dHFXqA"
        ' 
        ' chkBasic
        ' 
        chkBasic.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        chkBasic.AutoSize = True
        chkBasic.Location = New Point(85, 180)
        chkBasic.Margin = New Padding(4, 3, 4, 3)
        chkBasic.Name = "chkBasic"
        chkBasic.Size = New Size(90, 19)
        chkBasic.TabIndex = 7
        chkBasic.Text = "Basic search"
        chkBasic.UseVisualStyleBackColor = True
        ' 
        ' chkAPI
        ' 
        chkAPI.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        chkAPI.AutoSize = True
        chkAPI.Location = New Point(196, 180)
        chkAPI.Margin = New Padding(4, 3, 4, 3)
        chkAPI.Name = "chkAPI"
        chkAPI.Size = New Size(66, 19)
        chkAPI.TabIndex = 8
        chkAPI.Text = "Use API"
        chkAPI.UseVisualStyleBackColor = True
        ' 
        ' SplitContainer1
        ' 
        SplitContainer1.Dock = DockStyle.Fill
        SplitContainer1.Location = New Point(0, 0)
        SplitContainer1.Margin = New Padding(4, 3, 4, 3)
        SplitContainer1.Name = "SplitContainer1"
        SplitContainer1.Orientation = Orientation.Horizontal
        ' 
        ' SplitContainer1.Panel1
        ' 
        SplitContainer1.Panel1.Controls.Add(btnProcessCustom)
        SplitContainer1.Panel1.Controls.Add(LangCombo)
        SplitContainer1.Panel1.Controls.Add(btnSTOP)
        SplitContainer1.Panel1.Controls.Add(SetProcessTime)
        SplitContainer1.Panel1.Controls.Add(btnProcessALLMP3)
        SplitContainer1.Panel1.Controls.Add(btnGetSelected)
        SplitContainer1.Panel1.Controls.Add(btnUncheckSelected)
        SplitContainer1.Panel1.Controls.Add(btnCheckSelected)
        SplitContainer1.Panel1.Controls.Add(btnLoad)
        SplitContainer1.Panel1.Controls.Add(btnSave)
        SplitContainer1.Panel1.Controls.Add(btnRefresh)
        SplitContainer1.Panel1.Controls.Add(btnRemoveFolder)
        SplitContainer1.Panel1.Controls.Add(btnClrTvw)
        SplitContainer1.Panel1.Controls.Add(btnAddFolder)
        SplitContainer1.Panel1.Controls.Add(tvwFolders)
        SplitContainer1.Panel1.Controls.Add(Splitter2)
        SplitContainer1.Panel1.Controls.Add(Splitter1)
        ' 
        ' SplitContainer1.Panel2
        ' 
        SplitContainer1.Panel2.Controls.Add(SQLString)
        SplitContainer1.Panel2.Controls.Add(StatusStrip1)
        SplitContainer1.Panel2.Controls.Add(logtxt)
        SplitContainer1.Panel2.Controls.Add(txtAPI)
        SplitContainer1.Panel2.Controls.Add(srchString)
        SplitContainer1.Panel2.Controls.Add(chkAPI)
        SplitContainer1.Panel2.Controls.Add(btnGetUrl)
        SplitContainer1.Panel2.Controls.Add(chkBasic)
        SplitContainer1.Panel2.Controls.Add(chkDebugMode)
        SplitContainer1.Panel2.Controls.Add(btnGetStats)
        SplitContainer1.Size = New Size(889, 767)
        SplitContainer1.SplitterDistance = 526
        SplitContainer1.SplitterWidth = 5
        SplitContainer1.TabIndex = 9
        ' 
        ' LangCombo
        ' 
        LangCombo.FormattingEnabled = True
        LangCombo.Items.AddRange(New Object() {"Armenian", "Russian", "Armenian, Russian", "Armenian, English", "Russian, English", "English"})
        LangCombo.Location = New Point(756, 371)
        LangCombo.Margin = New Padding(4, 3, 4, 3)
        LangCombo.Name = "LangCombo"
        LangCombo.Size = New Size(124, 23)
        LangCombo.TabIndex = 16
        LangCombo.Text = "Armenian"
        ' 
        ' btnSTOP
        ' 
        btnSTOP.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnSTOP.Location = New Point(752, 401)
        btnSTOP.Margin = New Padding(4, 3, 4, 3)
        btnSTOP.Name = "btnSTOP"
        btnSTOP.Size = New Size(133, 36)
        btnSTOP.TabIndex = 15
        btnSTOP.Text = "■ STOP Process"
        btnSTOP.UseVisualStyleBackColor = True
        ' 
        ' SetProcessTime
        ' 
        SetProcessTime.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        SetProcessTime.Checked = False
        SetProcessTime.Format = DateTimePickerFormat.Time
        SetProcessTime.Location = New Point(757, 342)
        SetProcessTime.Margin = New Padding(4, 3, 4, 3)
        SetProcessTime.Name = "SetProcessTime"
        SetProcessTime.ShowCheckBox = True
        SetProcessTime.ShowUpDown = True
        SetProcessTime.Size = New Size(123, 23)
        SetProcessTime.TabIndex = 14
        ' 
        ' btnProcessALLMP3
        ' 
        btnProcessALLMP3.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnProcessALLMP3.Location = New Point(752, 299)
        btnProcessALLMP3.Margin = New Padding(4, 3, 4, 3)
        btnProcessALLMP3.Name = "btnProcessALLMP3"
        btnProcessALLMP3.Size = New Size(133, 36)
        btnProcessALLMP3.TabIndex = 12
        btnProcessALLMP3.Text = "↨ Process Manually"
        btnProcessALLMP3.UseVisualStyleBackColor = True
        ' 
        ' btnGetSelected
        ' 
        btnGetSelected.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnGetSelected.Location = New Point(752, 209)
        btnGetSelected.Margin = New Padding(4, 3, 4, 3)
        btnGetSelected.Name = "btnGetSelected"
        btnGetSelected.Size = New Size(133, 31)
        btnGetSelected.TabIndex = 11
        btnGetSelected.Text = "Show Selected"
        btnGetSelected.UseVisualStyleBackColor = True
        ' 
        ' btnUncheckSelected
        ' 
        btnUncheckSelected.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnUncheckSelected.Location = New Point(752, 172)
        btnUncheckSelected.Margin = New Padding(4, 3, 4, 3)
        btnUncheckSelected.Name = "btnUncheckSelected"
        btnUncheckSelected.Size = New Size(133, 31)
        btnUncheckSelected.TabIndex = 10
        btnUncheckSelected.Text = "⍻Uncheck Selected"
        btnUncheckSelected.UseVisualStyleBackColor = True
        ' 
        ' btnCheckSelected
        ' 
        btnCheckSelected.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnCheckSelected.Location = New Point(752, 135)
        btnCheckSelected.Margin = New Padding(4, 3, 4, 3)
        btnCheckSelected.Name = "btnCheckSelected"
        btnCheckSelected.Size = New Size(133, 31)
        btnCheckSelected.TabIndex = 9
        btnCheckSelected.Text = "✓ Check Selected"
        btnCheckSelected.UseVisualStyleBackColor = True
        ' 
        ' btnLoad
        ' 
        btnLoad.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnLoad.Location = New Point(752, 443)
        btnLoad.Margin = New Padding(4, 3, 4, 3)
        btnLoad.Name = "btnLoad"
        btnLoad.Size = New Size(133, 36)
        btnLoad.TabIndex = 8
        btnLoad.Text = "← Load List"
        btnLoad.UseVisualStyleBackColor = True
        ' 
        ' btnSave
        ' 
        btnSave.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnSave.Location = New Point(752, 486)
        btnSave.Margin = New Padding(4, 3, 4, 3)
        btnSave.Name = "btnSave"
        btnSave.Size = New Size(133, 36)
        btnSave.TabIndex = 7
        btnSave.Text = "→ Save List"
        btnSave.UseVisualStyleBackColor = True
        ' 
        ' btnRefresh
        ' 
        btnRefresh.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnRefresh.Location = New Point(752, 69)
        btnRefresh.Margin = New Padding(4, 3, 4, 3)
        btnRefresh.Name = "btnRefresh"
        btnRefresh.Size = New Size(134, 27)
        btnRefresh.TabIndex = 6
        btnRefresh.Text = "♲ Refresh"
        btnRefresh.UseVisualStyleBackColor = True
        ' 
        ' btnRemoveFolder
        ' 
        btnRemoveFolder.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnRemoveFolder.Location = New Point(751, 36)
        btnRemoveFolder.Margin = New Padding(4, 3, 4, 3)
        btnRemoveFolder.Name = "btnRemoveFolder"
        btnRemoveFolder.Size = New Size(134, 27)
        btnRemoveFolder.TabIndex = 5
        btnRemoveFolder.Text = "Remove Folder"
        btnRemoveFolder.UseVisualStyleBackColor = True
        ' 
        ' btnClrTvw
        ' 
        btnClrTvw.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnClrTvw.ForeColor = Color.Red
        btnClrTvw.Location = New Point(752, 102)
        btnClrTvw.Margin = New Padding(4, 3, 4, 3)
        btnClrTvw.Name = "btnClrTvw"
        btnClrTvw.Size = New Size(133, 27)
        btnClrTvw.TabIndex = 4
        btnClrTvw.Text = "Clear folder"
        btnClrTvw.UseVisualStyleBackColor = True
        ' 
        ' btnAddFolder
        ' 
        btnAddFolder.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnAddFolder.Location = New Point(752, 3)
        btnAddFolder.Margin = New Padding(4, 3, 4, 3)
        btnAddFolder.Name = "btnAddFolder"
        btnAddFolder.Size = New Size(133, 27)
        btnAddFolder.TabIndex = 3
        btnAddFolder.Text = "Add folder"
        btnAddFolder.UseVisualStyleBackColor = True
        ' 
        ' tvwFolders
        ' 
        tvwFolders.CheckBoxes = True
        tvwFolders.Dock = DockStyle.Fill
        tvwFolders.ImageIndex = 0
        tvwFolders.ImageList = TvwImageList
        tvwFolders.Location = New Point(4, 0)
        tvwFolders.Margin = New Padding(4, 3, 4, 3)
        tvwFolders.Name = "tvwFolders"
        tvwFolders.SelectedImageIndex = 0
        tvwFolders.Size = New Size(746, 526)
        tvwFolders.TabIndex = 2
        ' 
        ' TvwImageList
        ' 
        TvwImageList.ColorDepth = ColorDepth.Depth32Bit
        TvwImageList.ImageSize = New Size(16, 16)
        TvwImageList.TransparentColor = Color.Transparent
        ' 
        ' Splitter2
        ' 
        Splitter2.Dock = DockStyle.Right
        Splitter2.Location = New Point(750, 0)
        Splitter2.Margin = New Padding(4, 3, 4, 3)
        Splitter2.Name = "Splitter2"
        Splitter2.Size = New Size(139, 526)
        Splitter2.TabIndex = 1
        Splitter2.TabStop = False
        ' 
        ' Splitter1
        ' 
        Splitter1.Location = New Point(0, 0)
        Splitter1.Margin = New Padding(4, 3, 4, 3)
        Splitter1.Name = "Splitter1"
        Splitter1.Size = New Size(4, 526)
        Splitter1.TabIndex = 0
        Splitter1.TabStop = False
        ' 
        ' SQLString
        ' 
        SQLString.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        SQLString.Location = New Point(4, 117)
        SQLString.Margin = New Padding(4, 3, 4, 3)
        SQLString.Name = "SQLString"
        SQLString.PasswordChar = "*"c
        SQLString.Size = New Size(881, 23)
        SQLString.TabIndex = 11
        SQLString.Text = "Server=10.0.1.2;Database=Radio_Statistics;User Id=YTStats;Password=Y!s2ats;TrustServerCertificate=True;"
        ' 
        ' StatusStrip1
        ' 
        StatusStrip1.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        StatusStrip1.Dock = DockStyle.None
        StatusStrip1.Items.AddRange(New ToolStripItem() {ProgressBar1, StatusText, ToolStripDropDownButton1})
        StatusStrip1.Location = New Point(4, 209)
        StatusStrip1.Name = "StatusStrip1"
        StatusStrip1.Padding = New Padding(1, 0, 16, 0)
        StatusStrip1.Size = New Size(161, 24)
        StatusStrip1.TabIndex = 10
        StatusStrip1.Text = "StatusStrip1"
        ' 
        ' ProgressBar1
        ' 
        ProgressBar1.Name = "ProgressBar1"
        ProgressBar1.Size = New Size(117, 18)
        ' 
        ' StatusText
        ' 
        StatusText.DisplayStyle = ToolStripItemDisplayStyle.Text
        StatusText.Name = "StatusText"
        StatusText.Size = New Size(10, 19)
        StatusText.Text = "."
        ' 
        ' ToolStripDropDownButton1
        ' 
        ToolStripDropDownButton1.DisplayStyle = ToolStripItemDisplayStyle.Image
        ToolStripDropDownButton1.DropDownItems.AddRange(New ToolStripItem() {btnSaveSettings, btnLoadSettings})
        ToolStripDropDownButton1.ImageTransparentColor = Color.Magenta
        ToolStripDropDownButton1.Name = "ToolStripDropDownButton1"
        ToolStripDropDownButton1.Size = New Size(13, 22)
        ToolStripDropDownButton1.Text = "ToolStripDropDownButton1"
        ' 
        ' btnSaveSettings
        ' 
        btnSaveSettings.Name = "btnSaveSettings"
        btnSaveSettings.Size = New Size(145, 22)
        btnSaveSettings.Text = "Save Settings"
        ' 
        ' btnLoadSettings
        ' 
        btnLoadSettings.Name = "btnLoadSettings"
        btnLoadSettings.Size = New Size(145, 22)
        btnLoadSettings.Text = "Load Settings"
        ' 
        ' Timer1
        ' 
        Timer1.Interval = 1000
        ' 
        ' btnProcessCustom
        ' 
        btnProcessCustom.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnProcessCustom.Location = New Point(753, 257)
        btnProcessCustom.Margin = New Padding(4, 3, 4, 3)
        btnProcessCustom.Name = "btnProcessCustom"
        btnProcessCustom.Size = New Size(133, 36)
        btnProcessCustom.TabIndex = 17
        btnProcessCustom.Text = "⚙ Process Custom"
        btnProcessCustom.UseVisualStyleBackColor = True
        ' 
        ' YouTubeSearcher
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(889, 767)
        Controls.Add(SplitContainer1)
        Margin = New Padding(4, 3, 4, 3)
        Name = "YouTubeSearcher"
        Text = "Form1"
        SplitContainer1.Panel1.ResumeLayout(False)
        SplitContainer1.Panel2.ResumeLayout(False)
        SplitContainer1.Panel2.PerformLayout()
        CType(SplitContainer1, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer1.ResumeLayout(False)
        StatusStrip1.ResumeLayout(False)
        StatusStrip1.PerformLayout()
        ResumeLayout(False)

    End Sub

    Friend WithEvents srchString As TextBox
    Friend WithEvents logtxt As TextBox
    Friend WithEvents btnGetUrl As Button
    Friend WithEvents chkDebugMode As CheckBox
    Friend WithEvents btnGetStats As Button
    Friend WithEvents txtAPI As TextBox
    Friend WithEvents chkBasic As CheckBox
    Friend WithEvents chkAPI As CheckBox
    Friend WithEvents SplitContainer1 As SplitContainer
    Friend WithEvents StatusStrip1 As StatusStrip
    Friend WithEvents ProgressBar1 As ToolStripProgressBar
    Friend WithEvents StatusText As ToolStripStatusLabel
    Friend WithEvents Splitter2 As Splitter
    Friend WithEvents Splitter1 As Splitter
    Friend WithEvents tvwFolders As TreeView
    Friend WithEvents btnAddFolder As Button
    Friend WithEvents btnRemoveFolder As Button
    Friend WithEvents btnClrTvw As Button
    Friend WithEvents TvwImageList As ImageList
    Friend WithEvents btnRefresh As Button
    Friend WithEvents btnLoad As Button
    Friend WithEvents btnSave As Button
    Friend WithEvents btnGetSelected As Button
    Friend WithEvents btnUncheckSelected As Button
    Friend WithEvents btnCheckSelected As Button
    Friend WithEvents btnProcessALLMP3 As Button
    Friend WithEvents SetProcessTime As DateTimePicker
    Friend WithEvents Timer1 As Timer
    Friend WithEvents ToolStripDropDownButton1 As ToolStripDropDownButton
    Friend WithEvents btnSaveSettings As ToolStripMenuItem
    Friend WithEvents btnLoadSettings As ToolStripMenuItem
    Friend WithEvents btnSTOP As Button
    Friend WithEvents LangCombo As ComboBox
    Friend WithEvents SQLString As TextBox
    Friend WithEvents btnProcessCustom As Button
End Class
