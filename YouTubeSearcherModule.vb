' =====================================================
' MODULE: YouTubeSearchModule.vb
' Contains all YouTube search logic - can be reused in other projects
' =====================================================
Imports System.Net.Http
Imports System.Net
Imports System.Text.RegularExpressions
Imports Newtonsoft.Json.Linq

Module YouTubeSearchModule

    ' Data class for YouTube video information
    Public Class YouTubeVideoData
        Public Property VideoId As String
        Public Property URL As String
        Public Property ViewCount As Long
        Public Property UploadDate As Date?
        Public Property AVRate As Integer
        Public Property Title As String
        Public Property Channel As String
    End Class

    ' Result class containing all results and best match
    Public Class YouTubeSearchResult
        Public Property AllResults As List(Of YouTubeVideoData)
        Public Property BestMatch As YouTubeVideoData
        Public Property SearchQuery As String
    End Class

    ' Internal class for processing video matches
    Private Class VideoMatch
        Public Property VideoId As String
        Public Property Context As String
        Public Property Position As Integer
    End Class

    ' HTTP client for web requests (should be disposed properly)
    Private httpClient As HttpClient

    ' Initialize HTTP client
    Public Sub InitializeYouTubeSearch()
        If httpClient Is Nothing Then
            httpClient = New HttpClient()
        End If
    End Sub

    ' Cleanup HTTP client
    Public Sub CleanupYouTubeSearch()
        If httpClient IsNot Nothing Then
            httpClient.Dispose()
            httpClient = Nothing
        End If
    End Sub

    ' Main search function using YouTube Data API v3 - returns search results with best match
    Public Async Function SearchYouTubeAPI(query As String, apiKey As String, Optional logAction As Action(Of String) = Nothing, Optional debugMode As Boolean = False) As Task(Of YouTubeSearchResult)
        Try
            ' Initialize if needed
            InitializeYouTubeSearch()

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Starting API search for: {query}{vbcrlf}")
            End If

            ' YouTube Data API v3 search endpoint
            Dim encodedQuery As String = WebUtility.UrlEncode(query)
            Dim apiUrl As String = $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&q={encodedQuery}&maxResults=25&key={apiKey}"

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: API URL: {apiUrl}{vbcrlf}")
            End If

            If logAction IsNot Nothing Then
                logAction($"Searching YouTube API for: {query}{vbcrlf}")
            End If

            ' Make API request
            Dim response As HttpResponseMessage = Await httpClient.GetAsync(apiUrl)

            If Not response.IsSuccessStatusCode Then
                If logAction IsNot Nothing Then
                    logAction($"API Error: {response.StatusCode} - {response.ReasonPhrase}{vbcrlf}")
                End If
                Return New YouTubeSearchResult With {
                    .AllResults = New List(Of YouTubeVideoData),
                    .BestMatch = Nothing,
                    .SearchQuery = query
                }
            End If

            Dim jsonResponse As String = Await response.Content.ReadAsStringAsync()

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Received API response, length: {jsonResponse.Length}{vbcrlf}")
            End If

            ' Parse JSON response to extract video data
            Dim results As List(Of YouTubeVideoData) = ParseYouTubeApiResponse(jsonResponse, logAction, debugMode)

            ' Filter for complete results (same as SearchYouTubeBasic)
            Dim validResults As New List(Of YouTubeVideoData)
            For Each video In results
                If IsValidResult(video) Then
                    validResults.Add(video)
                    If validResults.Count >= 10 Then Exit For ' Limit to top 10 valid results
                End If
            Next

            If logAction IsNot Nothing Then
                logAction($"Found {validResults.Count} valid videos from API{vbcrlf}")
            End If

            ' Find the best match using the same logic as SearchYouTubeBasic
            Dim bestMatch As YouTubeVideoData = FindBestMatch(validResults, query, logAction, debugMode)

            ' Create result object with best match info
            Dim searchResult As New YouTubeSearchResult With {
                .AllResults = validResults,
                .BestMatch = bestMatch,
                .SearchQuery = query
            }

            Return searchResult

        Catch ex As Exception
            If logAction IsNot Nothing Then
                logAction($"YouTube API search error: {ex.Message}{vbcrlf}")
            End If
            Return New YouTubeSearchResult With {
                .AllResults = New List(Of YouTubeVideoData),
                .BestMatch = Nothing,
                .SearchQuery = query
            }
        End Try
    End Function

    ' Parse YouTube Data API v3 JSON response
    Private Function ParseYouTubeApiResponse(jsonResponse As String, Optional logAction As Action(Of String) = Nothing, Optional debugMode As Boolean = False) As List(Of YouTubeVideoData)
        Dim results As New List(Of YouTubeVideoData)

        Try
            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Parsing API JSON response{vbcrlf}")
            End If

            ' Use proper JSON parsing to handle escaped quotes correctly
            Try
                ' Try using Newtonsoft.Json for robust parsing first
                Dim jsonObj As JObject = JObject.Parse(jsonResponse)
                Dim items As JArray = TryCast(jsonObj("items"), JArray)

                If items IsNot Nothing Then
                    For Each item As JObject In items
                        Try
                            Dim snippet As JObject = TryCast(item("snippet"), JObject)
                            Dim videoIdToken As JToken = item("id")("videoId")
                            Dim videoId As String = If(videoIdToken IsNot Nothing, videoIdToken.ToString(), "")

                            If snippet IsNot Nothing AndAlso Not String.IsNullOrEmpty(videoId) Then
                                Dim titleToken As JToken = snippet("title")
                                Dim channelToken As JToken = snippet("channelTitle")

                                Dim videoData As New YouTubeVideoData With {
                                    .VideoId = videoId.Trim(),
                                    .URL = $"https://www.youtube.com/watch?v={videoId.Trim()}",
                                    .Title = EnhancedHtmlDecode(If(titleToken IsNot Nothing, titleToken.ToString(), "")),
                                    .Channel = EnhancedHtmlDecode(If(channelToken IsNot Nothing, channelToken.ToString(), ""))
                                }

                                ' The JSON parser automatically handles escaping, so no additional processing needed
                                If Not String.IsNullOrEmpty(videoData.Title) Then
                                    results.Add(videoData)

                                    If debugMode AndAlso logAction IsNot Nothing Then
                                        logAction($"DEBUG: Parsed API video: {videoData.VideoId} - '{videoData.Title}' by '{videoData.Channel}'{vbcrlf}")
                                    End If
                                End If
                            End If

                        Catch ex As Exception
                            If debugMode AndAlso logAction IsNot Nothing Then
                                logAction($"DEBUG: Error parsing individual video: {ex.Message}{vbcrlf}")
                            End If
                        End Try
                    Next
                End If

            Catch jsonEx As Exception
                ' Fallback to regex parsing if JSON parsing fails
                If debugMode AndAlso logAction IsNot Nothing Then
                    logAction($"DEBUG: JSON parsing failed, using regex fallback: {jsonEx.Message}{vbcrlf}")
                End If

                ' Regex pattern that properly captures escaped quotes
                Dim videoPattern As String = """videoId""\s*:\s*""([^""]+)"".*?""title""\s*:\s*""((?:[^""\\]|\\.)*?)"".*?""channelTitle""\s*:\s*""((?:[^""\\]|\\.)*?)"""
                Dim matches As MatchCollection = Regex.Matches(jsonResponse, videoPattern, RegexOptions.Singleline)

                For i As Integer = 0 To Math.Min(matches.Count - 1, 24)
                    Try
                        Dim match As Match = matches(i)

                        Dim videoData As New YouTubeVideoData With {
                            .VideoId = match.Groups(1).Value.Trim(),
                            .URL = String.Format("https://www.youtube.com/watch?v={0}", match.Groups(1).Value.Trim()),
                            .Title = EnhancedHtmlDecode(UnescapeJsonString(match.Groups(2).Value)),
                            .Channel = EnhancedHtmlDecode(UnescapeJsonString(match.Groups(3).Value))
                        }

                        If Not String.IsNullOrEmpty(videoData.VideoId) AndAlso Not String.IsNullOrEmpty(videoData.Title) Then
                            results.Add(videoData)

                            If debugMode AndAlso logAction IsNot Nothing Then
                                logAction($"DEBUG: Regex parsed video {i + 1}: {videoData.VideoId} - '{videoData.Title}' by '{videoData.Channel}'{vbcrlf}")
                            End If
                        End If

                    Catch ex As Exception
                        If debugMode AndAlso logAction IsNot Nothing Then
                            logAction($"DEBUG: Error parsing regex match {i + 1}: {ex.Message}{vbcrlf}")
                        End If
                    End Try
                Next
            End Try

        Catch ex As Exception
            If logAction IsNot Nothing Then
                logAction($"API JSON parsing error: {ex.Message}{vbcrlf}")
            End If
        End Try

        Return results
    End Function
    ' Parse a single video item from YouTube API response
    Private Function ParseSingleVideoItem(videoItemJson As String) As YouTubeVideoData
        Try
            Dim videoData As New YouTubeVideoData

            ' Extract video ID
            Dim videoIdMatch As String = ExtractJsonValue(videoItemJson, "videoId")
            If Not String.IsNullOrEmpty(videoIdMatch) Then
                videoData.VideoId = videoIdMatch
                videoData.URL = $"https://www.youtube.com/watch?v={videoIdMatch}"
            End If

            ' Extract title
            Dim titleMatch As String = ExtractJsonValue(videoItemJson, "title")
            If Not String.IsNullOrEmpty(titleMatch) Then
                videoData.Title = WebUtility.HtmlDecode(titleMatch)
            End If

            ' Extract channel title
            Dim channelMatch As String = ExtractJsonValue(videoItemJson, "channelTitle")
            If Not String.IsNullOrEmpty(channelMatch) Then
                videoData.Channel = WebUtility.HtmlDecode(channelMatch)
            End If

            Return videoData

        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    ' Extract JSON value by key (simple JSON parser for our needs)
    Private Function ExtractJsonValue(json As String, key As String) As String
        Try
            Dim searchPattern As String = $"""{key}"":"""
            Dim startIndex As Integer = json.IndexOf(searchPattern)
            If startIndex = -1 Then Return ""

            startIndex += searchPattern.Length
            Dim endIndex As Integer = json.IndexOf("""", startIndex)
            If endIndex = -1 Then Return ""

            Return json.Substring(startIndex, endIndex - startIndex)
        Catch
            Return ""
        End Try
    End Function

    ' Extract JSON array content
    Private Function ExtractJsonArray(json As String, startIndex As Integer) As String
        Try
            Dim bracketCount As Integer = 0
            Dim inString As Boolean = False
            Dim arrayStart As Integer = -1

            For i As Integer = startIndex To json.Length - 1
                Dim c As Char = json(i)

                If c = """" AndAlso (i = 0 OrElse json(i - 1) <> "\"c) Then
                    inString = Not inString
                ElseIf Not inString Then
                    If c = "["c Then
                        If arrayStart = -1 Then arrayStart = i + 1
                        bracketCount += 1
                    ElseIf c = "]"c Then
                        bracketCount -= 1
                        If bracketCount = 0 AndAlso arrayStart <> -1 Then
                            Return json.Substring(arrayStart, i - arrayStart)
                        End If
                    End If
                End If
            Next

            Return ""
        Catch
            Return ""
        End Try
    End Function

    ' Split JSON array into individual items
    Private Function SplitJsonArray(arrayContent As String) As List(Of String)
        Dim items As New List(Of String)

        Try
            Dim braceCount As Integer = 0
            Dim inString As Boolean = False
            Dim currentItemStart As Integer = 0

            For i As Integer = 0 To arrayContent.Length - 1
                Dim c As Char = arrayContent(i)

                If c = """" AndAlso (i = 0 OrElse arrayContent(i - 1) <> "\"c) Then
                    inString = Not inString
                ElseIf Not inString Then
                    If c = "{"c Then
                        If braceCount = 0 Then currentItemStart = i
                        braceCount += 1
                    ElseIf c = "}"c Then
                        braceCount -= 1
                        If braceCount = 0 Then
                            Dim item As String = arrayContent.Substring(currentItemStart, i - currentItemStart + 1)
                            items.Add(item)
                        End If
                    End If
                End If
            Next
        Catch
            ' Return empty list on parse error
        End Try

        Return items
    End Function

    ' Main search function - returns search results with best match (Web Scraping)
    Public Async Function SearchYouTubeBasic(query As String, Optional logAction As Action(Of String) = Nothing, Optional debugMode As Boolean = False) As Task(Of YouTubeSearchResult)
        Try
            ' Initialize if needed
            InitializeYouTubeSearch()

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Starting search for: {query}{vbcrlf}")
            End If

            Dim encodedQuery As String = WebUtility.UrlEncode(query)
            Dim searchUrl As String = $"https://www.youtube.com/results?search_query={encodedQuery}"

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Search URL: {searchUrl}{vbcrlf}")
            End If

            ' Add headers to make request look more like a real browser
            httpClient.DefaultRequestHeaders.Clear()
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36")
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8")
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5")

            If logAction IsNot Nothing Then
                logAction($"Searching YouTube for: {query}{vbcrlf}")
            End If

            Dim response As HttpResponseMessage = Await httpClient.GetAsync(searchUrl)
            Dim html As String = Await response.Content.ReadAsStringAsync()

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Received HTML response, length: {html.Length}{vbcrlf}")
            End If

            ' Find all video IDs with their surrounding context
            Dim videoMatches As New List(Of VideoMatch)
            Dim results As New List(Of YouTubeVideoData)

            ' Pattern to find video IDs and capture surrounding context
            Dim videoIdPattern As String = """videoId"":""([a-zA-Z0-9_-]{11})"""
            Dim matches As MatchCollection = Regex.Matches(html, videoIdPattern)

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Found {matches.Count} video ID matches{vbcrlf}")
            End If

            For Each match As Match In matches
                Dim videoId As String = match.Groups(1).Value

                ' Get larger context around the match for data extraction
                Dim startIndex As Integer = Math.Max(0, match.Index - 3000)
                Dim endIndex As Integer = Math.Min(html.Length - 1, match.Index + 3000)
                Dim context As String = html.Substring(startIndex, endIndex - startIndex + 1)

                videoMatches.Add(New VideoMatch With {
                    .VideoId = videoId,
                    .Context = context,
                    .Position = match.Index
                })
            Next

            ' Sort by position (first appearance first)
            videoMatches = videoMatches.OrderBy(Function(v) v.Position).ToList()

            ' Extract top 10 results
            Dim resultCount As Integer = Math.Min(10, videoMatches.Count)

            If logAction IsNot Nothing Then
                logAction($"Processing {videoMatches.Count} potential results...{vbcrlf}")
            End If

            ' Process all available results and filter for complete ones
            For i As Integer = 0 To videoMatches.Count - 1
                Try
                    Dim videoData As New YouTubeVideoData With {
                        .VideoId = videoMatches(i).VideoId,
                        .URL = $"https://www.youtube.com/watch?v={videoMatches(i).VideoId}"
                    }

                    ' Extract title and channel from context
                    ExtractTitleAndChannel(videoData, videoMatches(i).Context)


                    ' Only add videos that have all required fields (Title, Channel, VideoId)
                    If IsValidResult(videoData) Then
                        results.Add(videoData)

                        If debugMode AndAlso logAction IsNot Nothing Then
                            logAction($"DEBUG: Added valid video {results.Count}: {videoData.VideoId} - {videoData.Title}{vbcrlf}")
                        End If

                        ' Stop when we have 10 valid results
                        If results.Count >= 10 Then
                            Exit For
                        End If
                    Else
                        If debugMode AndAlso logAction IsNot Nothing Then
                            logAction($"DEBUG: Skipped incomplete video {i + 1}: VideoId={videoData.VideoId}, Title={videoData.Title}, Channel={videoData.Channel}{vbcrlf}")
                        End If
                    End If

                Catch ex As Exception
                    If debugMode AndAlso logAction IsNot Nothing Then
                        logAction($"DEBUG: Error processing video {i + 1}: {ex.Message}{vbcrlf}")
                    End If
                End Try
            Next

            If logAction IsNot Nothing Then
                logAction($"Found {results.Count} valid videos{vbcrlf}")
            End If

            ' Find the best match based on title and artist criteria
            Dim bestMatch As YouTubeVideoData = FindBestMatch(results, query, logAction, debugMode)

            ' Create result object with best match info
            Dim searchResult As New YouTubeSearchResult With {
                .AllResults = results,
                .BestMatch = bestMatch,
                .SearchQuery = query
            }

            Return searchResult

        Catch ex As Exception
            If logAction IsNot Nothing Then
                logAction($"YouTube search error: {ex.Message}{vbcrlf}")
            End If
            Return New YouTubeSearchResult With {
                .AllResults = New List(Of YouTubeVideoData),
                .BestMatch = Nothing,
                .SearchQuery = query
            }
        End Try
    End Function

    ' Common words to ignore in matching
    Private ReadOnly CommonWords As String() = {
        "the", "and", "a", "an", "of", "in", "on", "at", "to", "for",
        "with", "by", "from", "is", "are", "was", "were", "be", "been",
        "have", "has", "had", "do", "does", "did", "will", "would", "could",
        "should", "may", "might", "can", "must", "shall", "this", "that",
        "these", "those", "i", "you", "he", "she", "it", "we", "they",
        "me", "him", "her", "us", "them", "my", "your", "his", "her", "its",
        "our", "their", "ft", "feat", "featuring"
    }

    ' Split text into meaningful words, removing common words and punctuation
    Private Function GetMeaningfulWords(text As String) As List(Of String)
        If String.IsNullOrWhiteSpace(text) Then
            Return New List(Of String)
        End If

        ' Split by various separators - IMPROVED: Added "/" for compound words like "тебя/aranc"
        Dim separators As Char() = {" "c, ","c, "&"c, "("c, ")"c, "["c, "]"c, "{"c, "}"c,
                                   "-"c, "_"c, "."c, "!"c, "?"c, ";"c, ":"c, "'"c, """"c, "/"c}

        Dim words As String() = text.ToLower().Split(separators, StringSplitOptions.RemoveEmptyEntries)
        Dim meaningfulWords As New List(Of String)

        For Each word As String In words
            Dim cleanWord As String = word.Trim()
            ' Remove words that are too short or are common words
            If cleanWord.Length > 1 AndAlso Not CommonWords.Contains(cleanWord) Then
                meaningfulWords.Add(cleanWord)
            End If
        Next

        Return meaningfulWords
    End Function

    ''' <summary>
    ''' Find best match with channel priority and official indicators
    ''' </summary>
    Private Function FindBestMatch(results As List(Of YouTubeVideoData), searchString As String,
                              Optional logAction As Action(Of String) = Nothing,
                              Optional debugMode As Boolean = False) As YouTubeVideoData

        If results.Count = 0 Then Return Nothing

        ' Parse artist and title from search string
        Dim searchArtist As String = ""
        Dim searchTitle As String = ""
        ArtistTitleMatcher2.TrySplitArtistTitle(searchString, searchArtist, searchTitle)

        Dim bestMatch As YouTubeVideoData = Nothing
        Dim bestScore As Double = 0.0

        If debugMode AndAlso logAction IsNot Nothing Then
            logAction($"DEBUG: Starting match for: '{searchString}'{vbCrLf}")
            logAction($"DEBUG: Parsed Artist: '{searchArtist}', Title: '{searchTitle}'{vbCrLf}")
        End If

        For i As Integer = 0 To results.Count - 1
            Dim video As YouTubeVideoData = results(i)

            ' 1. TITLE MATCHING (0-100 points)
            Dim titleMatchScore As Double = ArtistTitleMatcher2.GetMatchPercentage(searchString, video.Title, "auto")
            Dim channelTitleScore As Double = ArtistTitleMatcher2.GetMatchPercentage(searchString, $"{video.Channel} - {video.Title}", "auto")
            Dim baseScore As Double = Math.Max(titleMatchScore, channelTitleScore)

            ' 2. CHANNEL MATCH BONUS (0-50 points) - MAJOR INDICATOR
            Dim channelBonus As Double = 0.0
            If Not String.IsNullOrWhiteSpace(searchArtist) AndAlso Not String.IsNullOrWhiteSpace(video.Channel) Then
                Dim channelMatchPct As Double = ArtistTitleMatcher2.GetMatchPercentage(searchArtist, video.Channel, "auto")
                If channelMatchPct >= 80.0 Then
                    channelBonus = 50.0  ' Strong channel match = artist's official channel
                ElseIf channelMatchPct >= 60.0 Then
                    channelBonus = 25.0  ' Partial channel match
                ElseIf channelMatchPct >= 40.0 Then
                    channelBonus = 10.0  ' Weak channel match
                End If
            End If

            ' 3. OFFICIAL INDICATORS BONUS (0-20 points)
            Dim officialBonus As Double = 0.0
            Dim titleLower As String = video.Title.ToLower()
            If titleLower.Contains("official") Then officialBonus += 10.0
            If titleLower.Contains("music video") Then officialBonus += 5.0
            If titleLower.Contains("official audio") OrElse titleLower.Contains("official video") Then officialBonus += 5.0

            ' 4. CALCULATE FINAL SCORE
            Dim finalScore As Double = baseScore + channelBonus + officialBonus

            If debugMode AndAlso logAction IsNot Nothing Then
                logAction($"DEBUG: Video {i + 1}: '{video.Title}' by '{video.Channel}'{vbCrLf}")
                logAction($"DEBUG: Title Match: {titleMatchScore:F1}%, Channel-Title: {channelTitleScore:F1}%{vbCrLf}")
                logAction($"DEBUG: Base: {baseScore:F1}, Channel Bonus: {channelBonus:F1}, Official Bonus: {officialBonus:F1}{vbCrLf}")
                logAction($"DEBUG: FINAL SCORE: {finalScore:F1}{vbCrLf}")
            End If

            ' Update best match if this score is higher
            If finalScore > bestScore Then
                bestMatch = video
                bestScore = finalScore

                If debugMode AndAlso logAction IsNot Nothing Then
                    logAction($"DEBUG: *** NEW BEST MATCH! Score: {finalScore:F1} ***{vbCrLf}")
                End If
            End If
        Next

        ' Return match if score is acceptable (70% threshold)
        If bestMatch IsNot Nothing AndAlso bestScore >= 70.0 Then
            If logAction IsNot Nothing Then
                logAction($"Best match found with score {bestScore:F1}%: '{bestMatch.Title}' by '{bestMatch.Channel}'{vbCrLf}")
            End If
            Return bestMatch
        Else
            If logAction IsNot Nothing Then
                If bestMatch IsNot Nothing Then
                    logAction($"Best score ({bestScore:F1}%) below threshold (70.0%){vbCrLf}")
                Else
                    logAction($"No suitable match found{vbCrLf}")
                End If
            End If
            Return Nothing
        End If
    End Function

    ' Validate that a video result has all required fields
    Private Function IsValidResult(videoData As YouTubeVideoData) As Boolean
        ' Check if VideoId is present and valid (11 characters, alphanumeric + - and _)
        If String.IsNullOrWhiteSpace(videoData.VideoId) OrElse videoData.VideoId.Length <> 11 Then
            Return False
        End If

        ' Check if Title is present and not an error message
        If String.IsNullOrWhiteSpace(videoData.Title) OrElse
           videoData.Title = "Title not found" OrElse
           videoData.Title = "Error extracting title" Then
            Return False
        End If

        ' Check if Channel is present and not an error message
        If String.IsNullOrWhiteSpace(videoData.Channel) OrElse
           videoData.Channel = "Channel not found" OrElse
           videoData.Channel = "Error extracting channel" Then
            Return False
        End If

        Return True
    End Function

    ' Extract title and channel name from YouTube HTML context
    Private Sub ExtractTitleAndChannel(videoData As YouTubeVideoData, context As String)
        Try
            ' Method 1: Try complex title structure first
            Dim titlePattern As String = """title"":\{""runs"":\[\{""text"":""((?:[^""\\]|\\.)*?)"""
            Dim titleMatch As Match = Regex.Match(context, titlePattern)

            If titleMatch.Success Then
                videoData.Title = EnhancedHtmlDecode(UnescapeJsonString(titleMatch.Groups(1).Value))
            Else
                ' Method 2: Try simple title pattern
                videoData.Title = EnhancedHtmlDecode(UnescapeJsonString(ExtractJsonString(context, "title")))

                ' Method 3: Try alternative title patterns if still empty
                If String.IsNullOrEmpty(videoData.Title) Then
                    Dim altPatterns As String() = {
                    """videoTitle"":""((?:[^""\\]|\\.)*?)""",
                    """headline"":""((?:[^""\\]|\\.)*?)""",
                    "<title[^>]*>(.*?)</title>"
                }

                    For Each pattern In altPatterns
                        Dim altMatch As Match = Regex.Match(context, pattern, RegexOptions.IgnoreCase)
                        If altMatch.Success Then
                            videoData.Title = EnhancedHtmlDecode(UnescapeJsonString(altMatch.Groups(1).Value))
                            Exit For
                        End If
                    Next
                End If
            End If

            ' Method 1: Try complex channel structure first
            Dim channelPattern As String = """ownerText"":\{""runs"":\[\{""text"":""((?:[^""\\]|\\.)*?)"""
            Dim channelMatch As Match = Regex.Match(context, channelPattern)

            If channelMatch.Success Then
                videoData.Channel = EnhancedHtmlDecode(UnescapeJsonString(channelMatch.Groups(1).Value))
            Else
                ' Method 2: Try alternative channel patterns
                Dim altChannelPatterns As String() = {
                """shortBylineText"":\{""runs"":\[\{""text"":""((?:[^""\\]|\\.)*?)""",
                """channelName"":""((?:[^""\\]|\\.)*?)""",
                """author"":""((?:[^""\\]|\\.)*?)""",
                """uploader"":""((?:[^""\\]|\\.)*?)"""
            }

                For Each pattern In altChannelPatterns
                    Dim altMatch As Match = Regex.Match(context, pattern)
                    If altMatch.Success Then
                        videoData.Channel = EnhancedHtmlDecode(UnescapeJsonString(altMatch.Groups(1).Value))
                        Exit For
                    End If
                Next
            End If

            ' Set defaults if extraction failed
            If String.IsNullOrWhiteSpace(videoData.Title) Then
                videoData.Title = "Title not found"
            End If

            If String.IsNullOrWhiteSpace(videoData.Channel) Then
                videoData.Channel = "Channel not found"
            End If

        Catch ex As Exception
            videoData.Title = "Error extracting title"
            videoData.Channel = "Error extracting channel"
        End Try
    End Sub
    ''' <summary>
    ''' Enhanced HTML decoding that preserves quotes properly
    ''' </summary>
    Private Function EnhancedHtmlDecode(htmlText As String) As String
        If String.IsNullOrWhiteSpace(htmlText) Then
            Return ""
        End If

        Dim decoded As String = htmlText

        ' First, use built-in HTML decoding
        decoded = System.Net.WebUtility.HtmlDecode(decoded)

        ' Handle additional HTML entities that might encode quotes
        decoded = decoded.Replace("&quot;", """")     ' HTML entity for "
        decoded = decoded.Replace("&#34;", """")      ' Numeric entity for "
        decoded = decoded.Replace("&#x22;", """")     ' Hex entity for "
        decoded = decoded.Replace("&apos;", "'")      ' HTML entity for '
        decoded = decoded.Replace("&#39;", "'")       ' Numeric entity for '
        decoded = decoded.Replace("&#x27;", "'")      ' Hex entity for '

        ' Handle other common entities
        decoded = decoded.Replace("&amp;", "&")
        decoded = decoded.Replace("&lt;", "<")
        decoded = decoded.Replace("&gt;", ">")

        Return decoded.Trim()
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