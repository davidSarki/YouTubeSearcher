' =====================================================
' FORM CLASS: YouTubeSearcher.vb (Enhanced with TreeView functionality)
' Contains form-related logic, UI interaction, and TreeView operations
' Requires: YouTubeSearchModule.vb, DirectoryTreeViewModule.vb
' =====================================================
'Imports YouTubeSearchModule
'Imports YouTubeStatsModule
'Imports DirectoryTreeViewModule
'Imports System.Windows.Forms
Imports System.IO
Imports System.Threading
Imports Newtonsoft.Json

Public Class YouTubeSearcher



    Private cancellationTokenSource As CancellationTokenSource = Nothing
    Private isProcessing As Boolean = False
    Private autoProcessingEnabled As Boolean = False

    ' Form Load - Initialize TreeView
    Private Sub YouTubeSearcher_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            Me.Text = $"YouTube Searcher - Version {Application.ProductVersion}"

            ' Initialize the TreeView (existing code)
            DirectoryTreeViewModule.InitializeDirectoryTreeView(tvwFolders, True)
            DirectoryTreeViewModule.SetTreeViewImageList(tvwFolders, TvwImageList)
            tvwFolders.CheckBoxes = True

            ' Set up TreeView event handlers (existing code)
            AddHandler tvwFolders.AfterSelect, AddressOf TreeView_AfterSelect
            AddHandler tvwFolders.AfterCheck, AddressOf DirectoryTreeViewModule.TreeView_AfterCheck

            ' Initially disable Remove button until a root folder is selected (existing code)
            btnRemoveFolder.Enabled = False
            ' Ensure STOP button is hidden on startup

            HideStopButton()
            ' Load saved settings and TreeView state
            LoadFormSettings()

            ConnectionString = SQLString.Text

            StatusText.Text = "Ready"
            logtxt.AppendText($"YouTube Searcher initialized{vbCrLf}")


        Catch ex As Exception
            logtxt.AppendText($"Error initializing form: {ex.Message}{vbCrLf}")
        End Try
    End Sub
    ''' <summary>
    ''' Enhanced Form Closing event - save all settings
    ''' </summary>
    Private Sub YouTubeSearcher_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        Try
            ' Save all current settings automatically
            If chkDebugMode.Checked Then
                logtxt.AppendText($"Saving settings before closing...{vbCrLf}")
            End If
            SaveFormSettings()

            ' Disable automatic processing
            autoProcessingEnabled = False
            If Timer1 IsNot Nothing Then
                Timer1.Enabled = False
                Timer1.Stop()
            End If

            ' Cancel any ongoing processing
            If cancellationTokenSource IsNot Nothing Then
                cancellationTokenSource.Cancel()
                cancellationTokenSource.Dispose()
                cancellationTokenSource = Nothing
            End If

            ' Cleanup modules
            CleanupYouTubeSearch()
            CleanupYouTubeStats()

            If chkDebugMode.Checked Then
                logtxt.AppendText($"Settings saved and cleanup completed{vbCrLf}")
            End If

        Catch ex As Exception
            ' Log error but don't prevent closing
            Try
                logtxt.AppendText($"Error during form closing: {ex.Message}{vbCrLf}")
            Catch
                ' If even logging fails, just continue closing
            End Try
        End Try
    End Sub

    ''' <summary>
    ''' Get the settings file path
    ''' </summary>
    Private Function GetSettingsFilePath() As String
        Dim appPath As String = Application.StartupPath
        Return Path.Combine(appPath, "YouTubeSearcherSettings.json")
    End Function

    ''' <summary>
    ''' Get the TreeView state file path
    ''' </summary>
    Private Function GetTreeViewStateFilePath() As String
        Dim appPath As String = Application.StartupPath
        Return Path.Combine(appPath, "TreeViewState.json")
    End Function

    ''' <summary>
    ''' Save all form settings to JSON file
    ''' </summary>
    Private Sub SaveFormSettings()
        Try
            Dim settings As New FormSettings()

            ' Save control states
            settings.ApiKey = txtAPI.Text.Trim()
            settings.UseAPI = chkAPI.Checked
            settings.UseBasic = chkBasic.Checked
            settings.DebugMode = chkDebugMode.Checked
            settings.AutoProcessingEnabled = SetProcessTime.Checked
            settings.ScheduledTime = SetProcessTime.Value
            settings.TreeViewStatePath = GetTreeViewStateFilePath()
            settings.langText = LangCombo.Text
            settings.SQLCpnnection = SQLString.Text

            ' Save settings to JSON
            Dim json As String = JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented)
            File.WriteAllText(GetSettingsFilePath(), json, System.Text.Encoding.UTF8)

            ' Save TreeView state
            DirectoryTreeViewModule.SaveTreeViewState(tvwFolders, ProgressBar1, StatusText, True)

            If chkDebugMode.Checked Then
                logtxt.AppendText($"Settings saved successfully{vbCrLf}")
            End If

        Catch ex As Exception
            logtxt.AppendText($"Error saving settings: {ex.Message}{vbCrLf}")
        End Try
    End Sub


    ''' <summary>
    ''' Load all form settings from JSON file
    ''' </summary>

    Private Sub LoadFormSettings()
        Try
            Dim settingsPath As String = GetSettingsFilePath()

            If Not File.Exists(settingsPath) Then
                logtxt.AppendText($"No saved settings found. Using defaults.{vbCrLf}")
                Return
            End If

            ' Load settings from JSON
            Dim json As String = File.ReadAllText(settingsPath, System.Text.Encoding.UTF8)
            Dim settings As FormSettings = JsonConvert.DeserializeObject(Of FormSettings)(json)

            If settings IsNot Nothing Then
                ' Restore control states
                txtAPI.Text = settings.ApiKey
                chkAPI.Checked = settings.UseAPI
                chkBasic.Checked = settings.UseBasic
                chkDebugMode.Checked = settings.DebugMode
                LangCombo.Text = settings.langText
                ' Restore scheduled time (but don't enable auto-processing yet)
                SetProcessTime.Value = settings.ScheduledTime
                SQLString.Text = settings.SQLCpnnection


                ' Load TreeView state automatically (no dialog)
                ' Load TreeView state if exists
                If File.Exists(GetTreeViewStateFilePath()) Then
                    DirectoryTreeViewModule.LoadTreeViewState(tvwFolders, ProgressBar1, StatusText, True)
                    logtxt.AppendText($"TreeView state loaded{vbCrLf}")
                End If

                ' IMPORTANT: Set autoProcessingEnabled BEFORE setting the checkbox
                ' This ensures the button state is correct from the start
                autoProcessingEnabled = False  ' Start with manual mode

                ' Now restore auto-processing if it was enabled
                If settings.AutoProcessingEnabled Then
                    ' This will trigger SetProcessTime_CheckedChanged and set correct button state
                    SetProcessTime.Checked = True
                    AutoprecessToggle() ' Ensure toggle logic runs
                    logtxt.AppendText($"Auto-processing restored: {settings.ScheduledTime:yyyy-MM-dd HH:mm:ss}{vbCrLf}")
                Else
                    ' Ensure manual processing button state is set correctly
                    SetProcessTime.Checked = False
                    AutoprecessToggle() ' Ensure toggle logic runs
                    'btnProcessALLMP3.Text = "Process Manually"
                    'btnProcessALLMP3.BackColor = SystemColors.Control
                    'btnProcessALLMP3.ForeColor = SystemColors.ControlText
                    'btnProcessALLMP3.UseVisualStyleBackColor = True
                    'btnProcessALLMP3.Enabled = True
                End If

                logtxt.AppendText($"Settings loaded from: {Path.GetFileName(settingsPath)}{vbCrLf}")
            End If

        Catch ex As Exception
            logtxt.AppendText($"Error loading settings: {ex.Message}{vbCrLf}")
        End Try
    End Sub
    ''' <summary>
    ''' Auto-save settings before auto-processing (called before each auto-process)
    ''' </summary>
    Private Sub AutoSaveBeforeProcessing()
        Try
            logtxt.AppendText($"Auto-saving settings before processing...{vbCrLf}")
            SaveFormSettings()
            logtxt.AppendText($"Auto-save completed{vbCrLf}")
        Catch ex As Exception
            logtxt.AppendText($"Error during auto-save: {ex.Message}{vbCrLf}")
        End Try
    End Sub



    Private Sub tvwFolders_MouseDown(sender As Object, e As MouseEventArgs) Handles tvwFolders.MouseDown
        DirectoryTreeViewModule.TreeView_MouseDown(sender, e)
    End Sub
    '  Bulk check/uncheck selected nodes
    Private Sub btnCheckSelected_Click(sender As Object, e As EventArgs) Handles btnCheckSelected.Click
        DirectoryTreeViewModule.CheckSelectedNodes(True)
    End Sub

    Private Sub btnUncheckSelected_Click(sender As Object, e As EventArgs) Handles btnUncheckSelected.Click
        DirectoryTreeViewModule.CheckSelectedNodes(False)
    End Sub

    '  Get selected node info
    Private Sub btnGetSelected_Click(sender As Object, e As EventArgs) Handles btnGetSelected.Click
        Dim selected As List(Of TreeNode) = DirectoryTreeViewModule.GetSelectedNodes()
        MessageBox.Show($"Selected {selected.Count} nodes")
    End Sub


    ' Add Folder Button Click Handler
    Private Sub btnAddFolder_Click(sender As Object, e As EventArgs) Handles btnAddFolder.Click
        Try
            Using folderDialog As New FolderBrowserDialog()
                folderDialog.Description = "Select a folder to add to the tree"
                folderDialog.ShowNewFolderButton = False

                If folderDialog.ShowDialog() = DialogResult.OK Then
                    Dim selectedPath As String = folderDialog.SelectedPath

                    ' Update status
                    StatusText.Text = "Adding folder..."
                    Application.DoEvents()

                    ' Add folder to TreeView (Append mode to keep existing folders)
                    AddFolderTreeView(tvwFolders, selectedPath, AddFolderMode.Append, ProgressBar1, StatusText)

                    logtxt.AppendText($"Added folder: {selectedPath}{vbCrLf}")
                    StatusText.Text = $"Added: {Path.GetFileName(selectedPath)}"
                End If
            End Using
        Catch ex As Exception
            logtxt.AppendText($"Error adding folder: {ex.Message}{vbCrLf}")
            StatusText.Text = "Error adding folder"
        End Try
    End Sub
    Private Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
        ' Refresh all
        DirectoryTreeViewModule.RefreshElement(tvwFolders, 0, ProgressBar1, StatusText)
    End Sub
    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        DirectoryTreeViewModule.SaveTreeViewState(tvwFolders, ProgressBar1, StatusText)
    End Sub

    Private Sub btnLoad_Click(sender As Object, e As EventArgs) Handles btnLoad.Click
        DirectoryTreeViewModule.LoadTreeViewState(tvwFolders, ProgressBar1, StatusText)
    End Sub

    ' Clear TreeView Button Click Handler
    Private Sub btnClrTvw_Click(sender As Object, e As EventArgs) Handles btnClrTvw.Click
        Try
            If tvwFolders.Nodes.Count = 0 Then
                logtxt.AppendText($"TreeView is already empty{vbCrLf}")
                Return
            End If

            ' Confirm before clearing
            Dim result As DialogResult = MessageBox.Show(
                "Are you sure you want to clear all folders from the TreeView?",
                "Confirm Clear",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question)

            If result = DialogResult.Yes Then
                tvwFolders.Nodes.Clear()
                btnRemoveFolder.Enabled = False
                logtxt.AppendText($"TreeView cleared{vbCrLf}")
                StatusText.Text = "TreeView cleared"
            End If

        Catch ex As Exception
            logtxt.AppendText($"Error clearing TreeView: {ex.Message}{vbCrLf}")
            StatusText.Text = "Error clearing TreeView"
        End Try
    End Sub

    ' Remove Root Folder Button Click Handler
    Private Sub btnRemoveFolder_Click(sender As Object, e As EventArgs) Handles btnRemoveFolder.Click
        Try
            If tvwFolders.SelectedNode Is Nothing Then
                logtxt.AppendText($"Please select a root folder to remove{vbCrLf}")
                Return
            End If

            ' Check if selected node is a root node
            If tvwFolders.SelectedNode.Parent IsNot Nothing Then
                logtxt.AppendText($"Please select a root folder (not a subfolder) to remove{vbCrLf}")
                Return
            End If

            ' Get the index of the selected root node
            Dim selectedIndex As Integer = tvwFolders.Nodes.IndexOf(tvwFolders.SelectedNode)
            Dim folderName As String = tvwFolders.SelectedNode.Text

            ' Confirm before removing
            Dim result As DialogResult = MessageBox.Show(
                $"Are you sure you want to remove the folder '{folderName}' from the TreeView?",
                "Confirm Remove",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question)

            If result = DialogResult.Yes Then
                ' Remove the root element using the module function
                RemoveElement(tvwFolders, selectedIndex)

                logtxt.AppendText($"Removed root folder: {folderName}{vbCrLf}")
                StatusText.Text = $"Removed: {folderName}"

                ' Disable remove button if no more nodes
                If tvwFolders.Nodes.Count = 0 Then
                    btnRemoveFolder.Enabled = False
                End If
            End If

        Catch ex As Exception
            logtxt.AppendText($"Error removing folder: {ex.Message}{vbCrLf}")
            StatusText.Text = "Error removing folder"
        End Try
    End Sub

    ' TreeView Node Selection Handler
    Private Sub TreeView_AfterSelect(sender As Object, e As TreeViewEventArgs)
        Try
            ' Enable/disable Remove button based on selection
            If e.Node IsNot Nothing AndAlso e.Node.Parent Is Nothing Then
                ' Root node selected - enable remove button
                btnRemoveFolder.Enabled = True
                StatusText.Text = $"Selected: {e.Node.Text} (Root folder)"
            Else
                ' Child node or no selection - disable remove button
                btnRemoveFolder.Enabled = False
                If e.Node IsNot Nothing Then
                    StatusText.Text = $"Selected: {e.Node.Text}"
                Else
                    StatusText.Text = "Ready"
                End If
            End If

        Catch ex As Exception
            logtxt.AppendText($"Error in TreeView selection: {ex.Message}{vbCrLf}")
        End Try
    End Sub

    ' Refresh TreeView (you can add a button for this or call it when needed)
    Private Sub RefreshTreeView()
        Try
            StatusText.Text = "Refreshing TreeView..."
            Application.DoEvents()

            ' Refresh all root elements (index 0 means all)
            RefreshElement(tvwFolders, 0)

            logtxt.AppendText($"TreeView refreshed{vbCrLf}")
            StatusText.Text = "TreeView refreshed"

        Catch ex As Exception
            logtxt.AppendText($"Error refreshing TreeView: {ex.Message}{vbCrLf}")
            StatusText.Text = "Error refreshing TreeView"
        End Try
    End Sub

    ' Save TreeView State (you can add a button for this or call it when needed)
    Private Sub SaveTreeViewState()
        Try
            Using saveDialog As New SaveFileDialog()
                saveDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
                saveDialog.Title = "Save TreeView State"
                saveDialog.DefaultExt = "json"

                If saveDialog.ShowDialog() = DialogResult.OK Then
                    SaveTreeViewState()
                    logtxt.AppendText($"TreeView state saved to: {saveDialog.FileName}{vbCrLf}")
                    StatusText.Text = "TreeView state saved"
                End If
            End Using

        Catch ex As Exception
            logtxt.AppendText($"Error saving TreeView state: {ex.Message}{vbCrLf}")
            StatusText.Text = "Error saving TreeView state"
        End Try
    End Sub

    ' Load TreeView State (you can add a button for this or call it when needed)
    Private Sub LoadTreeViewState()
        Try
            Using openDialog As New OpenFileDialog()
                openDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
                openDialog.Title = "Load TreeView State"

                If openDialog.ShowDialog() = DialogResult.OK Then
                    LoadTreeViewState()
                    logtxt.AppendText($"TreeView state loaded from: {openDialog.FileName}{vbCrLf}")
                    StatusText.Text = "TreeView state loaded"

                    ' Update button states
                    btnRemoveFolder.Enabled = False
                End If
            End Using

        Catch ex As Exception
            logtxt.AppendText($"Error loading TreeView state: {ex.Message}{vbCrLf}")
            StatusText.Text = "Error loading TreeView state"
        End Try
    End Sub

    ' Get Selected Files/Folders (helper method for future use)
    Public Function GetSelectedItems() As List(Of String)
        Dim selectedItems As New List(Of String)

        Try
            ' Recursively collect all checked items
            CollectCheckedItems(tvwFolders.Nodes, selectedItems)

        Catch ex As Exception
            logtxt.AppendText($"Error getting selected items: {ex.Message}{vbCrLf}")
        End Try

        Return selectedItems
    End Function

    ' Recursively collect checked items
    Private Sub CollectCheckedItems(nodes As TreeNodeCollection, selectedItems As List(Of String))
        For Each node As TreeNode In nodes
            If node.Checked Then
                Dim dirNode As DirectoryTreeNode = TryCast(node, DirectoryTreeNode)
                If dirNode IsNot Nothing Then
                    selectedItems.Add(dirNode.FileFullPath)
                End If
            End If

            ' Recursively check child nodes
            If node.Nodes.Count > 0 Then
                CollectCheckedItems(node.Nodes, selectedItems)
            End If
        Next
    End Sub

    ' Right-click context menu for TreeView (optional enhancement)
    Private Sub SetupTreeViewContextMenu()
        Try
            Dim contextMenu As New ContextMenuStrip()

            ' Add menu items
            Dim refreshItem As New ToolStripMenuItem("Refresh", Nothing, AddressOf RefreshContextMenu_Click)
            Dim expandAllItem As New ToolStripMenuItem("Expand All", Nothing, AddressOf ExpandAllContextMenu_Click)
            Dim collapseAllItem As New ToolStripMenuItem("Collapse All", Nothing, AddressOf CollapseAllContextMenu_Click)
            Dim separator1 As New ToolStripSeparator()
            Dim saveStateItem As New ToolStripMenuItem("Save State", Nothing, AddressOf SaveStateContextMenu_Click)
            Dim loadStateItem As New ToolStripMenuItem("Load State", Nothing, AddressOf LoadStateContextMenu_Click)

            contextMenu.Items.AddRange({refreshItem, expandAllItem, collapseAllItem, separator1, saveStateItem, loadStateItem})

            ' Assign context menu to TreeView
            tvwFolders.ContextMenuStrip = contextMenu

        Catch ex As Exception
            logtxt.AppendText($"Error setting up context menu: {ex.Message}{vbCrLf}")
        End Try
    End Sub

    ' Context menu event handlers
    Private Sub RefreshContextMenu_Click(sender As Object, e As EventArgs)
        RefreshTreeView()
    End Sub

    Private Sub ExpandAllContextMenu_Click(sender As Object, e As EventArgs)
        tvwFolders.ExpandAll()
        StatusText.Text = "All nodes expanded"
    End Sub

    Private Sub CollapseAllContextMenu_Click(sender As Object, e As EventArgs)
        tvwFolders.CollapseAll()
        StatusText.Text = "All nodes collapsed"
    End Sub

    Private Sub SaveStateContextMenu_Click(sender As Object, e As EventArgs)
        SaveTreeViewState()
    End Sub
    Private Sub LoadStateContextMenu_Click(sender As Object, e As EventArgs)
        LoadTreeViewState()
    End Sub

    ' =================== EXISTING YOUTUBE SEARCH CODE ===================
    ' (Keep all your existing YouTube search functionality below)

    ' Button click event handler
    Private Async Sub btnGetUrl_Click(sender As Object, e As EventArgs) Handles btnGetUrl.Click
        Try
            If String.IsNullOrWhiteSpace(srchString.Text) Then
                logtxt.AppendText($"Please enter a search string.{vbCrLf}")
                Return
            End If

            btnGetUrl.Enabled = False
            logtxt.AppendText($"=== Search Started ==={vbCrLf}")

            ' Create logging action to pass to module
            Dim logAction As Action(Of String) = Sub(message As String)
                                                     logtxt.AppendText(message)
                                                 End Sub

            If chkBasic.Checked Then
                'Call module function with logging And debug mode
                Dim searchResult As YouTubeSearchResult = Await SearchYouTubeBasic(
               srchString.Text.Trim(),
              logAction,
               chkDebugMode.CheckState
               )

                ' Show search results 
                showResults(searchResult, " BASIC ")
            End If

            If chkAPI.Checked Then
                Dim apikey As String = txtAPI.Text
                Dim apiserachresult As YouTubeSearchResult = Await SearchYouTubeAPI(
                    srchString.Text.Trim(), apikey,
                    logAction,
                    chkDebugMode.CheckState
                )

                ' Show search results 
                showResults(apiserachresult, " API ")
            End If

            logtxt.AppendText($"=== Search Completed ==={vbCrLf}{vbCrLf}")

        Catch ex As Exception
            logtxt.AppendText($"Error in search process: {ex.Message}{vbCrLf}")
        Finally
            btnGetUrl.Enabled = True
        End Try
    End Sub

    ' Form cleanup - properly dispose resources when form closes
    Private Sub YouTubeSearcher_FormClosed(sender As Object, e As FormClosedEventArgs) Handles Me.FormClosed
        CleanupYouTubeSearch()
        CleanupYouTubeStats()
    End Sub

    Private Sub showResults(searchresult As YouTubeSearchResult, srchmode As String)
        ' Display all results
        If searchresult.AllResults.Count > 0 Then
            logtxt.AppendText($"{vbCrLf}=== ALL {srchmode} RESULTS ==={vbCrLf}")
            logtxt.AppendText($"VideoID | TITLE | Channel | YouTubeLink{vbCrLf}")
            logtxt.AppendText($"{New String("-"c, 80)}{vbCrLf}")

            For i As Integer = 0 To searchresult.AllResults.Count - 1
                Dim video As YouTubeVideoData = searchresult.AllResults(i)
                logtxt.AppendText($"{video.VideoId} | {video.Title} | {video.Channel} | {video.URL}{vbCrLf}")
            Next

            ' Display best match
            If searchresult.BestMatch IsNot Nothing Then
                logtxt.AppendText($"{vbCrLf}=== BEST MATCH ==={vbCrLf}")
                logtxt.AppendText($"Selected: {searchresult.BestMatch.Title} by {searchresult.BestMatch.Channel}{vbCrLf}")
                logtxt.AppendText($"VideoID: {searchresult.BestMatch.VideoId}{vbCrLf}")
                logtxt.AppendText($"URL: {searchresult.BestMatch.URL}{vbCrLf}")
            Else
                logtxt.AppendText($"{vbCrLf}=== BEST MATCH ==={vbCrLf}")
                logtxt.AppendText($"No video met the matching criteria (≥80% title match + ≥50% artist match){vbCrLf}")
            End If
        Else
            logtxt.AppendText($"No results found for: {srchString.Text}{vbCrLf}")
        End If
    End Sub

    Private Async Sub btnGetStats_Click(sender As Object, e As EventArgs) Handles btnGetStats.Click
        Try
            If String.IsNullOrWhiteSpace(srchString.Text) Then
                logtxt.AppendText($"Please enter a search string.{vbCrLf}")
                Return
            End If

            Dim videoid As String = srchString.Text.Trim()
            btnGetStats.Enabled = False
            logtxt.AppendText($"=== Search started for VideoID: {videoid} ==={vbCrLf}")

            ' Create logging action
            Dim logAction As Action(Of String) = Sub(message As String)
                                                     logtxt.AppendText(message)
                                                 End Sub

            ' Initialize stats module
            InitializeYouTubeStats()

            If chkBasic.Checked Then
                ' Method 1: Web Scraping
                logtxt.AppendText($"{vbCrLf}--- Method 1: Web Scraping ---{vbCrLf}")
                Dim scrapingStats As YTStatsRecord = Await GetYouTubeVideoStats(videoid, logAction, chkDebugMode.CheckState)

                If scrapingStats IsNot Nothing Then
                    logtxt.AppendText($"Scraping - Publish: {scrapingStats.PublishDate:yyyy-MM-dd}, Views: {scrapingStats.ViewCount:N0}{vbCrLf}")
                Else
                    logtxt.AppendText($"Scraping method failed{vbCrLf}")
                End If
            End If

            If chkAPI.Checked Then
                Dim apikey As String = txtAPI.Text
                ' Method 2: YouTube Data API
                logtxt.AppendText($"{vbCrLf}--- Method 2: YouTube Data API ---{vbCrLf}")
                Dim apiStats As YTStatsRecord = Await GetYouTubeVideoStatsAPI(videoid, apikey, logAction, chkDebugMode.CheckState)

                If apiStats IsNot Nothing Then
                    logtxt.AppendText($"API - Publish: {apiStats.PublishDate:yyyy-MM-dd}, Views: {apiStats.ViewCount:N0}{vbCrLf}")
                Else
                    logtxt.AppendText($"API method failed{vbCrLf}")
                End If
            End If

        Catch ex As Exception
            logtxt.AppendText($"Error testing stats: {ex.Message}{vbCrLf}")
        Finally
            btnGetStats.Enabled = True
        End Try
    End Sub

    Private Sub btnProcessCustom_Click(sender As Object, e As EventArgs) Handles btnProcessCustom.Click
        Try
            Dim customForm As New CustomFileForm()
            customForm.UseAPI = chkAPI.Checked
            customForm.ApiKey = txtAPI.Text.Trim()
            customForm.DebugMode = chkDebugMode.Checked
            customForm.Language = LangCombo.Text
            customForm.ShowDialog()
        Catch ex As Exception
            MessageBox.Show($"Error opening custom processor: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

#Region "Process MANUAl and AUTOMATIC"
    Private Async Sub btnProcessAllMP3_Click(sender As Object, e As EventArgs) Handles btnProcessALLMP3.Click
        Try
            ' Check if auto-processing is active - if so, don't allow manual processing
            If autoProcessingEnabled Then
                MessageBox.Show("Automatic processing is currently active. Please disable auto-processing first.",
                          "Auto-Processing Active", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' Check if already processing
            If isProcessing Then
                MessageBox.Show("Processing is already running. Use the STOP button to cancel.",
                          "Processing Active", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' START PROCESSING
            isProcessing = True
            cancellationTokenSource = New CancellationTokenSource

            ' Show STOP button and update main button
            ShowStopButton()
            btnProcessALLMP3.Text = "Processing..."
            btnProcessALLMP3.Enabled = False

            ' Disable other buttons during processing
            btnGetUrl.Enabled = False
            btnGetStats.Enabled = False

            ' Clear log
            logtxt.Clear()

            ' Create logging action
            Dim logAction As Action(Of String) = Sub(message)
                                                     If InvokeRequired Then
                                                         Invoke(Sub() logtxt.AppendText(message))
                                                     Else
                                                         logtxt.AppendText(message)
                                                     End If
                                                 End Sub

            ' Validate API key if API mode is selected
            If chkAPI.Checked AndAlso String.IsNullOrWhiteSpace(txtAPI.Text) Then
                MessageBox.Show("Please enter a valid YouTube API key when using API mode.",
                          "API Key Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            ' Confirm processing
            Dim checkedCount = CountCheckedMP3Files()

            If checkedCount = 0 Then
                MessageBox.Show("No checked MP3 files found in the TreeView.",
                          "No Files Selected", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim result = MessageBox.Show(
            $"This will process {checkedCount} checked MP3 files.{vbCrLf}{vbCrLf}" &
            $"Mode: {If(chkAPI.Checked, "API with fallback", "Basic scraping only")}{vbCrLf}" &
            $"This may take several minutes.{vbCrLf}{vbCrLf}" &
            $"You can stop the process at any time by clicking the STOP button.{vbCrLf}{vbCrLf}" &
            $"Do you want to do ALIAS too (press YES if you want to do ALIAS, press Cancel to Cancel operation) ?",
            "Confirm Processing",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question)

            If result = DialogResult.Cancel Then Return

            logAction($"User confirmed processing of {checkedCount} MP3 files{vbCrLf}")
            logAction($"Click 'STOP' button to cancel at any time{vbCrLf}{vbCrLf}")

            ' Start processing
            StatusText.Text = "Processing MP3 files..."

            Try
                Dim processingResults = Await ProcessAllCheckedMP3Files(
                tvwFolders,
                chkAPI.Checked,
                txtAPI.Text.Trim,
                logAction,
                ProgressBar1,
                StatusText,
                chkDebugMode.Checked,
                cancellationTokenSource.Token,
                LangCombo.Text,
                IIf(result = DialogResult.No, False, True)
            )

                ' Show final results (only if not cancelled)
                If Not cancellationTokenSource.Token.IsCancellationRequested Then
                    MessageBox.Show(
                                $"Processing Complete!{vbCrLf}{vbCrLf}" &
                                $"PHASE 1 - MP3 Files:{vbCrLf}" &
                                $"Total files: {processingResults.TotalFiles}{vbCrLf}" &
                                $"Successfully processed: {processingResults.ProcessedFiles}{vbCrLf}" &
                                $"New records created: {processingResults.NewRecords}{vbCrLf}" &
                                $"Stats updated: {processingResults.StatsUpdated}{vbCrLf}" &
                                $"{vbCrLf}" &
                                $"PHASE 2 - Songs without ALIAS:{vbCrLf}" &
                                $"Found without ALIAS: {processingResults.TotalMissingAlias}{vbCrLf}" &
                                $"Stats updated: {processingResults.AliasRecordsFound}{vbCrLf}" &
                                $"{vbCrLf}" &
                                $"Total stats updates: {processingResults.StatsUpdated + processingResults.AliasRecordsFound}{vbCrLf}" &
                                $"Not found/errors: {processingResults.NotFoundFiles + processingResults.ErrorFiles}",
                                "Processing Results",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information)
                End If

            Catch ex As OperationCanceledException
                ' Processing was cancelled
                logAction($"{vbCrLf}*** PROCESSING CANCELLED BY USER ***{vbCrLf}")
                StatusText.Text = "Processing cancelled by user"
                MessageBox.Show("Processing was cancelled by user.", "Processing Cancelled",
                          MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Try

        Catch ex As Exception
            MessageBox.Show($"Error during processing: {ex.Message}",
                       "Processing Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            If logtxt.InvokeRequired Then
                logtxt.Invoke(Sub() logtxt.AppendText($"FATAL ERROR: {ex.Message}{vbCrLf}"))
            Else
                logtxt.AppendText($"FATAL ERROR: {ex.Message}{vbCrLf}")
            End If
        Finally
            ' Always reset processing state and button appearance
            isProcessing = False
            HideStopButton()

            ' Reset main button to normal state (unless auto-processing is active)
            If autoProcessingEnabled Then
                btnProcessALLMP3.Text = "Auto-Processing Active"
                btnProcessALLMP3.BackColor = Color.Orange
                btnProcessALLMP3.ForeColor = Color.White
                btnProcessALLMP3.Enabled = False
                StatusText.Text = "Auto-processing: Waiting for next scheduled time..."
            Else
                btnProcessALLMP3.Text = "↨ Process Manually"
                btnProcessALLMP3.BackColor = SystemColors.Control
                btnProcessALLMP3.ForeColor = SystemColors.ControlText
                btnProcessALLMP3.UseVisualStyleBackColor = True
                btnProcessALLMP3.Enabled = True
                StatusText.Text = "Ready"
            End If

            ' Always re-enable other buttons
            btnGetUrl.Enabled = True
            btnGetStats.Enabled = True

            ' Dispose cancellation token
            If cancellationTokenSource IsNot Nothing Then
                cancellationTokenSource.Dispose()
                cancellationTokenSource = Nothing
            End If
        End Try
    End Sub

    ' ==============================================================
    ' STEP 4: Add helper methods for STOP button visibility
    ' ==============================================================

    ''' <summary>
    ''' Show the STOP button when processing starts
    ''' </summary>
    Private Sub ShowStopButton()
        btnSTOP.Visible = True
        btnSTOP.Enabled = True
        btnSTOP.Text = "STOP"
        btnSTOP.BackColor = Color.Red
        btnSTOP.ForeColor = Color.White
    End Sub

    ''' <summary>
    ''' Hide the STOP button when processing ends
    ''' </summary>
    Private Sub HideStopButton()
        btnSTOP.Visible = False
        btnSTOP.Enabled = True  ' Reset for next time
        btnSTOP.Text = "STOP"
        btnSTOP.BackColor = Color.Red
        btnSTOP.ForeColor = Color.White
    End Sub
    ' =========================== AUTOMATIC PROCESSING ===========================

    ' Event handler for the SetProcessTime checkbox
    Private Sub SetProcessTime_CheckedChanged(sender As Object, e As EventArgs) Handles SetProcessTime.ValueChanged
        AutoprecessToggle()
    End Sub
    Private Sub AutoprecessToggle()
        Try
            If SetProcessTime.Checked Then
                ' Enable automatic processing
                autoProcessingEnabled = True
                Timer1.Enabled = True
                Timer1.Start()

                logtxt.AppendText($"=== AUTOMATIC PROCESSING ENABLED ==={vbCrLf}")
                logtxt.AppendText($"Scheduled time: {SetProcessTime.Value:yyyy-MM-dd HH:mm:ss}{vbCrLf}")
                logtxt.AppendText($"Timer started - checking every second{vbCrLf}{vbCrLf}")

                StatusText.Text = $"Auto-processing scheduled for {SetProcessTime.Value:HH:mm:ss}"

                ' Set auto-processing button state (only if not currently processing)
                If Not isProcessing Then
                    btnProcessALLMP3.Text = "⧗ Auto-Processing"
                    btnProcessALLMP3.BackColor = Color.Orange
                    btnProcessALLMP3.ForeColor = Color.White
                    btnProcessALLMP3.Enabled = False
                End If

            Else
                ' Disable automatic processing
                autoProcessingEnabled = False
                Timer1.Enabled = False
                Timer1.Stop()

                logtxt.AppendText($"=== AUTOMATIC PROCESSING DISABLED ==={vbCrLf}")
                logtxt.AppendText($"Timer stopped{vbCrLf}{vbCrLf}")

                StatusText.Text = "Auto-processing disabled"

                ' ALWAYS restore manual processing button state when disabling auto-processing
                ' (regardless of current processing state)
                If Not isProcessing Then
                    btnProcessALLMP3.Text = "↨ Process Manually"
                    btnProcessALLMP3.BackColor = SystemColors.Control
                    btnProcessALLMP3.ForeColor = SystemColors.ControlText
                    btnProcessALLMP3.UseVisualStyleBackColor = True
                    btnProcessALLMP3.Enabled = True
                End If
            End If

        Catch ex As Exception
            MessageBox.Show($"Error setting up automatic processing: {ex.Message}",
                       "Auto-Processing Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
    ' Timer event handler - checks if it's time to process
    Private Async Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        Try
            ' Only proceed if auto-processing is enabled and we're not already processing
            If Not autoProcessingEnabled OrElse isProcessing Then
                Return
            End If

            ' Check if current time matches the scheduled time (within 1 second tolerance)
            Dim currentTime As DateTime = DateTime.Now
            Dim scheduledTime As DateTime = SetProcessTime.Value

            ' Create today's scheduled time from the DateTimePicker
            Dim todayScheduled As New DateTime(currentTime.Year, currentTime.Month, currentTime.Day,
                                          scheduledTime.Hour, scheduledTime.Minute, scheduledTime.Second)

            ' Check if we're within 1 second of the scheduled time
            Dim timeDifferenceSeconds As Double = Math.Abs((currentTime - todayScheduled).TotalSeconds)

            If timeDifferenceSeconds <= 1.0 Then
                ' It's time to process!
                logtxt.AppendText($"=== AUTOMATIC PROCESSING TRIGGERED ==={vbCrLf}")
                logtxt.AppendText($"Scheduled time reached: {currentTime:yyyy-MM-dd HH:mm:ss}{vbCrLf}")
                logtxt.AppendText($"Starting automatic refresh and processing...{vbCrLf}{vbCrLf}")

                ' Temporarily stop the timer to prevent multiple triggers
                Timer1.Enabled = False

                Await PerformAutomaticProcessing()

                ' Re-enable timer for next day (if auto-processing is still enabled)
                If autoProcessingEnabled Then
                    Timer1.Enabled = True

                    ' Calculate next processing time (tomorrow at the same time)
                    Dim nextProcessTime As DateTime = todayScheduled.AddDays(1)
                    logtxt.AppendText($"Next automatic processing scheduled for: {nextProcessTime:yyyy-MM-dd HH:mm:ss}{vbCrLf}{vbCrLf}")
                    StatusText.Text = $"Next auto-processing: {nextProcessTime:yyyy-MM-dd HH:mm:ss}"
                End If
            Else
                ' Update status with countdown
                Dim timeUntilProcess As TimeSpan = todayScheduled.Subtract(currentTime)
                If timeUntilProcess.TotalSeconds < 0 Then
                    ' Scheduled time has passed today, show tomorrow's time
                    timeUntilProcess = todayScheduled.AddDays(1).Subtract(currentTime)
                End If

                StatusText.Text = $"Auto-processing in: {timeUntilProcess:hh\:mm\:ss}"
            End If

        Catch ex As Exception
            logtxt.AppendText($"ERROR in Timer1_Tick: {ex.Message}{vbCrLf}")
            Timer1.Enabled = False ' Stop timer on error
            autoProcessingEnabled = False
            SetProcessTime.Checked = False
        End Try
    End Sub

    ''' <summary>
    ''' Enhanced Perform Automatic Processing - with auto-save
    ''' </summary>
    Private Async Function PerformAutomaticProcessing() As Task
        Try
            isProcessing = True
            ' Show STOP button for auto-processing too
            ShowStopButton()

            ' AUTO-SAVE BEFORE PROCESSING
            AutoSaveBeforeProcessing()

            ' Update UI to show automatic processing is active
            btnProcessALLMP3.Text = "⧗ Auto-Processing"
            btnProcessALLMP3.BackColor = Color.Blue
            btnProcessALLMP3.ForeColor = Color.White

            StatusText.Text = "Auto-processing: Refreshing folders..."

            logtxt.Clear()
            ' STEP 1: Refresh all folders first
            logtxt.AppendText($"Step 1: Refreshing all folders...{vbCrLf}")

            DirectoryTreeViewModule.RefreshElement(tvwFolders, 0, ProgressBar1, StatusText)

            logtxt.AppendText($"Folder refresh completed{vbCrLf}")
            logtxt.AppendText($"Step 2: Starting MP3 processing...{vbCrLf}{vbCrLf}")

            ' STEP 2: ↨ Process Manually
            StatusText.Text = "Auto-processing: Processing MP3 files..."

            ' Create logging action for automatic processing
            Dim logAction As Action(Of String) = Sub(message As String)
                                                     If Me.InvokeRequired Then
                                                         Me.Invoke(Sub() logtxt.AppendText(message))
                                                     Else
                                                         logtxt.AppendText(message)
                                                     End If
                                                 End Sub

            ' Create cancellation token for automatic processing
            cancellationTokenSource = New CancellationTokenSource()

            ' Validate API key if API mode is selected
            If chkAPI.Checked AndAlso String.IsNullOrWhiteSpace(txtAPI.Text) Then
                logtxt.AppendText($"WARNING: API mode selected but no API key provided. Using basic mode.{vbCrLf}")
            End If

            ' Start automatic processing
            Dim processingResults As ProcessingResults = Await GeneralModule.ProcessAllCheckedMP3Files(
            tvwFolders,
            chkAPI.Checked AndAlso Not String.IsNullOrWhiteSpace(txtAPI.Text),
            txtAPI.Text.Trim(),
            logAction,
            ProgressBar1,
            StatusText,
            chkDebugMode.Checked,
            cancellationTokenSource.Token,
            LangCombo.Text
        )

            ' Log automatic processing results
            logtxt.AppendText($"=== AUTOMATIC PROCESSING COMPLETED ==={vbCrLf}")
            logtxt.AppendText($"MP3 Processing:{vbCrLf}")
            logtxt.AppendText($"Total files: {processingResults.TotalFiles}{vbCrLf}")
            logtxt.AppendText($"Successfully processed: {processingResults.ProcessedFiles}{vbCrLf}")
            logtxt.AppendText($"Existing records found: {processingResults.ExistingRecords}{vbCrLf}")
            logtxt.AppendText($"New records created: {processingResults.NewRecords}{vbCrLf}")
            logtxt.AppendText($"Stats updated: {processingResults.StatsUpdated}{vbCrLf}")
            logtxt.AppendText($"Not found on YouTube: {processingResults.NotFoundFiles}{vbCrLf}")
            logtxt.AppendText($"Errors: {processingResults.ErrorFiles}{vbCrLf}")
            logtxt.AppendText($"Database Status:{vbCrLf}")
            logtxt.AppendText($"YT_Songs without ALIAS: {processingResults.TotalMissingAlias}{vbCrLf}")
            logtxt.AppendText($"Automatic processing finished at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{vbCrLf}{vbCrLf}")

            ' AUTO-SAVE AFTER PROCESSING (in case TreeView state changed)
            logtxt.AppendText($"Auto-saving updated state after processing...{vbCrLf}")
            SaveFormSettings()

        Catch ex As OperationCanceledException
            logtxt.AppendText($"Automatic processing was cancelled{vbCrLf}")
        Catch ex As Exception
            logtxt.AppendText($"ERROR during automatic processing: {ex.Message}{vbCrLf}")
        Finally
            ' Hide STOP button when auto-processing ends
            HideStopButton()
            isProcessing = False

            ' Cleanup
            If cancellationTokenSource IsNot Nothing Then
                cancellationTokenSource.Dispose()
                cancellationTokenSource = Nothing
            End If

            isProcessing = False

            ' Reset button appearance for auto-processing mode
            If autoProcessingEnabled Then
                btnProcessALLMP3.Text = "⧗ Auto-Processing"
                btnProcessALLMP3.BackColor = Color.Orange
                btnProcessALLMP3.ForeColor = Color.White
                StatusText.Text = "Auto-processing: Waiting for next scheduled time..."
            End If
        End Try
    End Function

    ' Helper function to count checked MP3 files
    Private Function CountCheckedMP3Files() As Integer
        Dim count As Integer = 0

        For Each rootNode As TreeNode In tvwFolders.Nodes
            count += CountCheckedMP3FilesInNode(rootNode)
        Next

        Return count
    End Function

    Private Function CountCheckedMP3FilesInNode(node As TreeNode) As Integer
        Dim count As Integer = 0

        Dim dirNode As DirectoryTreeViewModule.DirectoryTreeNode = TryCast(node, DirectoryTreeViewModule.DirectoryTreeNode)

        If dirNode IsNot Nothing AndAlso dirNode.Checked Then
            If Not dirNode.IsFolder AndAlso dirNode.FileFullPath.ToLower().EndsWith(".mp3") Then
                count += 1
            End If
        End If

        ' Recursively check child nodes
        For Each childNode As TreeNode In node.Nodes
            count += CountCheckedMP3FilesInNode(childNode)
        Next

        Return count
    End Function

    Private Sub btnSaveSettings_Click(sender As Object, e As EventArgs) Handles btnSaveSettings.Click
        Try
            SaveFormSettings()
            MessageBox.Show("Settings saved successfully!", "Settings Saved",
                       MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show($"Error saving settings: {ex.Message}", "Save Error",
                       MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>
    ''' Add manual load button event (optional - you can add a load button to the form)
    ''' </summary>
    Private Sub btnLoadSettings_Click(sender As Object, e As EventArgs) Handles btnLoadSettings.Click
        Try
            LoadFormSettings()
            MessageBox.Show("Settings loaded successfully!", "Settings Loaded",
                       MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show($"Error loading settings: {ex.Message}", "Load Error",
                       MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnStop_Click(sender As Object, e As EventArgs) Handles btnSTOP.Click
        Try
            If cancellationTokenSource IsNot Nothing AndAlso isProcessing Then
                ' Create logging action for cancellation message
                Dim logAction As Action(Of String) = Sub(message As String)
                                                         If Me.InvokeRequired Then
                                                             Me.Invoke(Sub() logtxt.AppendText(message))
                                                         Else
                                                             logtxt.AppendText(message)
                                                         End If
                                                     End Sub

                logAction($"{vbCrLf}*** USER REQUESTED CANCELLATION ***{vbCrLf}")
                StatusText.Text = "Cancelling process..."

                ' Change STOP button to show cancellation in progress
                btnSTOP.Text = "CANCELLING..."
                btnSTOP.BackColor = Color.Orange
                btnSTOP.Enabled = False

                ' Cancel the process
                cancellationTokenSource.Cancel()
            End If
        Catch ex As Exception
            logtxt.AppendText($"Error during cancellation: {ex.Message}{vbCrLf}")
        End Try
    End Sub

    Private Sub SQLString_TextChanged(sender As Object, e As EventArgs) Handles SQLString.TextChanged
        ConnectionString = SQLString.Text
    End Sub

    Private Sub SQLString_DoubleClick(sender As Object, e As EventArgs) Handles SQLString.DoubleClick
        If SQLString.PasswordChar = "" Then SQLString.PasswordChar = "*" Else SQLString.PasswordChar = ""
    End Sub

    Private Sub SQLString_LostFocus(sender As Object, e As EventArgs) Handles SQLString.LostFocus
        SQLString.PasswordChar = "*"
    End Sub




#End Region


End Class