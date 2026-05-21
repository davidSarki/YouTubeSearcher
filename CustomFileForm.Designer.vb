<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class CustomFileForm
    Inherits System.Windows.Forms.Form

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

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(CustomFileForm))
        lvwSongs = New ListView()
        colArtist = New ColumnHeader()
        colTitle = New ColumnHeader()
        colURL = New ColumnHeader()
        colFileName = New ColumnHeader()
        btnAddSong = New Button()
        btnProcess = New Button()
        btnRemove = New Button()
        btnClear = New Button()
        StatusStrip1 = New StatusStrip()
        ProgressBar1 = New ToolStripProgressBar()
        StatusText = New ToolStripStatusLabel()
        txtLog = New TextBox()
        btnEdit = New Button()
        StatusStrip1.SuspendLayout()
        SuspendLayout()
        ' 
        ' lvwSongs
        ' 
        lvwSongs.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        lvwSongs.CheckBoxes = True
        lvwSongs.Columns.AddRange(New ColumnHeader() {colArtist, colTitle, colURL, colFileName})
        lvwSongs.FullRowSelect = True
        lvwSongs.GridLines = True
        lvwSongs.Location = New Point(14, 53)
        lvwSongs.Margin = New Padding(4, 3, 4, 3)
        lvwSongs.Name = "lvwSongs"
        lvwSongs.Size = New Size(910, 537)
        lvwSongs.TabIndex = 0
        lvwSongs.UseCompatibleStateImageBehavior = False
        lvwSongs.View = View.Details
        ' 
        ' colArtist
        ' 
        colArtist.Text = "Artist"
        colArtist.Width = 150
        ' 
        ' colTitle
        ' 
        colTitle.Text = "Title"
        colTitle.Width = 200
        ' 
        ' colURL
        ' 
        colURL.Text = "URL"
        colURL.Width = 250
        ' 
        ' colFileName
        ' 
        colFileName.Text = "File Name"
        colFileName.Width = 250
        ' 
        ' btnAddSong
        ' 
        btnAddSong.Location = New Point(13, 12)
        btnAddSong.Margin = New Padding(4, 3, 4, 3)
        btnAddSong.Name = "btnAddSong"
        btnAddSong.Size = New Size(117, 35)
        btnAddSong.TabIndex = 1
        btnAddSong.Text = "Add Songs"
        btnAddSong.UseVisualStyleBackColor = True
        ' 
        ' btnProcess
        ' 
        btnProcess.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnProcess.Location = New Point(807, 12)
        btnProcess.Margin = New Padding(4, 3, 4, 3)
        btnProcess.Name = "btnProcess"
        btnProcess.Size = New Size(117, 35)
        btnProcess.TabIndex = 2
        btnProcess.Text = "Process"
        btnProcess.UseVisualStyleBackColor = True
        ' 
        ' btnRemove
        ' 
        btnRemove.Location = New Point(137, 12)
        btnRemove.Margin = New Padding(4, 3, 4, 3)
        btnRemove.Name = "btnRemove"
        btnRemove.Size = New Size(117, 35)
        btnRemove.TabIndex = 3
        btnRemove.Text = "Remove"
        btnRemove.UseVisualStyleBackColor = True
        ' 
        ' btnClear
        ' 
        btnClear.Location = New Point(260, 12)
        btnClear.Margin = New Padding(4, 3, 4, 3)
        btnClear.Name = "btnClear"
        btnClear.Size = New Size(117, 35)
        btnClear.TabIndex = 4
        btnClear.Text = "Clear All"
        btnClear.UseVisualStyleBackColor = True
        ' 
        ' StatusStrip1
        ' 
        StatusStrip1.Items.AddRange(New ToolStripItem() {ProgressBar1, StatusText})
        StatusStrip1.Location = New Point(0, 755)
        StatusStrip1.Name = "StatusStrip1"
        StatusStrip1.Padding = New Padding(1, 0, 16, 0)
        StatusStrip1.Size = New Size(938, 24)
        StatusStrip1.TabIndex = 5
        StatusStrip1.Text = "StatusStrip1"
        ' 
        ' ProgressBar1
        ' 
        ProgressBar1.Name = "ProgressBar1"
        ProgressBar1.Size = New Size(233, 18)
        ' 
        ' StatusText
        ' 
        StatusText.Name = "StatusText"
        StatusText.Size = New Size(39, 19)
        StatusText.Text = "Ready"
        ' 
        ' txtLog
        ' 
        txtLog.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        txtLog.Location = New Point(13, 595)
        txtLog.Margin = New Padding(4, 3, 4, 3)
        txtLog.Multiline = True
        txtLog.Name = "txtLog"
        txtLog.ReadOnly = True
        txtLog.ScrollBars = ScrollBars.Vertical
        txtLog.Size = New Size(911, 157)
        txtLog.TabIndex = 7
        ' 
        ' btnEdit
        ' 
        btnEdit.Location = New Point(385, 12)
        btnEdit.Margin = New Padding(4, 3, 4, 3)
        btnEdit.Name = "btnEdit"
        btnEdit.Size = New Size(117, 35)
        btnEdit.TabIndex = 8
        btnEdit.Text = "Edit Selected"
        btnEdit.UseVisualStyleBackColor = True
        ' 
        ' CustomFileForm
        ' 
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(938, 779)
        Controls.Add(btnEdit)
        Controls.Add(StatusStrip1)
        Controls.Add(btnClear)
        Controls.Add(btnRemove)
        Controls.Add(btnProcess)
        Controls.Add(btnAddSong)
        Controls.Add(lvwSongs)
        Controls.Add(txtLog)
        Icon = CType(resources.GetObject("$this.Icon"), Icon)
        Margin = New Padding(4, 3, 4, 3)
        Name = "CustomFileForm"
        Text = "Custom File Processor"
        StatusStrip1.ResumeLayout(False)
        StatusStrip1.PerformLayout()
        ResumeLayout(False)
        PerformLayout()

    End Sub

    Friend WithEvents lvwSongs As ListView
    Friend WithEvents colArtist As ColumnHeader
    Friend WithEvents colTitle As ColumnHeader
    Friend WithEvents colURL As ColumnHeader
    Friend WithEvents colFileName As ColumnHeader
    Friend WithEvents btnAddSong As Button
    Friend WithEvents btnProcess As Button
    Friend WithEvents btnRemove As Button
    Friend WithEvents btnClear As Button
    Friend WithEvents StatusStrip1 As StatusStrip
    Friend WithEvents ProgressBar1 As ToolStripProgressBar
    Friend WithEvents StatusText As ToolStripStatusLabel
    Friend WithEvents txtLog As TextBox
    Friend WithEvents btnEdit As Button
End Class