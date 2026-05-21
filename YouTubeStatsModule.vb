' =====================================================
' MODULE: YouTubeStatsModule.vb
' Contains YouTube video statistics extraction logic
' Scrapes video pages to get publish date and view count
' =====================================================
Imports System.Net.Http
Imports System.Net
Imports System.Text.RegularExpressions
Imports System.Globalization
Imports System.Threading

Module YouTubeStatsModule

    ' Data class for YouTube video statistics
    Public Class YTStatsRecord
        Public Property PublishDate As Date
        Public Property ViewCount As Long
        Public Property YTVideoID As String
        Public Property Title As String = ""
        Public Property Channel As String = ""
    End Class

    ' HTTP client for web requests (separate from search module)
    Private statsHttpClient As HttpClient

    ' Initialize HTTP client for stats module
    Public Sub InitializeYouTubeStats()
        If statsHttpClient Is Nothing Then
            ' Create HttpClientHandler to handle compression automatically
            Dim handler As New HttpClientHandler()
            If handler.SupportsAutomaticDecompression Then
                handler.AutomaticDecompression = DecompressionMethods.GZip Or DecompressionMethods.Deflate
            End If

            statsHttpClient = New HttpClient(handler)
            ' Set timeout for stats requests
            statsHttpClient.Timeout = TimeSpan.FromSeconds(30)
        End If
    End Sub

    ' Cleanup HTTP client for stats module
    Public Sub CleanupYouTubeStats()
        If statsHttpClient IsNot Nothing Then
            statsHttpClient.Dispose()
            statsHttpClient = Nothing
        End If
    End Sub

    ' Validate YouTube VideoID format (11 characters, alphanumeric + - and _)
    Private Function IsValidVideoId(videoId As String) As Boolean
        If String.IsNullOrWhiteSpace(videoId) OrElse videoId.Length <> 11 Then
            Return False
        End If

        ' Check if contains only valid YouTube VideoID characters
        Dim validPattern As String = "^[a-zA-Z0-9_-]{11}$"
        Return Regex.IsMatch(videoId, validPattern)
    End Function

    ' Main function to get YouTube video statistics using API
    Public Async Function GetYouTubeVideoStatsAPI(videoId As String, apiKey As String, Optional logAction As Action(Of String) = Nothing, Optional debugMode As Boolean = False) As Task(Of YTStatsRecord)
        Try
            ' Initialize if needed
            InitializeYouTubeStats()

            ' Validate VideoID format
            If Not IsValidVideoId(videoId) Then
                If logAction IsNot Nothing Then
                    logAction($"Invalid VideoID format: {videoId}{vbcrlf}")
                End If
                Return Nothing
            End If

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Getting API stats for VideoID: {videoId}{vbcrlf}")
            End If

            ' Construct YouTube Data API v3 videos endpoint
            ' We need snippet (for publishedAt) and statistics (for viewCount)
            Dim encodedVideoId As String = WebUtility.UrlEncode(videoId)
            Dim apiUrl As String = $"https://www.googleapis.com/youtube/v3/videos?part=snippet,statistics&id={encodedVideoId}&key={apiKey}"

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: API URL: {apiUrl}{vbcrlf}")
            End If

            If logAction IsNot Nothing Then
                logAction($"Fetching API stats for VideoID: {videoId}{vbcrlf}")
            End If

            ' Make API request
            Dim response As HttpResponseMessage = Await statsHttpClient.GetAsync(apiUrl)

            If Not response.IsSuccessStatusCode Then
                If logAction IsNot Nothing Then
                    logAction($"API Error {response.StatusCode}: {response.ReasonPhrase}{vbcrlf}")
                End If
                Return Nothing
            End If

            Dim jsonResponse As String = Await response.Content.ReadAsStringAsync()

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Received API JSON response, length: {jsonResponse.Length}{vbcrlf}")

                ' Show first part of JSON for debugging
                Dim jsonPreview As String = jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length))
                logAction($"DEBUG: JSON preview (first 500 chars): {jsonPreview}{vbcrlf}")

                ' Save JSON to debug file
                Try
                    Dim debugFileName As String = $"youtube_api_debug_{videoId}_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                    Dim debugFilePath As String = System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, debugFileName)
                    System.IO.File.WriteAllText(debugFilePath, jsonResponse, System.Text.Encoding.UTF8)
                    logAction($"DEBUG: JSON saved to file: {debugFileName}{vbcrlf}")
                    logAction($"DEBUG: Full path: {debugFilePath}{vbcrlf}")
                Catch ex As Exception
                    logAction($"DEBUG: Could not save JSON file: {ex.Message}{vbcrlf}")
                End Try
            End If

            ' Check if video exists in API response
            If jsonResponse.Contains("""pageInfo"":{""totalResults"":0") Then
                If logAction IsNot Nothing Then
                    logAction($"Video not found in API: {videoId}{vbcrlf}")
                End If
                Return Nothing
            End If

            ' Extract publish date and view count from API response
            Dim publishDate As Date? = ExtractPublishDateFromAPI(jsonResponse, logAction, debugMode)
            Dim viewCount As Long? = ExtractViewCountFromAPI(jsonResponse, logAction, debugMode)

            ' Additional debug info if extraction failed
            If debugMode AndAlso logAction IsNot Nothing Then
                If publishDate Is Nothing Then
                    logAction($"DEBUG: PublishDate extraction failed. Searching for any date-like patterns...{vbcrlf}")
                    ' Look for any date patterns in the JSON
                    Dim anyDatePattern As String = "\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}"
                    Dim dateMatches As MatchCollection = Regex.Matches(jsonResponse, anyDatePattern)
                    logAction($"DEBUG: Found {dateMatches.Count} date patterns in JSON{vbcrlf}")
                    For i As Integer = 0 To Math.Min(2, dateMatches.Count - 1)
                        logAction($"DEBUG: Date pattern {i + 1}: {dateMatches(i).Value}{vbcrlf}")
                    Next
                End If

                If viewCount Is Nothing Then
                    logAction($"DEBUG: ViewCount extraction failed. Searching for any number patterns...{vbcrlf}")
                    ' Look for any large numbers that could be view counts
                    Dim numberPattern As String = """(\d{4,})"""
                    Dim numberMatches As MatchCollection = Regex.Matches(jsonResponse, numberPattern)
                    logAction($"DEBUG: Found {numberMatches.Count} large number patterns in JSON{vbcrlf}")
                    For i As Integer = 0 To Math.Min(3, numberMatches.Count - 1)
                        logAction($"DEBUG: Number pattern {i + 1}: {numberMatches(i).Groups(1).Value}{vbcrlf}")
                    Next
                End If
            End If

            ' Validate that we got both required values
            If publishDate Is Nothing OrElse viewCount Is Nothing Then
                If logAction IsNot Nothing Then
                    logAction($"Failed to extract required API stats - PublishDate: {publishDate IsNot Nothing}, ViewCount: {viewCount IsNot Nothing}{vbcrlf}")
                End If
                Return Nothing
            End If

            ' Create and return stats record
            Dim statsRecord As New YTStatsRecord With {
                .YTVideoID = videoId,
                .PublishDate = publishDate.Value,
                .ViewCount = viewCount.Value,
                .Title = ExtractTitleFromAPI(jsonResponse),
                .Channel = ExtractChannelFromAPI(jsonResponse)
            }

            If logAction IsNot Nothing Then
                logAction($"Successfully extracted API stats - Publish: {statsRecord.PublishDate:yyyy-MM-dd}, Views: {statsRecord.ViewCount:N0}{vbcrlf}")
            End If

            Return statsRecord

        Catch ex As Exception
            If logAction IsNot Nothing Then
                logAction($"Error getting YouTube API stats for {videoId}: {ex.Message}{vbcrlf}")
                If debugMode Then
                    logAction($"DEBUG: Full exception: {ex.ToString()}{vbcrlf}")
                End If
            End If
            Return Nothing
        End Try
    End Function

    Private Function ExtractTitleFromAPI(jsonResponse As String) As String
        Try
            Dim match As Match = Regex.Match(jsonResponse, """title"":\s*""((?:[^""\\]|\\.)*)""")
            If match.Success Then Return UnescapeJsonString(match.Groups(1).Value)
            Return ""
        Catch
            Return ""
        End Try
    End Function

    Private Function ExtractChannelFromAPI(jsonResponse As String) As String
        Try
            Dim match As Match = Regex.Match(jsonResponse, """channelTitle"":\s*""((?:[^""\\]|\\.)*)""")
            If match.Success Then Return UnescapeJsonString(match.Groups(1).Value)
            Return ""
        Catch
            Return ""
        End Try
    End Function

    ' Extract publish date from YouTube Data API v3 JSON response
    Private Function ExtractPublishDateFromAPI(jsonResponse As String, Optional logAction As Action(Of String) = Nothing, Optional debugMode As Boolean = False) As Date?
        Try
            ' Simple pattern for: "publishedAt": "2025-05-03T10:01:23Z"
            Dim publishDatePattern As String = """publishedAt"":\s*""([^""]+)"""
            Dim match As Match = Regex.Match(jsonResponse, publishDatePattern)

            If match.Success Then
                Dim dateString As String = match.Groups(1).Value

                If debugMode AndAlso logAction IsNot Nothing Then
                    logAction($"DEBUG: Found publishedAt: {dateString}{vbcrlf}")
                End If

                ' Parse ISO 8601 date format
                Dim publishDate As Date
                If Date.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, publishDate) Then
                    If debugMode AndAlso logAction IsNot Nothing Then
                        logAction($"DEBUG: Parsed date: {publishDate:yyyy-MM-dd}{vbcrlf}")
                    End If
                    Return publishDate.Date
                End If
            End If

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: publishedAt not found or could not parse{vbcrlf}")
            End If

            Return Nothing

        Catch ex As Exception
            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Exception in ExtractPublishDateFromAPI: {ex.Message}{vbcrlf}")
            End If
            Return Nothing
        End Try
    End Function

    ' Extract view count from YouTube Data API v3 JSON response
    Private Function ExtractViewCountFromAPI(jsonResponse As String, Optional logAction As Action(Of String) = Nothing, Optional debugMode As Boolean = False) As Long?
        Try
            ' Simple pattern for: "viewCount": "26951461"
            Dim viewCountPattern As String = """viewCount"":\s*""([^""]+)"""
            Dim match As Match = Regex.Match(jsonResponse, viewCountPattern)

            If match.Success Then
                Dim viewCountString As String = match.Groups(1).Value

                If debugMode AndAlso logAction IsNot Nothing Then
                    logAction($"DEBUG: Found viewCount: {viewCountString}{vbcrlf}")
                End If

                ' Parse numeric value
                Dim viewCount As Long
                If Long.TryParse(viewCountString, viewCount) Then
                    If debugMode AndAlso logAction IsNot Nothing Then
                        logAction($"DEBUG: Parsed viewCount: {viewCount:N0}{vbcrlf}")
                    End If
                    Return viewCount
                End If
            End If

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: viewCount not found or could not parse{vbcrlf}")
            End If

            Return Nothing

        Catch ex As Exception
            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Exception in ExtractViewCountFromAPI: {ex.Message}{vbcrlf}")
            End If
            Return Nothing
        End Try
    End Function

    ' Main function to get YouTube video statistics (Web Scraping)
    Public Async Function GetYouTubeVideoStats(videoId As String, Optional logAction As Action(Of String) = Nothing, Optional debugMode As Boolean = False) As Task(Of YTStatsRecord)
        Try
            ' Initialize if needed
            InitializeYouTubeStats()

            ' Validate VideoID format
            If Not IsValidVideoId(videoId) Then
                If logAction IsNot Nothing Then
                    logAction($"Invalid VideoID format: {videoId}{vbcrlf}")
                End If
                Return Nothing
            End If

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Getting stats for VideoID: {videoId}{vbcrlf}")
            End If

            ' Construct YouTube video URL
            Dim videoUrl As String = $"https://www.youtube.com/watch?v={videoId}"

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Video URL: {videoUrl}{vbcrlf}")
            End If

            ' Set browser-like headers to avoid blocking
            statsHttpClient.DefaultRequestHeaders.Clear()
            statsHttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
            statsHttpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8")
            statsHttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9")
            ' Remove Accept-Encoding header - let HttpClient handle compression automatically
            statsHttpClient.DefaultRequestHeaders.Add("DNT", "1")
            statsHttpClient.DefaultRequestHeaders.Add("Connection", "keep-alive")
            statsHttpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1")

            If logAction IsNot Nothing Then
                logAction($"Fetching stats for VideoID: {videoId}{vbcrlf}")
            End If

            ' Make request to YouTube video page
            Dim response As HttpResponseMessage = Await statsHttpClient.GetAsync(videoUrl)

            If Not response.IsSuccessStatusCode Then
                If logAction IsNot Nothing Then
                    logAction($"HTTP Error {response.StatusCode}: {response.ReasonPhrase}{vbcrlf}")
                End If
                Return Nothing
            End If

            Dim html As String = Await response.Content.ReadAsStringAsync()

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Received HTML response, length: {html.Length}{vbcrlf}")

                ' Save HTML to debug file
                Try
                    Dim debugFileName As String = $"youtube_debug_{videoId}_{DateTime.Now:yyyyMMdd_HHmmss}.html"
                    Dim debugFilePath As String = System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, debugFileName)
                    System.IO.File.WriteAllText(debugFilePath, html, System.Text.Encoding.UTF8)
                    logAction($"DEBUG: HTML saved to file: {debugFileName}{vbcrlf}")
                    logAction($"DEBUG: Full path: {debugFilePath}{vbcrlf}")
                Catch ex As Exception
                    logAction($"DEBUG: Could not save HTML file: {ex.Message}{vbcrlf}")
                End Try
            End If

            ' Check if video exists (not deleted/private/unavailable)
            If html.Contains("This video is unavailable") OrElse
               html.Contains("Video unavailable") OrElse
               html.Contains("Private video") OrElse
               html.Contains("This video has been removed") Then
                If logAction IsNot Nothing Then
                    logAction($"Video is unavailable or private: {videoId}{vbcrlf}")
                End If
                Return Nothing
            End If

            ' Extract publish date and view count
            Dim publishDate As Date? = ExtractPublishDate(html, logAction, debugMode)
            Dim viewCount As Long? = ExtractViewCount(html, logAction, debugMode)

            ' Validate that we got both required values
            If publishDate Is Nothing OrElse viewCount Is Nothing Then
                If logAction IsNot Nothing Then
                    logAction($"Failed to extract required stats - PublishDate: {publishDate IsNot Nothing}, ViewCount: {viewCount IsNot Nothing}{vbcrlf}")
                End If
                Return Nothing
            End If

            ' Create and return stats record
            Dim statsRecord As New YTStatsRecord With {
                .YTVideoID = videoId,
                .PublishDate = publishDate.Value,
                .ViewCount = viewCount.Value
            }

            If logAction IsNot Nothing Then
                logAction($"Successfully extracted stats - Publish: {statsRecord.PublishDate:yyyy-MM-dd}, Views: {statsRecord.ViewCount:N0}{vbcrlf}")
            End If

            Return statsRecord

        Catch ex As Exception
            If logAction IsNot Nothing Then
                logAction($"Error getting YouTube stats for {videoId}: {ex.Message}{vbcrlf}")
                If debugMode Then
                    logAction($"DEBUG: Full exception: {ex.ToString()}{vbcrlf}")
                End If
            End If
            Return Nothing
        End Try
    End Function

    ' Extract publish date from YouTube page HTML
    Private Function ExtractPublishDate(html As String, Optional logAction As Action(Of String) = Nothing, Optional debugMode As Boolean = False) As Date?
        Try
            ' Search pattern: "publishDate":"2023-04-12T09:59:17-07:00"
            Dim publishDatePattern As String = """publishDate"":""([^""]+)"""
            Dim match As Match = Regex.Match(html, publishDatePattern)

            If match.Success Then
                Dim dateString As String = match.Groups(1).Value

                If debugMode AndAlso logAction IsNot Nothing Then
                    logAction($"DEBUG: Found publishDate string: {dateString}{vbcrlf}")
                End If

                ' Parse ISO 8601 date format (with timezone)
                Dim publishDate As Date
                If Date.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, publishDate) Then
                    If debugMode AndAlso logAction IsNot Nothing Then
                        logAction($"DEBUG: Parsed publishDate: {publishDate:yyyy-MM-dd}{vbcrlf}")
                    End If
                    Return publishDate.Date ' Return only date part, ignore time
                Else
                    If debugMode AndAlso logAction IsNot Nothing Then
                        logAction($"DEBUG: Failed to parse publishDate: {dateString}{vbcrlf}")
                    End If
                End If
            Else
                If debugMode AndAlso logAction IsNot Nothing Then
                    logAction($"DEBUG: publishDate pattern not found in HTML{vbcrlf}")

                    ' Show a sample of HTML around where we might expect the date
                    Dim sampleText As String = ""
                    Dim publishIndex As Integer = html.IndexOf("publish", StringComparison.OrdinalIgnoreCase)
                    If publishIndex >= 0 Then
                        Dim startIndex As Integer = Math.Max(0, publishIndex - 100)
                        Dim length As Integer = Math.Min(300, html.Length - startIndex)
                        sampleText = html.Substring(startIndex, length)
                        logAction($"DEBUG: HTML sample around 'publish': {sampleText}{vbcrlf}")
                    End If

                    ' Try to find any date-like patterns
                    Dim datePatterns As String() = {
                        """uploadDate"":""([^""]+)""",
                        """datePublished"":""([^""]+)""",
                        """published"":""([^""]+)""",
                        "\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}"
                    }

                    For Each pattern As String In datePatterns
                        Dim testMatch As Match = Regex.Match(html, pattern)
                        If testMatch.Success Then
                            logAction($"DEBUG: Found alternative date pattern '{pattern}': {testMatch.Groups(1).Value}{vbcrlf}")
                        End If
                    Next
                End If
            End If

            Return Nothing

        Catch ex As Exception
            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Exception in ExtractPublishDate: {ex.Message}{vbcrlf}")
            End If
            Return Nothing
        End Try
    End Function

    ' Extract view count from YouTube page HTML
    Private Function ExtractViewCount(html As String, Optional logAction As Action(Of String) = Nothing, Optional debugMode As Boolean = False) As Long?
        Try
            ' Search pattern: "viewCount":"29829387"
            Dim viewCountPattern As String = """viewCount"":""([^""]+)"""
            Dim match As Match = Regex.Match(html, viewCountPattern)

            If match.Success Then
                Dim viewCountString As String = match.Groups(1).Value

                If debugMode AndAlso logAction IsNot Nothing Then
                    logAction($"DEBUG: Found viewCount string: {viewCountString}{vbcrlf}")
                End If

                ' Parse numeric value
                Dim viewCount As Long
                If Long.TryParse(viewCountString, viewCount) Then
                    If debugMode AndAlso logAction IsNot Nothing Then
                        logAction($"DEBUG: Parsed viewCount: {viewCount:N0}{vbcrlf}")
                    End If
                    Return viewCount
                Else
                    If debugMode AndAlso logAction IsNot Nothing Then
                        logAction($"DEBUG: Failed to parse viewCount: {viewCountString}{vbcrlf}")
                    End If
                End If
            Else
                If debugMode AndAlso logAction IsNot Nothing Then
                    logAction($"DEBUG: viewCount pattern not found in HTML{vbcrlf}")

                    ' Show a sample of HTML around where we might expect view count
                    Dim sampleText As String = ""
                    Dim viewIndex As Integer = html.IndexOf("view", StringComparison.OrdinalIgnoreCase)
                    If viewIndex >= 0 Then
                        Dim startIndex As Integer = Math.Max(0, viewIndex - 100)
                        Dim length As Integer = Math.Min(300, html.Length - startIndex)
                        sampleText = html.Substring(startIndex, length)
                        logAction($"DEBUG: HTML sample around 'view': {sampleText}{vbcrlf}")
                    End If

                    ' Try alternative patterns as fallback
                    Dim altPatterns As String() = {
                        """viewCount"":\s*""([^""]+)""",
                        """view_count"":""([^""]+)""",
                        """interactionCount"":""([^""]+)""",
                        """views"":""([^""]+)""",
                        "\d+,?\d*\s*views?",
                        "\d+\s*views?"
                    }

                    For Each altPattern As String In altPatterns
                        Dim altMatch As Match = Regex.Match(html, altPattern)
                        If altMatch.Success Then
                            logAction($"DEBUG: Found alternative viewCount pattern '{altPattern}': {altMatch.Groups(1).Value}{vbcrlf}")
                            Dim altViewCount As Long
                            If Long.TryParse(altMatch.Groups(1).Value.Replace(",", ""), altViewCount) Then
                                Return altViewCount
                            End If
                        End If
                    Next
                End If
            End If

            Return Nothing

        Catch ex As Exception
            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Exception in ExtractViewCount: {ex.Message}{vbcrlf}")
            End If
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Process a single VideoID to get current YouTube stats and update database
    ''' This is for YT_Songs records that exist but need current stats
    ''' </summary>
    Public Async Function ProcessSingleVideoIdForStats(songRecord As YTSongRecord,
                                                       fileIndex As Integer,
                                                       totalCount As Integer,
                                                       results As ProcessingResults,
                                                       useAPI As Boolean,
                                                       apiKey As String,
                                                       logAction As Action(Of String),
                                                       progressBar As ToolStripProgressBar,
                                                       statusLabel As ToolStripStatusLabel,
                                                       debugMode As Boolean,
                                                       cancellationToken As CancellationToken) As Task

        Dim videoId As String = songRecord.YTVideoID

        Try
            ' Update progress
            If progressBar IsNot Nothing Then
                progressBar.Value = results.TotalFiles + fileIndex
            End If
            ' UpdateStatus(statusLabel, $"Processing ALIAS stats {fileIndex}/{totalCount}: {videoId}")

            If logAction IsNot Nothing Then
                logAction($"--- Processing VideoID {fileIndex}/{totalCount}: {videoId} ---{vbcrlf}")
                logAction($"YouTube Title: {songRecord.YTArtistTitle}{vbcrlf}")
                logAction($"Channel: {songRecord.YTChannelName}{vbcrlf}")
            End If

            ' Add delay to be respectful to YouTube
            If logAction IsNot Nothing Then
                logAction($"Waiting 1 second before YouTube request...{vbcrlf}")
            End If
            Await Task.Delay(500, cancellationToken)

            ' Get current video stats
            Dim statsRecord As YouTubeStatsModule.YTStatsRecord = Nothing

            If useAPI AndAlso Not String.IsNullOrWhiteSpace(apiKey) Then
                ' Try API first
                If logAction IsNot Nothing Then
                    logAction($"Getting current stats with YouTube API...{vbcrlf}")
                End If

                statsRecord = Await YouTubeStatsModule.GetYouTubeVideoStatsAPI(videoId, apiKey, logAction, debugMode)

                ' Fallback to basic if API failed
                If statsRecord Is Nothing Then
                    If logAction IsNot Nothing Then
                        logAction($"API failed. Waiting 2 seconds before fallback...{vbcrlf}")
                    End If
                    Await Task.Delay(500, cancellationToken)

                    statsRecord = Await YouTubeStatsModule.GetYouTubeVideoStats(videoId, logAction, debugMode)
                End If
            Else
                ' Use basic method only
                If logAction IsNot Nothing Then
                    logAction($"Getting current stats with basic method...{vbcrlf}")
                End If
                statsRecord = Await YouTubeStatsModule.GetYouTubeVideoStats(videoId, logAction, debugMode)
            End If

            If statsRecord IsNot Nothing Then
                ' Update current stats in database
                Try
                    InsertYTStatsWithMerge(videoId, statsRecord.ViewCount, Today.Date)

                    If logAction IsNot Nothing Then
                        logAction($"✓ Stats updated: Views={statsRecord.ViewCount:N0}, Date={Today.Date:yyyy-MM-dd}{vbcrlf}")
                    End If

                    results.AliasRecordsFound += 1

                Catch ex As Exception
                    If logAction IsNot Nothing Then
                        logAction($"ERROR updating stats for VideoID {videoId}: {ex.Message}{vbcrlf}")
                    End If
                    results.ErrorFiles += 1
                End Try
            Else
                If logAction IsNot Nothing Then
                    logAction($"⚠ Could not get current stats for VideoID: {videoId} (video may be deleted/private){vbcrlf}")
                End If
                results.NotFoundFiles += 1
            End If

            If logAction IsNot Nothing Then
                logAction($"✓ Completed processing VideoID: {videoId}{vbcrlf}{vbcrlf}")
            End If

            ' Delay before next video
            If fileIndex < totalCount Then
                If logAction IsNot Nothing Then
                    logAction($"Waiting 2 seconds before next VideoID...{vbcrlf}")
                End If
                Await Task.Delay(500, cancellationToken)
            End If

        Catch ex As Exception
            If logAction IsNot Nothing Then
                logAction($"ERROR processing VideoID {videoId}: {ex.Message}{vbcrlf}{vbcrlf}")
            End If
            results.ErrorFiles += 1
        End Try
    End Function

    ''' <summary>
    ''' Enhanced JSON string extraction that properly handles escaped quotes
    ''' </summary>
    Private Function ExtractJsonString(json As String, key As String) As String
        Try
            ' Pattern that captures the full JSON string value, including escaped quotes
            Dim pattern As String = $"""{key}""\s*:\s*""((?:[^""\\]|\\.)*)"""
            Dim match As Match = Regex.Match(json, pattern)

            If match.Success Then
                Dim value As String = match.Groups(1).Value

                ' Properly unescape JSON string escapes
                value = UnescapeJsonString(value)

                Return value
            End If

            Return ""
        Catch ex As Exception
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' Properly unescape JSON string values while preserving quotes
    ''' </summary>
    Private Function UnescapeJsonString(escapedString As String) As String
        If String.IsNullOrEmpty(escapedString) Then
            Return ""
        End If

        Dim result As String = escapedString

        ' Handle JSON escape sequences in correct order
        result = result.Replace("\\""", """")    ' \" becomes "
        result = result.Replace("\\/", "/")      ' \/ becomes /
        result = result.Replace("\\\\", "\")     ' \\ becomes \
        result = result.Replace("\\b", vbBack)   ' \b becomes backspace
        result = result.Replace("\\f", vbFormFeed) ' \f becomes form feed
        result = result.Replace("\\n", vbLf)     ' \n becomes line feed
        result = result.Replace("\\r", vbCr)     ' \r becomes carriage return
        result = result.Replace("\\t", vbTab)    ' \t becomes tab

        ' Handle Unicode escapes (\uXXXX)
        result = System.Text.RegularExpressions.Regex.Replace(result, "\\u([0-9A-Fa-f]{4})",
        Function(match)
            Dim unicode As String = match.Groups(1).Value
            Dim charCode As Integer = Convert.ToInt32(unicode, 16)
            Return Convert.ToChar(charCode).ToString()
        End Function)

        Return result
    End Function
End Module