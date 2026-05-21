Imports Microsoft.Data.SqlClient
Imports System.IO
Imports System.Threading
Imports TagLibSharp = TagLib

' Add this class to store all form settings
Public Class FormSettings
    Public Property ApiKey As String = ""
    Public Property UseAPI As Boolean = False
    Public Property UseBasic As Boolean = True
    Public Property DebugMode As Boolean = False
    Public Property AutoProcessingEnabled As Boolean = False
    Public Property ScheduledTime As DateTime = New DateTime(2025, 1, 1, 2, 0, 0)
    Public Property TreeViewStatePath As String = ""
    Public Property langText As String = ""
    Public Property SQLCpnnection As String = ""
    Public Sub New()
        ' Default constructor
    End Sub
End Class
Module GeneralModule
    ' Database connection string
    Public ConnectionString As String = "" '"Server=10.0.1.2;Database=Radio_Statistics;User Id=YTStats;Password=Y!s2ats;TrustServerCertificate=True;"
    Private cancellationToken As CancellationToken = New CancellationToken()
    Public Class Mp3Info
        Public Property Folder As String
        Public Property FileName As String
        Public Property Artist As String
        Public Property Title As String
        Public Property Genre As String()
        Public Property ArtistTranslit As String
        Public Property TitleTranslit As String
        Public Property durationSeconds As Integer
        Public Property CommentTags As Dictionary(Of String, String)
        Public Property isUseful As Boolean
        Public Property Rating As Integer = 0
        Public Property IsRuledSong As Boolean = False
        Public Property ArtistTitle As String ' For database search

        Public Sub New()
            CommentTags = New Dictionary(Of String, String)()
        End Sub
    End Class

    ''' <summary>
    ''' UPDATED: YTSongRecord class - Updated for new ALIAS field schema
    ''' </summary>
    Public Class YTSongRecord
        Public Property YTVideoID As String
        Public Property YTArtistTitle As String    ' YouTube video title
        Public Property Mp3ALIAS As String           ' MP3 Artist - Title
        Public Property PublishDate As Date
        Public Property YTChannelName As String
    End Class
#Region "########## MP3 Operations ##########"
    ''' <summary>
    ''' Enhanced MP3 info parsing with custom tags support
    ''' </summary>
    Public Function ParseMp3InfoEnhanced(filePath As String) As Mp3Info
        Try
            If Not File.Exists(filePath) Then Return Nothing

            Dim mp3Info As New Mp3Info()

            Using file As TagLibSharp.File = TagLibSharp.File.Create(filePath)
                ' Basic metadata
                mp3Info.Folder = Path.GetDirectoryName(filePath)
                mp3Info.FileName = Path.GetFileName(filePath)
                mp3Info.Artist = If(String.IsNullOrEmpty(file.Tag.FirstPerformer), "Unknown Artist", file.Tag.FirstPerformer.Trim())
                mp3Info.Title = If(String.IsNullOrEmpty(file.Tag.Title), Path.GetFileNameWithoutExtension(filePath), file.Tag.Title.Trim())
                mp3Info.durationSeconds = CInt(file.Properties.Duration.TotalSeconds)

                ' Genres
                If file.Tag.Genres IsNot Nothing AndAlso file.Tag.Genres.Length > 0 Then
                    mp3Info.Genre = file.Tag.Genres
                Else
                    mp3Info.Genre = {"Unknown"}
                End If

                ' Custom tags from TXXX frames
                ReadCustomTagsFromMP3(file, mp3Info)

            End Using

            Return mp3Info

        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Read custom TXXX tags from MP3 file
    ''' </summary>
    Private Sub ReadCustomTagsFromMP3(file As TagLibSharp.File, mp3Info As Mp3Info)
        Try
            Dim id3v2Tag As TagLibSharp.Id3v2.Tag = TryCast(file.GetTag(TagLibSharp.TagTypes.Id3v2), TagLibSharp.Id3v2.Tag)

            If id3v2Tag IsNot Nothing Then
                For Each frame As TagLibSharp.Id3v2.Frame In id3v2Tag.GetFrames()
                    If TypeOf frame Is TagLibSharp.Id3v2.UserTextInformationFrame Then
                        Dim userFrame As TagLibSharp.Id3v2.UserTextInformationFrame = DirectCast(frame, TagLibSharp.Id3v2.UserTextInformationFrame)

                        If Not String.IsNullOrEmpty(userFrame.Description) AndAlso userFrame.Text IsNot Nothing AndAlso userFrame.Text.Length > 0 Then
                            mp3Info.CommentTags(userFrame.Description.ToUpper()) = userFrame.Text(0)
                        End If
                    End If
                Next

                ' Set rating if available
                If mp3Info.CommentTags.ContainsKey("RATING") Then
                    Integer.TryParse(mp3Info.CommentTags("RATING"), mp3Info.Rating)
                End If
            End If

        Catch ex As Exception
            ' Ignore custom tag reading errors
        End Try
    End Sub

#End Region

#Region "Database Operations"

    ''' <summary>
    ''' STEP 2: Insert new record in YT_Songs 
    ''' YTArtistTitle = YouTube video title, ALIAS = MP3 Artist - Title
    ''' </summary>
    Public Function InsertYTSong(mp3ArtistTitle As String, youtubeTitle As String, videoId As String, publishDate As Date, channelName As String, Optional lang As String = "") As String
        Try
            Using connection As New SqlConnection(ConnectionString)
                connection.Open()

                ' Use parameterized query - this automatically handles all special characters including quotes
                Dim insertQuery As String = "INSERT INTO YT_Songs (YTVideoID, YTArtistTitle, ALIAS, PublishDate, YTChannelName, Lang) VALUES (@YTVideoID, @YTArtistTitle, @ALIAS, @PublishDate, @YTChannelName, @Lang)"

                Using command As New SqlCommand(insertQuery, connection)
                    ' Only trim whitespace, preserve all quotes and special characters
                    command.Parameters.AddWithValue("@YTVideoID", If(String.IsNullOrEmpty(videoId), "", videoId.Trim()))
                    command.Parameters.AddWithValue("@YTArtistTitle", If(String.IsNullOrEmpty(youtubeTitle), "", youtubeTitle.Trim()))
                    command.Parameters.AddWithValue("@ALIAS", If(String.IsNullOrEmpty(mp3ArtistTitle), "", mp3ArtistTitle.Trim()))
                    command.Parameters.AddWithValue("@PublishDate", publishDate.Date)
                    command.Parameters.AddWithValue("@Lang", If(String.IsNullOrEmpty(lang), "", lang.Trim()))

                    ' Handle channel name
                    If String.IsNullOrWhiteSpace(channelName) Then
                        command.Parameters.AddWithValue("@YTChannelName", DBNull.Value)
                    Else
                        command.Parameters.AddWithValue("@YTChannelName", channelName.Trim())
                    End If

                    command.ExecuteNonQuery()
                End Using

                Return videoId

            End Using

        Catch ex As Exception
            Throw New Exception($"Database insert error for VideoID {videoId}: {ex.Message}")
        End Try
    End Function

    ''' <summary>
    ''' Get YTVideoID from YT_Songs where ALIAS matches (exact LIKE first, then fuzzy matching)
    ''' </summary>
    Public Function GetVideoIdByAliasLike(mp3ArtistTitle As String, Optional matchThreshold As Double = 80.0) As String
        Try
            If String.IsNullOrWhiteSpace(mp3ArtistTitle) Then
                Return Nothing
            End If

            Dim searchString As String = mp3ArtistTitle.Trim()

            Using connection As New SqlConnection(ConnectionString)
                connection.Open()

                ' STEP 1: Try exact SQL LIKE match first (fast path)
                Dim escapedSearchString As String = EscapeSqlLikeString(searchString)
                Dim exactQuery As String = "SELECT TOP 1 YTVideoID FROM YT_Songs WHERE ALIAS LIKE @SearchPattern ESCAPE '^'"

                Using exactCommand As New SqlCommand(exactQuery, connection)
                    exactCommand.Parameters.AddWithValue("@SearchPattern", $"%{escapedSearchString}%")
                    Dim exactResult = exactCommand.ExecuteScalar()
                    If exactResult IsNot Nothing AndAlso Not IsDBNull(exactResult) Then
                        Return exactResult.ToString()
                    End If
                End Using

                ' STEP 2: No exact match - use fuzzy matching
                Dim fuzzyQuery As String = "SELECT YTVideoID, ALIAS FROM YT_Songs WHERE ALIAS IS NOT NULL AND ALIAS <> ''"

                Using fuzzyCommand As New SqlCommand(fuzzyQuery, connection)
                    Using reader As SqlDataReader = fuzzyCommand.ExecuteReader()
                        Dim bestVideoId As String = Nothing
                        Dim bestScore As Double = 0.0

                        While reader.Read()
                            Dim videoId As String = reader("YTVideoID").ToString()
                            Dim aliastext As String = reader("ALIAS").ToString()

                            Dim score As Double = ArtistTitleMatcher2.GetMatchPercentage(searchString, aliastext, "auto")

                            If score >= matchThreshold AndAlso score > bestScore Then
                                bestScore = score
                                bestVideoId = videoId
                            End If
                        End While

                        If bestVideoId IsNot Nothing Then
                            Return bestVideoId
                        End If
                    End Using
                End Using
            End Using

        Catch ex As Exception
            Throw New Exception($"Database VideoID search error: {ex.Message}")
        End Try

        Return Nothing
    End Function

    ''' <summary>
    ''' Escape special characters in SQL LIKE patterns
    ''' Characters that need escaping: [ ] % _ ^
    ''' </summary>
    Private Function EscapeSqlLikeString(input As String) As String
        If String.IsNullOrEmpty(input) Then
            Return ""
        End If

        Dim escaped As String = input

        ' Escape the escape character first (^)
        escaped = escaped.Replace("^", "^^")

        ' Escape SQL LIKE wildcards and special characters
        escaped = escaped.Replace("%", "^%")   ' % is wildcard for "any characters"
        escaped = escaped.Replace("_", "^_")   ' _ is wildcard for "any single character"
        escaped = escaped.Replace("[", "^[")   ' [ starts character class
        escaped = escaped.Replace("]", "^]")   ' ] ends character class

        Return escaped
    End Function


    ''' <summary>
    ''' UPDATED: InsertYTStats - Remove/Update existing today's stats before inserting
    ''' Ensures YTVideoID + Date combination is unique
    ''' </summary>
    Public Sub InsertYTStats(videoId As String, viewCount As Long, statDate As Date)
        Try
            Using connection As New SqlConnection(ConnectionString)
                connection.Open()

                ' First, check if record exists for today
                Dim checkQuery As String = "SELECT COUNT(*) FROM YT_Stats WHERE YTVideoID = @YTVideoID AND StatDate = @StatDate"

                Using checkCommand As New SqlCommand(checkQuery, connection)
                    checkCommand.Parameters.AddWithValue("@YTVideoID", videoId)
                    checkCommand.Parameters.AddWithValue("@StatDate", statDate.Date)

                    Dim recordExists As Integer = CInt(checkCommand.ExecuteScalar())

                    If recordExists > 0 Then
                        ' Update existing record
                        Dim updateQuery As String = "UPDATE YT_Stats SET ViewCount = @ViewCount WHERE YTVideoID = @YTVideoID AND StatDate = @StatDate"

                        Using updateCommand As New SqlCommand(updateQuery, connection)
                            updateCommand.Parameters.AddWithValue("@ViewCount", viewCount)
                            updateCommand.Parameters.AddWithValue("@YTVideoID", videoId)
                            updateCommand.Parameters.AddWithValue("@StatDate", statDate.Date)
                            updateCommand.ExecuteNonQuery()
                        End Using
                    Else
                        ' Insert new record
                        Dim insertQuery As String = "INSERT INTO YT_Stats (StatDate, ViewCount, YTVideoID) VALUES (@StatDate, @ViewCount, @YTVideoID)"

                        Using insertCommand As New SqlCommand(insertQuery, connection)
                            insertCommand.Parameters.AddWithValue("@StatDate", statDate.Date)
                            insertCommand.Parameters.AddWithValue("@ViewCount", viewCount)
                            insertCommand.Parameters.AddWithValue("@YTVideoID", videoId)
                            insertCommand.ExecuteNonQuery()
                        End Using
                    End If
                End Using
            End Using

        Catch ex As Exception
            Throw New Exception($"Database stats upsert error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' ALTERNATIVE: InsertYTStats using MERGE statement (more efficient)
    ''' Single SQL statement to handle insert/update
    ''' </summary>
    Public Sub InsertYTStatsWithMerge(videoId As String, viewCount As Long, statDate As Date)
        Try
            Using connection As New SqlConnection(ConnectionString)
                connection.Open()

                ' Use MERGE statement for efficient upsert
                Dim mergeQuery As String = "
                MERGE YT_Stats AS target
                USING (VALUES (@YTVideoID, @StatDate, @ViewCount)) AS source (YTVideoID, StatDate, ViewCount)
                ON target.YTVideoID = source.YTVideoID AND target.StatDate = source.StatDate
                WHEN MATCHED THEN
                    UPDATE SET ViewCount = source.ViewCount
                WHEN NOT MATCHED THEN
                    INSERT (YTVideoID, StatDate, ViewCount) VALUES (source.YTVideoID, source.StatDate, source.ViewCount);"

                Using command As New SqlCommand(mergeQuery, connection)
                    command.Parameters.AddWithValue("@YTVideoID", videoId)
                    command.Parameters.AddWithValue("@StatDate", statDate.Date)
                    command.Parameters.AddWithValue("@ViewCount", viewCount)
                    command.ExecuteNonQuery()
                End Using
            End Using

        Catch ex As Exception
            Throw New Exception($"Database stats merge error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Get list of YT_Songs records that have no ALIAS (NULL or empty)
    ''' Returns complete record information for reporting
    ''' </summary>
    Public Function GetYTSongsWithoutAlias() As List(Of YTSongRecord)
        Dim songsWithoutAlias As New List(Of YTSongRecord)

        Try
            Using connection As New SqlConnection(ConnectionString)
                connection.Open()

                Dim query As String = "SELECT YTVideoID, YTArtistTitle, ALIAS, PublishDate, YTChannelName FROM YT_Songs WHERE ALIAS IS NULL OR ALIAS = '' ORDER BY PublishDate DESC"

                Using command As New SqlCommand(query, connection)
                    Using reader As SqlDataReader = command.ExecuteReader()
                        While reader.Read()
                            Dim record As New YTSongRecord With {
                            .YTVideoID = reader("YTVideoID").ToString(),
                            .YTArtistTitle = If(IsDBNull(reader("YTArtistTitle")), "", reader("YTArtistTitle").ToString()),
                            .Mp3ALIAS = If(IsDBNull(reader("ALIAS")), "", reader("ALIAS").ToString()),
                            .PublishDate = If(IsDBNull(reader("PublishDate")), Date.MinValue, CDate(reader("PublishDate"))),
                            .YTChannelName = If(IsDBNull(reader("YTChannelName")), "", reader("YTChannelName").ToString())
                        }
                            songsWithoutAlias.Add(record)
                        End While
                    End Using
                End Using
            End Using

        Catch ex As Exception
            Throw New Exception($"Database query error for songs without ALIAS: {ex.Message}")
        End Try

        Return songsWithoutAlias
    End Function

    Public Function VideoIdExistsInDatabase(videoId As String) As Boolean
        Try
            If String.IsNullOrWhiteSpace(videoId) Then Return False

            Using connection As New SqlConnection(ConnectionString)
                connection.Open()

                Dim query As String = "SELECT COUNT(*) FROM YT_Songs WHERE YTVideoID = @VideoID"

                Using command As New SqlCommand(query, connection)
                    command.Parameters.AddWithValue("@VideoID", videoId.Trim())

                    Dim count As Integer = CInt(command.ExecuteScalar())
                    Return count > 0
                End Using
            End Using

        Catch ex As Exception
            Return False
        End Try
    End Function
#End Region

#Region "Processing Files"

    ''' <summary>
    ''' Process checked MP3 files one by one - Search YouTube, get stats, and update database
    ''' Processes files sequentially directly from TreeView to avoid overwhelming YouTube
    ''' <param name="treeView">TreeView containing MP3 files</param>
    ''' <param name="useAPI">Whether to use YouTube API (true) or basic scraping (false)</param>
    ''' <param name="apiKey">YouTube API key (required if useAPI = true)</param>
    ''' <param name="logAction">Action to log messages</param>
    ''' <param name="progressBar">Progress bar for UI updates</param>
    ''' <param name="statusLabel">Status label for UI updates</param>
    ''' <param name="debugMode">Enable debug logging</param>
    ''' <param name="cancellationToken">Token to cancel the operation</param>
    ''' </summary>

    Public Async Function ProcessAllCheckedMP3Files(treeView As TreeView,
                                               useAPI As Boolean,
                                               apiKey As String,
                                               logAction As Action(Of String),
                                               Optional progressBar As ToolStripProgressBar = Nothing,
                                               Optional statusLabel As ToolStripStatusLabel = Nothing,
                                               Optional debugMode As Boolean = False,
                                               Optional cancellationToken As CancellationToken = Nothing,
                                               Optional language As String = "", Optional doAlias As Boolean = True) As Task(Of ProcessingResults)

        Dim results As New ProcessingResults()

        ' Handle default cancellation token
        If cancellationToken.Equals(Nothing) Then
            cancellationToken = CancellationToken.None
        End If

        Try
            ' Initialize modules
            YouTubeSearchModule.InitializeYouTubeSearch()
            YouTubeStatsModule.InitializeYouTubeStats()

            If logAction IsNot Nothing Then
                logAction($"=== Starting Complete Processing ==={vbcrlf}")
                logAction($"Mode: {If(useAPI, "API with fallback", "Basic scraping only")}{vbcrlf}")
            End If

            ' PHASE 1: Process TreeView MP3 Files
            If logAction IsNot Nothing Then
                logAction($"{vbcrlf}=== PHASE 1: Processing TreeView MP3 Files ==={vbcrlf}")
            End If

            ' [Keep all existing MP3 processing code here - no changes]
            Dim totalFiles As Integer = CountCheckedMP3FilesInTreeView(treeView)

            If totalFiles > 0 Then
                If progressBar IsNot Nothing Then
                    progressBar.Maximum = totalFiles
                    progressBar.Value = 0
                End If

                results.TotalFiles = totalFiles
                Dim fileCounter As New FileCounter()

                ' Process files one by one directly from TreeView
                For Each rootNode As TreeNode In treeView.Nodes
                    If cancellationToken.IsCancellationRequested Then
                        cancellationToken.ThrowIfCancellationRequested()
                    End If

                    Await ProcessNodeAndChildren(rootNode, fileCounter, results, useAPI, apiKey,
                                           logAction, progressBar, statusLabel, debugMode, cancellationToken, language)
                Next

                ' MP3 Processing summary
                If logAction IsNot Nothing Then
                    logAction($"{vbcrlf}=== PHASE 1 Complete: MP3 Processing Results ==={vbcrlf}")
                    logAction($"Total MP3 files: {results.TotalFiles}{vbcrlf}")
                    logAction($"Successfully processed: {results.ProcessedFiles}{vbcrlf}")
                    logAction($"New records created: {results.NewRecords}{vbcrlf}")
                    logAction($"Stats updated: {results.StatsUpdated}{vbcrlf}")
                End If
            Else
                If logAction IsNot Nothing Then
                    logAction($"No checked MP3 files found in TreeView{vbcrlf}")
                End If
            End If

            If doAlias Then
                ' PHASE 2: Process YT_Songs without ALIAS (Get Current Stats)
                If logAction IsNot Nothing Then
                    logAction($"{vbcrlf}=== PHASE 2: Processing YT_Songs without ALIAS ==={vbcrlf}")
                    logAction($"Getting current YouTube stats for records without ALIAS...{vbcrlf}")
                End If

                UpdateStatus(statusLabel, "Processing songs without ALIAS...")

                Try
                    ' Get list of VideoIDs without ALIAS
                    Dim songsWithoutAlias As List(Of YTSongRecord) = GetYTSongsWithoutAlias()
                    results.TotalMissingAlias = songsWithoutAlias.Count

                    If songsWithoutAlias.Count = 0 Then
                        If logAction IsNot Nothing Then
                            logAction($"✓ All YT_Songs records have ALIAS - no records to process{vbcrlf}")
                        End If
                    Else
                        If logAction IsNot Nothing Then
                            logAction($"Found {songsWithoutAlias.Count} YT_Songs without ALIAS - processing for current stats...{vbcrlf}")
                        End If

                        ' Update progress bar for ALIAS processing
                        Dim totalProcessing As Integer = results.TotalFiles + songsWithoutAlias.Count
                        If progressBar IsNot Nothing Then
                            progressBar.Maximum = totalProcessing
                        End If

                        ' Process each VideoID to get current stats
                        For i As Integer = 0 To songsWithoutAlias.Count - 1
                            ' Check for cancellation
                            If cancellationToken.IsCancellationRequested Then
                                If logAction IsNot Nothing Then
                                    logAction($"ALIAS processing cancelled by user request{vbcrlf}")
                                End If
                                cancellationToken.ThrowIfCancellationRequested()
                            End If

                            Await ProcessSingleVideoIdForStats(songsWithoutAlias(i), i + 1, songsWithoutAlias.Count,
                                                           results, useAPI, apiKey, logAction, progressBar,
                                                           statusLabel, debugMode, cancellationToken)
                        Next

                        If logAction IsNot Nothing Then
                            logAction($"{vbcrlf}=== PHASE 2 Complete: ALIAS Processing Results ==={vbcrlf}")
                            logAction($"Total without ALIAS: {results.TotalMissingAlias}{vbcrlf}")
                            logAction($"Stats updated: {results.AliasRecordsFound}{vbcrlf}")
                        End If
                    End If

                Catch ex As Exception
                    If logAction IsNot Nothing Then
                        logAction($"ERROR during ALIAS processing: {ex.Message}{vbcrlf}")
                    End If
                End Try

            End If
            ' FINAL SUMMARY
            If logAction IsNot Nothing Then
                logAction($"{vbcrlf}=== COMPLETE PROCESSING SUMMARY ==={vbcrlf}")
                logAction($"PHASE 1 - MP3 Files: {results.ProcessedFiles}/{results.TotalFiles} processed{vbcrlf}")
                logAction($"PHASE 2 - YT_Songs without ALIAS: {results.AliasRecordsFound}/{results.TotalMissingAlias} processed{vbcrlf}")
                logAction($"Total stats updates: {results.StatsUpdated + results.AliasRecordsFound}{vbcrlf}")
                logAction($"Processing completed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{vbcrlf}")
            End If

            UpdateStatus(statusLabel, $"Complete: {results.ProcessedFiles + results.AliasRecordsFound} total processed")

        Catch ex As Exception
            If logAction IsNot Nothing Then
                logAction($"FATAL ERROR during processing: {ex.Message}{vbcrlf}")
            End If
            UpdateStatus(statusLabel, "Processing failed with error")
        Finally
            ' Cleanup
            YouTubeSearchModule.CleanupYouTubeSearch()
            YouTubeStatsModule.CleanupYouTubeStats()

            If progressBar IsNot Nothing Then
                progressBar.Value = progressBar.Maximum
            End If
        End Try

        Return results
    End Function

    ''' <summary>
    ''' Process a TreeView node and all its children recursively, handling MP3 files one by one
    ''' </summary>
    Private Async Function ProcessNodeAndChildren(node As TreeNode,
                                                fileCounter As FileCounter,
                                                results As ProcessingResults,
                                                useAPI As Boolean,
                                                apiKey As String,
                                                logAction As Action(Of String),
                                                progressBar As ToolStripProgressBar,
                                                statusLabel As ToolStripStatusLabel,
                                                debugMode As Boolean,
                                                cancellationToken As CancellationToken,
                                                  language As String) As Task

        Dim dirNode As DirectoryTreeViewModule.DirectoryTreeNode = TryCast(node, DirectoryTreeViewModule.DirectoryTreeNode)

        ' Process this node if it's a checked MP3 file
        If dirNode IsNot Nothing AndAlso dirNode.Checked AndAlso
           Not dirNode.IsFolder AndAlso dirNode.FileFullPath.ToLower().EndsWith(".mp3") Then

            ' Check for cancellation before processing each file
            If cancellationToken.IsCancellationRequested Then
                cancellationToken.ThrowIfCancellationRequested()
            End If

            fileCounter.Increment()
            Await ProcessSingleMP3File(dirNode.FileFullPath, fileCounter.Current, results, useAPI, apiKey,
                                     logAction, progressBar, statusLabel, debugMode, cancellationToken, language)
        End If

        ' Recursively process child nodes
        For Each childNode As TreeNode In node.Nodes
            ' Check for cancellation before processing each child
            If cancellationToken.IsCancellationRequested Then
                cancellationToken.ThrowIfCancellationRequested()
            End If

            Await ProcessNodeAndChildren(childNode, fileCounter, results, useAPI, apiKey,
                                       logAction, progressBar, statusLabel, debugMode, cancellationToken, language)
        Next
    End Function

    ''' <summary>
    ''' Process a single MP3 file: Parse info -> Check database -> Search YouTube -> Get stats -> Update database
    ''' </summary>
    Private Async Function ProcessSingleMP3File(filePath As String,
                                              fileIndex As Integer,
                                              results As ProcessingResults,
                                              useAPI As Boolean,
                                              apiKey As String,
                                              logAction As Action(Of String),
                                              progressBar As ToolStripProgressBar,
                                              statusLabel As ToolStripStatusLabel,
                                              debugMode As Boolean,
                                              cancellationToken As CancellationToken,
                                                language As String) As Task

        Dim fileName As String = Path.GetFileName(filePath)

        Try
            ' Update progress
            If progressBar IsNot Nothing Then
                progressBar.Value = fileIndex
            End If
            UpdateStatus(statusLabel, $"Processing {fileIndex}/{results.TotalFiles}: {fileName}")

            If logAction IsNot Nothing Then
                logAction($"--- Processing file {fileIndex}/{results.TotalFiles}: {fileName} ---{vbcrlf}")
            End If

            ' STEP 1: Parse MP3 info
            If logAction IsNot Nothing Then
                logAction($"Step 1: Parsing MP3 metadata...{vbcrlf}")
            End If

            Dim mp3Info As Mp3Info = ParseMp3InfoEnhanced(filePath)
            If mp3Info Is Nothing Then
                If logAction IsNot Nothing Then
                    logAction($"ERROR: Could not parse MP3 info for {fileName}{vbcrlf}{vbcrlf}")
                End If
                results.ErrorFiles += 1
                Return
            End If

            Dim artistTitle As String = $"{mp3Info.Artist} - {mp3Info.Title}"
            If logAction IsNot Nothing Then
                logAction($"MP3 Info: {artistTitle}{vbcrlf}")
            End If

            ' STEP 2: Check database for existing record
            If logAction IsNot Nothing Then
                logAction($"Step 2: Checking database for existing record by ALIAS...{vbcrlf}")
            End If

            Dim videoId As String = GetVideoIdByAliasLike(artistTitle)

            If Not String.IsNullOrWhiteSpace(videoId) Then
                If logAction IsNot Nothing Then
                    logAction($"Found existing VideoID in database: {videoId}{vbcrlf}")
                End If
                results.ExistingRecords += 1
            Else
                ' STEP 3: Search YouTube for video (with delay before calling)
                If logAction IsNot Nothing Then
                    logAction($"Step 3: No existing record found. Searching YouTube...{vbcrlf}")
                End If

                ' Add delay before YouTube API/scraping call to be respectful
                If logAction IsNot Nothing Then
                    logAction($"Waiting 1 second before YouTube request...{vbcrlf}")
                End If
                Await Task.Delay(500, cancellationToken) ' 1 second delay with cancellation support

                Dim searchResult As YouTubeSearchModule.YouTubeSearchResult = Nothing

                If useAPI AndAlso Not String.IsNullOrWhiteSpace(apiKey) Then
                    ' Try API first
                    If logAction IsNot Nothing Then
                        logAction($"Searching with YouTube API...{vbcrlf}")
                    End If

                    searchResult = Await YouTubeSearchModule.SearchYouTubeAPI(artistTitle, apiKey, logAction, debugMode)

                    ' Fallback to basic if API failed
                    If searchResult Is Nothing OrElse searchResult.BestMatch Is Nothing Then
                        If logAction IsNot Nothing Then
                            logAction($"API search failed. Waiting 2 seconds before fallback...{vbcrlf}")
                        End If
                        Await Task.Delay(500, cancellationToken) ' Extra delay before fallback with cancellation

                        searchResult = Await YouTubeSearchModule.SearchYouTubeBasic(artistTitle, logAction, debugMode)
                    End If
                Else
                    ' Use basic search only
                    If logAction IsNot Nothing Then
                        logAction($"Searching with basic method...{vbcrlf}")
                    End If
                    searchResult = Await YouTubeSearchModule.SearchYouTubeBasic(artistTitle, logAction, debugMode)
                End If

                If searchResult IsNot Nothing AndAlso searchResult.BestMatch IsNot Nothing Then
                    videoId = searchResult.BestMatch.VideoId

                    If logAction IsNot Nothing Then
                        logAction($"Found YouTube video: {searchResult.BestMatch.Title} by {searchResult.BestMatch.Channel}{vbcrlf}")
                        logAction($"VideoID: {videoId}{vbcrlf}")
                    End If

                    ' STEP 4: Get YouTube stats immediately to get PublishDate for database
                    If logAction IsNot Nothing Then
                        logAction($"Step 4: Getting YouTube stats to obtain PublishDate...{vbcrlf}")
                    End If

                    ' Add delay before stats call
                    If logAction IsNot Nothing Then
                        logAction($"Waiting 1 second before stats request...{vbcrlf}")
                    End If
                    Await Task.Delay(500, cancellationToken) ' 1 second delay with cancellation support

                    Dim statsRecord As YouTubeStatsModule.YTStatsRecord = Nothing

                    If useAPI AndAlso Not String.IsNullOrWhiteSpace(apiKey) Then
                        ' Try API first
                        If logAction IsNot Nothing Then
                            logAction($"Getting stats with YouTube API...{vbcrlf}")
                        End If

                        statsRecord = Await YouTubeStatsModule.GetYouTubeVideoStatsAPI(videoId, apiKey, logAction, debugMode)

                        ' Fallback to basic if API failed
                        If statsRecord Is Nothing Then
                            If logAction IsNot Nothing Then
                                logAction($"API stats failed. Waiting 2 seconds before fallback...{vbcrlf}")
                            End If
                            Await Task.Delay(500, cancellationToken) ' Extra delay before fallback with cancellation

                            statsRecord = Await YouTubeStatsModule.GetYouTubeVideoStats(videoId, logAction, debugMode)
                        End If
                    Else
                        ' Use basic method only
                        If logAction IsNot Nothing Then
                            logAction($"Getting stats with basic method...{vbcrlf}")
                        End If
                        statsRecord = Await YouTubeStatsModule.GetYouTubeVideoStats(videoId, logAction, debugMode)
                    End If

                    If statsRecord IsNot Nothing Then
                        If logAction IsNot Nothing Then
                            logAction($"Stats retrieved: Views={statsRecord.ViewCount:N0}, PublishDate={statsRecord.PublishDate:yyyy-MM-dd}{vbcrlf}")
                        End If

                        ' STEP 5: Insert new song record into database with correct PublishDate
                        If logAction IsNot Nothing Then
                            logAction($"Step 5: Inserting new song record with PublishDate into database...{vbcrlf}")
                        End If

                        Try
                            InsertYTSong(artistTitle, searchResult.BestMatch.Title, videoId, statsRecord.PublishDate, searchResult.BestMatch.Channel, language)
                            If logAction IsNot Nothing Then
                                logAction($"Successfully inserted new song record with PublishDate{vbcrlf}")
                            End If
                            results.NewRecords += 1
                        Catch ex As Exception
                            If logAction IsNot Nothing Then
                                logAction($"ERROR inserting song record: {ex.Message}{vbcrlf}")
                            End If
                            results.ErrorFiles += 1
                            '  Return
                        End Try

                        ' STEP 6: Insert stats into database
                        If logAction IsNot Nothing Then
                            logAction($"Step 6: Saving stats to database...{vbcrlf}")
                        End If

                        Try
                            ' Use merge method for better performance and handling of duplicates
                            InsertYTStatsWithMerge(videoId, statsRecord.ViewCount, Today.Date)

                            If logAction IsNot Nothing Then
                                logAction($"Stats saved: Views={statsRecord.ViewCount:N0}, Date={Today.Date:yyyy-MM-dd}{vbcrlf}")
                            End If
                            results.StatsUpdated += 1
                        Catch ex As Exception
                            If logAction IsNot Nothing Then
                                logAction($"ERROR saving stats: {ex.Message}{vbcrlf}")
                            End If
                            ' Don't increment error count since we got the video and song record, just failed to save stats
                        End Try
                    Else
                        If logAction IsNot Nothing Then
                            logAction($"Could not get stats for VideoID: {videoId}. Cannot insert song record without PublishDate.{vbcrlf}")
                        End If
                        results.ErrorFiles += 1
                        Return
                    End If
                Else
                    If logAction IsNot Nothing Then
                        logAction($"No suitable YouTube video found for: {artistTitle}{vbcrlf}{vbcrlf}")
                    End If
                    results.NotFoundFiles += 1
                    Return
                End If
            End If

            ' For existing records, still get current stats
            If Not String.IsNullOrWhiteSpace(videoId) AndAlso results.ExistingRecords > 0 Then
                If logAction IsNot Nothing Then
                    logAction($"Step 3: Getting current YouTube stats for existing record...{vbcrlf}")
                End If

                ' Add delay before stats call
                If logAction IsNot Nothing Then
                    logAction($"Waiting 1 second before stats request...{vbcrlf}")
                End If
                Await Task.Delay(500, cancellationToken) ' 1 second delay with cancellation support

                Dim existingStatsRecord As YouTubeStatsModule.YTStatsRecord = Nothing

                If useAPI AndAlso Not String.IsNullOrWhiteSpace(apiKey) Then
                    ' Try API first
                    If logAction IsNot Nothing Then
                        logAction($"Getting stats with YouTube API...{vbcrlf}")
                    End If

                    existingStatsRecord = Await YouTubeStatsModule.GetYouTubeVideoStatsAPI(videoId, apiKey, logAction, debugMode)

                    ' Fallback to basic if API failed
                    If existingStatsRecord Is Nothing Then
                        If logAction IsNot Nothing Then
                            logAction($"API stats failed. Waiting 2 seconds before fallback...{vbcrlf}")
                        End If
                        Await Task.Delay(500, cancellationToken) ' Extra delay before fallback with cancellation

                        existingStatsRecord = Await YouTubeStatsModule.GetYouTubeVideoStats(videoId, logAction, debugMode)
                    End If
                Else
                    ' Use basic method only
                    If logAction IsNot Nothing Then
                        logAction($"Getting stats with basic method...{vbcrlf}")
                    End If
                    existingStatsRecord = Await YouTubeStatsModule.GetYouTubeVideoStats(videoId, logAction, debugMode)
                End If

                ' Save current stats for existing record
                If existingStatsRecord IsNot Nothing Then
                    Try
                        ' Use merge method for better performance and handling of duplicates
                        InsertYTStatsWithMerge(videoId, existingStatsRecord.ViewCount, Today.Date)

                        If logAction IsNot Nothing Then
                            logAction($"Current stats saved: Views={existingStatsRecord.ViewCount:N0}, Date={Today.Date:yyyy-MM-dd}{vbcrlf}")
                        End If
                        results.StatsUpdated += 1
                    Catch ex As Exception
                        If logAction IsNot Nothing Then
                            logAction($"ERROR saving current stats: {ex.Message}{vbcrlf}")
                        End If
                        ' Don't increment error count since we have existing record
                    End Try
                Else
                    If logAction IsNot Nothing Then
                        logAction($"Could not get current stats for existing VideoID: {videoId}{vbcrlf}")
                    End If
                End If
            End If

            If logAction IsNot Nothing Then
                logAction($"✓ Completed processing: {fileName}{vbcrlf}{vbcrlf}")
            End If

            results.ProcessedFiles += 1

            ' Final delay before next file to be extra respectful
            If fileIndex < results.TotalFiles Then ' Don't delay after the last file
                If logAction IsNot Nothing Then
                    logAction($"Waiting 2 seconds before next file...{vbcrlf}")
                End If
                Await Task.Delay(500, cancellationToken) ' 2 second delay between files with cancellation support
            End If

        Catch ex As Exception
            If logAction IsNot Nothing Then
                logAction($"ERROR processing {fileName}: {ex.Message}{vbcrlf}{vbcrlf}")
            End If
            results.ErrorFiles += 1
        End Try
    End Function

    ''' <summary>
    ''' Count total checked MP3 files in TreeView for progress tracking
    ''' </summary>
    Private Function CountCheckedMP3FilesInTreeView(treeView As TreeView) As Integer
        Dim count As Integer = 0

        If treeView Is Nothing Then Return 0

        For Each rootNode As TreeNode In treeView.Nodes
            count += CountCheckedMP3FilesInNode(rootNode)
        Next

        Return count
    End Function

    ''' <summary>
    ''' Recursively count checked MP3 files in a node
    ''' </summary>
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

    ''' <summary>
    ''' Update status label helper
    ''' </summary>
    Private Sub UpdateStatus(statusLabel As ToolStripStatusLabel, message As String)
        If statusLabel IsNot Nothing Then
            statusLabel.Text = message
            statusLabel.Owner?.Refresh()
            Application.DoEvents()
        End If
    End Sub

    ''' <summary>
    ''' Helper class to track file counter across async calls (replaces ByRef parameter)
    ''' </summary>
    Private Class FileCounter
        Private _current As Integer = 0

        Public Sub Increment()
            _current += 1
        End Sub

        Public ReadOnly Property Current As Integer
            Get
                Return _current
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Results class to track processing statistics
    ''' Enhanced Results class to track both MP3 and ALIAS processing statistics
    ''' </summary>
    Public Class ProcessingResults
        ' MP3 processing properties
        Public Property TotalFiles As Integer = 0
        Public Property ProcessedFiles As Integer = 0
        Public Property ExistingRecords As Integer = 0
        Public Property NewRecords As Integer = 0
        Public Property StatsUpdated As Integer = 0
        Public Property NotFoundFiles As Integer = 0
        Public Property ErrorFiles As Integer = 0

        ' ALIAS processing properties
        Public Property TotalMissingAlias As Integer = 0
        Public Property AliasRecordsFound As Integer = 0  ' Successfully processed ALIAS records
    End Class
#End Region

End Module
