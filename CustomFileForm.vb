Imports System.IO
Imports System.Threading
Imports System.Text.RegularExpressions
Public Class CustomFileForm

    Private cancellationTokenSource As CancellationTokenSource = Nothing
    Private isProcessing As Boolean = False
    Private editingSubItem As ListViewItem.ListViewSubItem = Nothing
    Private editBox As New TextBox()

    ' Parent form settings
    Public Property UseAPI As Boolean = False
    Public Property ApiKey As String = ""
    Public Property DebugMode As Boolean = False
    Public Property Language As String = ""

    Private Sub CustomFileForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Enable label editing
        editBox.Visible = False
        editBox.BorderStyle = BorderStyle.FixedSingle
        lvwSongs.Controls.Add(editBox)
        AddHandler editBox.KeyPress, AddressOf EditBox_KeyPress
        AddHandler editBox.LostFocus, AddressOf EditBox_LostFocus
    End Sub

    Private Sub btnAddSong_Click(sender As Object, e As EventArgs) Handles btnAddSong.Click
        Try
            Using openDialog As New OpenFileDialog()
                openDialog.Filter = "MP3 files (*.mp3)|*.mp3"
                openDialog.Multiselect = True
                openDialog.Title = "Select MP3 Files"

                If openDialog.ShowDialog() = DialogResult.OK Then
                    For Each filePath As String In openDialog.FileNames
                        AddFileToList(filePath)
                    Next

                    StatusText.Text = $"Added {openDialog.FileNames.Length} file(s)"
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show($"Error adding files: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' Returns the YOUTUBE_URL custom TXXX tag value from a parsed MP3, or "" if absent.
    Private Function GetYouTubeUrlFromTags(mp3Info As Mp3Info) As String
        If mp3Info Is Nothing OrElse mp3Info.CommentTags Is Nothing Then Return ""
        Dim url As String = Nothing
        If mp3Info.CommentTags.TryGetValue("YOUTUBE_URL", url) Then
            Return If(url, "").Trim()
        End If
        Return ""
    End Function

    Private Sub AddFileToList(filePath As String)
        Try
            ' Check if file already exists
            For Each itm As ListViewItem In lvwSongs.Items
                If itm.SubItems(3).Text = filePath Then
                    Return ' Already added
                End If
            Next

            ' Parse MP3 info
            Dim mp3Info As Mp3Info = ParseMp3InfoEnhanced(filePath)
            If mp3Info Is Nothing Then
                MessageBox.Show($"Could not read MP3 info from: {Path.GetFileName(filePath)}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            ' Create ListView item
            Dim item As New ListViewItem(mp3Info.Artist)
            item.SubItems.Add(mp3Info.Title)
            item.SubItems.Add(GetYouTubeUrlFromTags(mp3Info))
            item.SubItems.Add(filePath) ' Full file path

            lvwSongs.Items.Add(item)

        Catch ex As Exception
            MessageBox.Show($"Error adding file {Path.GetFileName(filePath)}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnRemove_Click(sender As Object, e As EventArgs) Handles btnRemove.Click
        If lvwSongs.SelectedItems.Count > 0 Then
            For Each item As ListViewItem In lvwSongs.SelectedItems
                lvwSongs.Items.Remove(item)
            Next
            StatusText.Text = "Selected items removed"
        End If
    End Sub

    Private Sub btnClear_Click(sender As Object, e As EventArgs) Handles btnClear.Click
        If lvwSongs.Items.Count > 0 Then
            If MessageBox.Show("Clear all items?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                lvwSongs.Items.Clear()
                StatusText.Text = "List cleared"
            End If
        End If
    End Sub
    Private Sub EditBox_KeyPress(sender As Object, e As KeyPressEventArgs)
        If e.KeyChar = ChrW(Keys.Enter) Then
            e.Handled = True
            If editingSubItem IsNot Nothing Then
                editingSubItem.Text = editBox.Text
            End If
            editBox.Visible = False
            editingSubItem = Nothing
        ElseIf e.KeyChar = ChrW(Keys.Escape) Then
            e.Handled = True
            editBox.Visible = False
            editingSubItem = Nothing
        End If
    End Sub

    Private Sub EditBox_LostFocus(sender As Object, e As EventArgs)
        If editingSubItem IsNot Nothing Then
            editingSubItem.Text = editBox.Text
        End If
        editBox.Visible = False
        editingSubItem = Nothing
    End Sub


    Private Sub btnEdit_Click(sender As Object, e As EventArgs) Handles btnEdit.Click
        ' Edit columns 0 (Artist), 1 (Title), 2 (URL) - not column 3 (FilePath)
        Dim wasModified = UniversalListViewEditor.EditListViewItems(lvwSongs, Me, New List(Of Integer) From {0, 1, 2})
        If wasModified Then
            StatusText.Text = "Items updated"
        End If
    End Sub

    ' Double-click to edit URL column
    Private Sub lvwSongs_DoubleClick(sender As Object, e As EventArgs) Handles lvwSongs.DoubleClick
        Try
            If lvwSongs.SelectedItems.Count = 0 Then Return

            Dim selectedItem As ListViewItem = lvwSongs.SelectedItems(0)

            ' Get click position
            Dim pos As Point = lvwSongs.PointToClient(Cursor.Position)
            Dim hitTest As ListViewHitTestInfo = lvwSongs.HitTest(pos)

            If hitTest.SubItem Is Nothing Then Return

            ' Find which column was clicked
            Dim columnIndex As Integer = 0
            For i As Integer = 0 To selectedItem.SubItems.Count - 1
                If selectedItem.SubItems(i) Is hitTest.SubItem Then
                    columnIndex = i
                    Exit For
                End If
            Next

            ' Only allow editing URL column (index 2)
            If columnIndex <> 2 Then Return

            editingSubItem = hitTest.SubItem

            ' Get proper bounds for the subitem
            Dim subItemRect As Rectangle = hitTest.SubItem.Bounds

            ' Position and show edit box
            editBox.Bounds = subItemRect
            editBox.Text = editingSubItem.Text
            editBox.Visible = True
            editBox.BringToFront()
            editBox.Focus()
            editBox.SelectAll()

        Catch ex As Exception
            ' Ignore errors
        End Try
    End Sub


    Private Async Sub btnProcess_Click(sender As Object, e As EventArgs) Handles btnProcess.Click
        Try
            If isProcessing Then
                MessageBox.Show("Processing already in progress", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            If lvwSongs.Items.Count = 0 Then
                MessageBox.Show("No files to process", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' Get items to process (checked first, then selected)
            Dim itemsToProcess As List(Of ListViewItem)

            If lvwSongs.CheckedItems.Count > 0 Then
                itemsToProcess = lvwSongs.CheckedItems.Cast(Of ListViewItem)().ToList()
            ElseIf lvwSongs.SelectedItems.Count > 0 Then
                itemsToProcess = lvwSongs.SelectedItems.Cast(Of ListViewItem)().ToList()
            Else
                MessageBox.Show("No items selected or checked for processing", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' Confirm processing
            Dim result As DialogResult = MessageBox.Show(
    $"Process {itemsToProcess.Count} file(s)?{vbCrLf}{vbCrLf}" &
    $"Mode: {If(UseAPI, "API with fallback", "Basic scraping only")}{vbCrLf}{vbCrLf}" &
    $"Continue?",
    "Confirm Processing",
    MessageBoxButtons.YesNo,
    MessageBoxIcon.Question)

            If result <> DialogResult.Yes Then Return

            isProcessing = True
            btnProcess.Enabled = False
            btnAddSong.Enabled = False
            cancellationTokenSource = New CancellationTokenSource()

            Await ProcessCustomFiles(itemsToProcess, cancellationTokenSource.Token)

        Catch ex As OperationCanceledException
            MessageBox.Show("Processing cancelled", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show($"Error during processing: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            isProcessing = False
            btnProcess.Enabled = True
            btnAddSong.Enabled = True
            If cancellationTokenSource IsNot Nothing Then
                cancellationTokenSource.Dispose()
                cancellationTokenSource = Nothing
            End If
        End Try
    End Sub

    Private Async Function ProcessCustomFiles(itemsToProcess As List(Of ListViewItem), cancellationToken As CancellationToken) As Task
        Dim results As New ProcessingResults()
        results.TotalFiles = itemsToProcess.Count

        ' Create logging action
        Dim logAction As Action(Of String) = Sub(message As String)
                                                 If Me.InvokeRequired Then
                                                     Me.Invoke(Sub() txtLog.AppendText(message))
                                                 Else
                                                     txtLog.AppendText(message)
                                                 End If
                                             End Sub

        Try
            txtLog.Clear()
            logAction($"=== Starting Custom File Processing ==={vbCrLf}")
            logAction($"Mode: {If(UseAPI, "API with fallback", "Basic scraping only")}{vbCrLf}")
            logAction($"Processing {results.TotalFiles} selected files{vbCrLf}{vbCrLf}")

            InitializeYouTubeSearch()
            InitializeYouTubeStats()

            ProgressBar1.Maximum = results.TotalFiles
            ProgressBar1.Value = 0

            ' Process each file
            For i As Integer = 0 To itemsToProcess.Count - 1
                If cancellationToken.IsCancellationRequested Then
                    logAction($"{vbCrLf}*** PROCESSING CANCELLED BY USER ***{vbCrLf}")
                    Exit For
                End If

                Dim item As ListViewItem = itemsToProcess(i)
                Dim filePath As String = item.SubItems(3).Text
                Dim providedUrl As String = item.SubItems(2).Text.Trim()

                StatusText.Text = $"Processing {i + 1}/{results.TotalFiles}: {Path.GetFileName(filePath)}"
                ProgressBar1.Value = i + 1
                Application.DoEvents()

                Await ProcessSingleCustomFile(filePath, providedUrl, i + 1, results, logAction, cancellationToken)

                If i < itemsToProcess.Count - 1 Then
                    logAction($"Waiting 2 seconds before next file...{vbCrLf}")
                    Await Task.Delay(500, cancellationToken)
                End If
            Next

            ' Summary
            logAction($"{vbCrLf}=== Custom File Processing Complete ==={vbCrLf}")
            logAction($"Total files: {results.TotalFiles}{vbCrLf}")
            logAction($"Successfully processed: {results.ProcessedFiles}{vbCrLf}")
            logAction($"New records created: {results.NewRecords}{vbCrLf}")
            logAction($"Existing records: {results.ExistingRecords}{vbCrLf}")
            logAction($"Stats updated: {results.StatsUpdated}{vbCrLf}")
            logAction($"Not found: {results.NotFoundFiles}{vbCrLf}")
            logAction($"Errors: {results.ErrorFiles}{vbCrLf}")
            logAction($"Processing completed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{vbCrLf}")

            ' Show results
            If Not cancellationToken.IsCancellationRequested Then
                MessageBox.Show(
                $"Processing Complete!{vbCrLf}{vbCrLf}" &
                $"Files processed: {results.ProcessedFiles}/{results.TotalFiles}{vbCrLf}" &
                $"New records: {results.NewRecords}{vbCrLf}" &
                $"Existing records: {results.ExistingRecords}{vbCrLf}" &
                $"Stats updated: {results.StatsUpdated}{vbCrLf}" &
                $"Not found: {results.NotFoundFiles}{vbCrLf}" &
                $"Errors: {results.ErrorFiles}",
                "Results", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If

        Finally
            CleanupYouTubeSearch()
            CleanupYouTubeStats()
            StatusText.Text = "Ready"
            ProgressBar1.Value = 0
        End Try
    End Function

    Private Async Function ProcessSingleCustomFile(filePath As String, providedUrl As String, fileIndex As Integer, results As ProcessingResults, logAction As Action(Of String), cancellationToken As CancellationToken) As Task
        Dim fileName As String = Path.GetFileName(filePath)

        Try
            logAction($"--- Processing file {fileIndex}/{results.TotalFiles}: {fileName} ---{vbCrLf}")
            logAction($"Step 1: Parsing MP3 metadata...{vbCrLf}")

            Dim mp3Info As Mp3Info = ParseMp3InfoEnhanced(filePath)
            If mp3Info Is Nothing Then
                logAction($"ERROR: Could not parse MP3 info for {fileName}{vbCrLf}{vbCrLf}")
                results.ErrorFiles += 1
                Return
            End If

            Dim artistTitle As String = $"{mp3Info.Artist} - {mp3Info.Title}"
            logAction($"MP3 Info: {artistTitle}{vbCrLf}")

            Dim videoId As String = Nothing
            Dim videoExistsInDb As Boolean = False
            Dim urlProvided As Boolean = False

            ' Try to extract VideoID from URL if provided
            If Not String.IsNullOrWhiteSpace(providedUrl) Then
                logAction($"Step 2: URL provided, extracting VideoID...{vbCrLf}")
                logAction($"Provided URL: {providedUrl}{vbCrLf}")

                ' Extract VideoID from URL
                Dim patterns As String() = {
                    "(?:youtube\.com/watch\?v=|youtu\.be/)([a-zA-Z0-9_-]{11})",
                    "youtube\.com/embed/([a-zA-Z0-9_-]{11})",
                    "^([a-zA-Z0-9_-]{11})$"
                }

                For Each pattern In patterns
                    Dim match As Match = Regex.Match(providedUrl, pattern)
                    If match.Success Then
                        videoId = match.Groups(1).Value
                        logAction($"Extracted VideoID from URL: {videoId}{vbCrLf}")
                        urlProvided = True
                        Exit For
                    End If
                Next

                If Not urlProvided Then
                    logAction($"WARNING: Could not extract valid VideoID from URL. Using fallback search method.{vbCrLf}")
                End If
            End If

            ' If URL was provided and VideoID extracted successfully
            If urlProvided AndAlso Not String.IsNullOrWhiteSpace(videoId) Then
                ' Check if VideoID exists in database
                logAction($"Step 3: Checking if VideoID exists in database...{vbCrLf}")
                videoExistsInDb = VideoIdExistsInDatabase(videoId)

                If videoExistsInDb Then
                    logAction($"Found existing VideoID in database: {videoId}{vbCrLf}")
                    results.ExistingRecords += 1
                Else
                    logAction($"VideoID not found in database. Will create new record.{vbCrLf}")

                    ' Get stats for new record
                    logAction($"Step 4: Getting YouTube stats for new record...{vbCrLf}")
                    logAction($"Waiting 1 second before stats request...{vbCrLf}")
                    Await Task.Delay(500, cancellationToken)

                    Dim statsRecord As YTStatsRecord = Nothing

                    If UseAPI AndAlso Not String.IsNullOrWhiteSpace(ApiKey) Then
                        logAction($"Getting stats with YouTube API...{vbCrLf}")
                        statsRecord = Await GetYouTubeVideoStatsAPI(videoId, ApiKey, logAction, DebugMode)

                        If statsRecord Is Nothing Then
                            logAction($"API stats failed. Waiting 2 seconds before fallback...{vbCrLf}")
                            Await Task.Delay(500, cancellationToken)
                            statsRecord = Await GetYouTubeVideoStats(videoId, logAction, DebugMode)
                        End If
                    Else
                        logAction($"Getting stats with basic method...{vbCrLf}")
                        statsRecord = Await GetYouTubeVideoStats(videoId, logAction, DebugMode)
                    End If

                    If statsRecord IsNot Nothing Then
                        logAction($"Stats retrieved: Views={statsRecord.ViewCount:N0}, PublishDate={statsRecord.PublishDate:yyyy-MM-dd}{vbCrLf}")
                        logAction($"Step 5: Inserting new song record with PublishDate into database...{vbCrLf}")

                        Try
                            InsertYTSong(artistTitle, statsRecord.Title, videoId, statsRecord.PublishDate, statsRecord.Channel, Language)
                            logAction($"Successfully inserted new song record with PublishDate{vbCrLf}")
                            results.NewRecords += 1

                            logAction($"Step 6: Saving stats to database...{vbCrLf}")
                            InsertYTStatsWithMerge(videoId, statsRecord.ViewCount, Today.Date)
                            logAction($"Stats saved: Views={statsRecord.ViewCount:N0}, Date={Today.Date:yyyy-MM-dd}{vbCrLf}")
                            results.StatsUpdated += 1
                        Catch ex As Exception
                            logAction($"ERROR inserting song record: {ex.Message}{vbCrLf}")
                            results.ErrorFiles += 1
                        End Try
                    Else
                        logAction($"Could not get stats for VideoID: {videoId}{vbCrLf}{vbCrLf}")
                        results.ErrorFiles += 1
                        Return
                    End If
                End If

            Else
                ' No URL provided OR could not extract VideoID - use original search logic
                If Not String.IsNullOrWhiteSpace(providedUrl) Then
                    logAction($"Step 2: Falling back to standard search method...{vbCrLf}")
                Else
                    logAction($"Step 2: No URL provided. Checking database for existing record by ALIAS...{vbCrLf}")
                End If

                videoId = GetVideoIdByAliasLike(artistTitle)

                If Not String.IsNullOrWhiteSpace(videoId) Then
                    logAction($"Found existing VideoID in database: {videoId}{vbCrLf}")
                    results.ExistingRecords += 1
                    videoExistsInDb = True
                Else
                    logAction($"Step 3: No existing record found. Searching YouTube...{vbCrLf}")
                    logAction($"Waiting 1 second before YouTube search...{vbCrLf}")
                    Await Task.Delay(500, cancellationToken)

                    Dim searchResult As YouTubeSearchResult = Nothing

                    If UseAPI AndAlso Not String.IsNullOrWhiteSpace(ApiKey) Then
                        logAction($"Searching with YouTube API...{vbCrLf}")
                        searchResult = Await SearchYouTubeAPI(artistTitle, ApiKey, logAction, DebugMode)

                        If searchResult Is Nothing OrElse searchResult.BestMatch Is Nothing Then
                            logAction($"API search failed. Waiting 2 seconds before fallback...{vbCrLf}")
                            Await Task.Delay(500, cancellationToken)
                            searchResult = Await SearchYouTubeBasic(artistTitle, logAction, DebugMode)
                        End If
                    Else
                        logAction($"Searching with basic method...{vbCrLf}")
                        searchResult = Await SearchYouTubeBasic(artistTitle, logAction, DebugMode)
                    End If

                    If searchResult IsNot Nothing AndAlso searchResult.BestMatch IsNot Nothing Then
                        videoId = searchResult.BestMatch.VideoId
                        logAction($"Found YouTube video: {searchResult.BestMatch.Title} by {searchResult.BestMatch.Channel}{vbCrLf}")
                        logAction($"VideoID: {videoId}{vbCrLf}")

                        ' Get stats for new record
                        logAction($"Step 4: Getting YouTube stats to obtain PublishDate...{vbCrLf}")
                        logAction($"Waiting 1 second before stats request...{vbCrLf}")
                        Await Task.Delay(500, cancellationToken)

                        Dim statsRecord As YTStatsRecord = Nothing

                        If UseAPI AndAlso Not String.IsNullOrWhiteSpace(ApiKey) Then
                            logAction($"Getting stats with YouTube API...{vbCrLf}")
                            statsRecord = Await GetYouTubeVideoStatsAPI(videoId, ApiKey, logAction, DebugMode)

                            If statsRecord Is Nothing Then
                                logAction($"API stats failed. Waiting 2 seconds before fallback...{vbCrLf}")
                                Await Task.Delay(500, cancellationToken)
                                statsRecord = Await GetYouTubeVideoStats(videoId, logAction, DebugMode)
                            End If
                        Else
                            logAction($"Getting stats with basic method...{vbCrLf}")
                            statsRecord = Await GetYouTubeVideoStats(videoId, logAction, DebugMode)
                        End If

                        If statsRecord IsNot Nothing Then
                            logAction($"Stats retrieved: Views={statsRecord.ViewCount:N0}, PublishDate={statsRecord.PublishDate:yyyy-MM-dd}{vbCrLf}")
                            logAction($"Step 5: Inserting new song record with PublishDate into database...{vbCrLf}")

                            Try
                                InsertYTSong(artistTitle, searchResult.BestMatch.Title, videoId, statsRecord.PublishDate, searchResult.BestMatch.Channel, Language)
                                logAction($"Successfully inserted new song record with PublishDate{vbCrLf}")
                                results.NewRecords += 1

                                logAction($"Step 6: Saving stats to database...{vbCrLf}")
                                InsertYTStatsWithMerge(videoId, statsRecord.ViewCount, Today.Date)
                                logAction($"Stats saved: Views={statsRecord.ViewCount:N0}, Date={Today.Date:yyyy-MM-dd}{vbCrLf}")
                                results.StatsUpdated += 1
                            Catch ex As Exception
                                logAction($"ERROR inserting song record: {ex.Message}{vbCrLf}")
                                results.ErrorFiles += 1
                            End Try
                        Else
                            logAction($"Could not get stats for VideoID: {videoId}. Cannot insert song record without PublishDate.{vbCrLf}")
                            results.ErrorFiles += 1
                            Return
                        End If
                    Else
                        logAction($"No suitable YouTube video found for: {artistTitle}{vbCrLf}{vbCrLf}")
                        results.NotFoundFiles += 1
                        Return
                    End If
                End If
            End If

            ' Update stats for existing records (whether found by URL or by ALIAS)
            If Not String.IsNullOrWhiteSpace(videoId) AndAlso videoExistsInDb Then
                Dim stepNum As Integer = If(urlProvided, 4, 3)
                logAction($"Step {stepNum}: Getting current YouTube stats for existing record...{vbCrLf}")
                logAction($"Waiting 1 second before stats request...{vbCrLf}")
                Await Task.Delay(500, cancellationToken)

                Dim existingStats As YTStatsRecord = Nothing

                If UseAPI AndAlso Not String.IsNullOrWhiteSpace(ApiKey) Then
                    logAction($"Getting stats with YouTube API...{vbCrLf}")
                    existingStats = Await GetYouTubeVideoStatsAPI(videoId, ApiKey, logAction, DebugMode)

                    If existingStats Is Nothing Then
                        logAction($"API stats failed. Waiting 2 seconds before fallback...{vbCrLf}")
                        Await Task.Delay(500, cancellationToken)
                        existingStats = Await GetYouTubeVideoStats(videoId, logAction, DebugMode)
                    End If
                Else
                    logAction($"Getting stats with basic method...{vbCrLf}")
                    existingStats = Await GetYouTubeVideoStats(videoId, logAction, DebugMode)
                End If

                If existingStats IsNot Nothing Then
                    Try
                        InsertYTStatsWithMerge(videoId, existingStats.ViewCount, Today.Date)
                        logAction($"Current stats saved: Views={existingStats.ViewCount:N0}, Date={Today.Date:yyyy-MM-dd}{vbCrLf}")
                        results.StatsUpdated += 1
                    Catch ex As Exception
                        logAction($"ERROR saving current stats: {ex.Message}{vbCrLf}")
                    End Try
                Else
                    logAction($"Could not get current stats for existing VideoID: {videoId}{vbCrLf}")
                End If
            End If

            logAction($"✓ Completed processing: {fileName}{vbCrLf}{vbCrLf}")
            results.ProcessedFiles += 1

        Catch ex As Exception
            logAction($"ERROR processing {fileName}: {ex.Message}{vbCrLf}{vbCrLf}")
            results.ErrorFiles += 1
        End Try
    End Function
End Class