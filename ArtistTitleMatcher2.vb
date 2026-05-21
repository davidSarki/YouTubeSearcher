Module ArtistTitleMatcher2

    ' ============================================================================
    ' DATA STRUCTURES
    ' ============================================================================

#Region "Data Structures"

    ''' <summary>
    ''' Holds parsed and normalized song information
    ''' </summary>
    Private Class NormalizedSongData
        Public OriginalText As String
        Public MainArtists As List(Of String)        ' Primary artists
        Public FeaturedArtists As List(Of String)    ' Featured/guest artists  
        Public TitleWords As List(Of String)         ' Clean title words only
        Public TitleOriginal As String               ' Original title before cleanup
        Public Language As String

        Public Sub New()
            MainArtists = New List(Of String)
            FeaturedArtists = New List(Of String)
            TitleWords = New List(Of String)
        End Sub
    End Class

#End Region

    ' ============================================================================
    ' MAIN ENTRY POINT
    ' ============================================================================

#Region "Main Entry Point"

    ''' <summary>
    ''' MAIN FUNCTION - Single entry point for all matching
    ''' Compares two songs in "Artist - Title" format
    ''' Returns 0-100% match score
    ''' </summary>
    Public Function GetMatchPercentage(searchQuery As String, candidateText As String, Optional language As String = "auto") As Double
        Try
            If String.IsNullOrWhiteSpace(searchQuery) OrElse String.IsNullOrWhiteSpace(candidateText) Then
                Return 0.0
            End If

            ' ==================================================
            ' PHASE 1: Parse and normalize both inputs
            ' ==================================================
            Dim searchData As NormalizedSongData = ParseAndNormalize(searchQuery, language)
            Dim candidateData As NormalizedSongData = ParseAndNormalize(candidateText, language)

            ' ==================================================
            ' PHASE 2: Compare normalized data
            ' ==================================================
            Dim score As Double = CompareNormalizedData(searchData, candidateData)

            Return Math.Min(100.0, score)

        Catch ex As Exception
            Return 0.0
        End Try
    End Function

    ''' <summary>
    ''' Backward compatibility - simple True/False match
    ''' </summary>
    Public Function IsMatch(searchQuery As String, candidateText As String, Optional language As String = "auto") As Boolean
        Return GetMatchPercentage(searchQuery, candidateText, language) >= 70.0
    End Function

#End Region

    ' ============================================================================
    ' PHASE 1: PARSE AND NORMALIZE
    ' ============================================================================

#Region "Phase 1: Parse and Normalize"

    ''' <summary>
    ''' Parse and normalize a song string into structured, clean data
    ''' This is where ALL cleanup happens - one place for everything
    ''' </summary>
    Private Function ParseAndNormalize(text As String, language As String) As NormalizedSongData
        Dim result As New NormalizedSongData With {.OriginalText = text}

        ' STEP 1: Split "Artist - Title"
        Dim artistField As String = ""
        Dim titleField As String = ""

        If Not TrySplitArtistTitle(text, artistField, titleField) Then
            artistField = ""
            titleField = text
        End If

        result.TitleOriginal = titleField

        ' STEP 2: Extract artists from ARTIST field
        result.MainArtists = ExtractArtistsFromField(artistField)

        ' STEP 3: Extract featured artists from TITLE field
        Dim cleanedTitle As String = ""
        result.FeaturedArtists = ExtractFeaturedArtistsFromTitle(titleField, cleanedTitle)

        ' STEP 4: Clean up title completely (pass artist names to remove duplicates)
        Dim finalTitle As String = CleanupTitleCompletely(cleanedTitle, result.MainArtists)

        ' STEP 5: Store language hint
        result.Language = If(language = "auto", "auto", language)

        ' STEP 6: Transliterate title and extract words
        Dim transliteratedTitle As String = TransliterateAndNormalize(finalTitle, True)
        result.TitleWords = ExtractMeaningfulWords(transliteratedTitle)

        ' STEP 7: Transliterate all artists
        For i As Integer = 0 To result.MainArtists.Count - 1
            result.MainArtists(i) = TransliterateAndNormalize(result.MainArtists(i), True)
        Next

        For i As Integer = 0 To result.FeaturedArtists.Count - 1
            result.FeaturedArtists(i) = TransliterateAndNormalize(result.FeaturedArtists(i), True)
        Next

        Return result
    End Function

    ''' <summary>
    ''' Extract artists from artist field (handles &, feat., ft., etc.)
    ''' </summary>
    Private Function ExtractArtistsFromField(artistField As String) As List(Of String)
        Dim artists As New List(Of String)
        If String.IsNullOrWhiteSpace(artistField) Then Return artists

        ' Split by common separators
        Dim separators As String() = {
            " Ð¸ ", " Ð˜ ", " i ", " I ",
            " & ", " and ", " AND ",
            " feat. ", " feat ", " ft. ", " ft ",
            " featuring ", " FEATURING ",
            " with ", " WITH ",
            " vs. ", " vs ", " versus ", " VS "
        }

        Dim parts As New List(Of String)
        parts.Add(artistField)

        For Each separator In separators
            Dim newParts As New List(Of String)
            For Each part In parts
                Dim subParts = part.Split(New String() {separator}, StringSplitOptions.RemoveEmptyEntries)
                For Each subPart In subParts
                    Dim cleaned = subPart.Trim()
                    If Not String.IsNullOrWhiteSpace(cleaned) Then
                        newParts.Add(cleaned)
                    End If
                Next
            Next
            parts = newParts
        Next

        Return parts
    End Function

    ''' <summary>
    ''' Extract featured artists from title and return cleaned title
    ''' Handles: "Title + Artist", "Title feat. Artist", etc.
    ''' </summary>
    Private Function ExtractFeaturedArtistsFromTitle(title As String, ByRef cleanedTitle As String) As List(Of String)
        Dim featured As New List(Of String)
        cleanedTitle = title

        If String.IsNullOrWhiteSpace(title) Then Return featured

        ' Featured artist patterns (order matters - check most specific first)
        Dim patterns As String() = {
            " feat. ", " feat ", " ft. ", " ft ",
            " featuring ", " with ",
            " + ",
            "+",  ' No space variant (Капелькою+Jingle)
            " & ",
            " при участии ", " и "
        }

        ' Find the FIRST occurrence of any pattern
        Dim lowestIndex As Integer = -1
        Dim foundPattern As String = Nothing

        For Each pattern In patterns
            Dim index As Integer = title.IndexOf(pattern, StringComparison.OrdinalIgnoreCase)
            If index > 0 Then
                If lowestIndex = -1 OrElse index < lowestIndex Then
                    lowestIndex = index
                    foundPattern = pattern
                End If
            End If
        Next

        ' Extract featured artist if found
        If lowestIndex > 0 AndAlso foundPattern IsNot Nothing Then
            cleanedTitle = title.Substring(0, lowestIndex).Trim()
            Dim featuredPart As String = title.Substring(lowestIndex + foundPattern.Length).Trim()

            If Not String.IsNullOrWhiteSpace(featuredPart) Then
                Dim subArtists = featuredPart.Split(New String() {" & ", ", ", " Ð¸ "}, StringSplitOptions.RemoveEmptyEntries)
                For Each artist In subArtists
                    Dim cleaned = artist.Trim()
                    If Not String.IsNullOrWhiteSpace(cleaned) Then
                        featured.Add(cleaned)
                    End If
                Next
            End If
        End If

        Return featured
    End Function

    ''' <summary>
    ''' Clean up title completely - remove everything that's not the core title
    ''' </summary>
    Private Function CleanupTitleCompletely(title As String, artistNames As List(Of String)) As String
        If String.IsNullOrWhiteSpace(title) Then Return ""

        Dim cleaned As String = title

        ' Step 1: Remove artist names from title (duplicates like "Джиган-Раз И Навсегда")
        cleaned = RemoveArtistNamesFromTitle(cleaned, artistNames)

        ' Step 2: Remove parentheses and their content
        cleaned = RemoveParenthesesContent(cleaned)

        ' Step 3: Remove track numbers at the beginning
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "^(Track\s+)?\d{1,3}[\s\.\-]+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase)

        ' Step 4: Remove year/version suffixes (2023, 2024, 2.0, etc.)
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "\s+(19|20)\d{2}\s*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "\s+\d+\.\d+\s*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase)

        ' Step 5: Remove punctuation for comparison
        cleaned = RemovePunctuation(cleaned)

        Return cleaned.Trim()
    End Function

    ''' <summary>
    ''' Remove artist names from the beginning of title
    ''' Handles cases like "Джиган-Раз И Навсегда" → "Раз И Навсегда"
    ''' </summary>
    Private Function RemoveArtistNamesFromTitle(title As String, artistNames As List(Of String)) As String
        If String.IsNullOrWhiteSpace(title) OrElse artistNames.Count = 0 Then
            Return title.Trim()
        End If

        Dim cleaned As String = title.Trim()
        Dim changed As Boolean = True

        While changed
            changed = False

            For Each artistName In artistNames
                If String.IsNullOrWhiteSpace(artistName) Then Continue For

                ' Check if title starts with artist name (case-insensitive)
                If cleaned.StartsWith(artistName, StringComparison.OrdinalIgnoreCase) Then
                    ' Remove the artist name
                    cleaned = cleaned.Substring(artistName.Length).Trim()

                    ' Remove leading separators like "-", " - ", etc.
                    While cleaned.Length > 0 AndAlso "-–—|/!. ".Contains(cleaned(0))
                        cleaned = cleaned.Substring(1).Trim()
                    End While

                    changed = True
                    Exit For
                End If
            Next
        End While

        ' If we removed everything, restore original title
        If String.IsNullOrWhiteSpace(cleaned) Then
            cleaned = title
        End If

        Return cleaned
    End Function

    ''' <summary>
    ''' Extract meaningful words from text for comparison
    ''' </summary>
    Private Function ExtractMeaningfulWords(text As String) As List(Of String)
        Dim words As New List(Of String)
        If String.IsNullOrWhiteSpace(text) Then Return words

        ' Split on whitespace and separators
        Dim separators As Char() = {" "c, ","c, ";"c, "!"c, "?"c, "."c}
        Dim parts = text.Split(separators, StringSplitOptions.RemoveEmptyEntries)

        For Each part In parts
            Dim cleaned = part.Trim()
            If cleaned.Length >= 2 Then ' Ignore single characters
                words.Add(cleaned)
            End If
        Next

        Return words
    End Function

#End Region


    ' ============================================================================
    ' PHASE 2: COMPARE
    ' ============================================================================

#Region "Phase 2: Compare"

    ''' <summary>
    ''' Compare two normalized song data structures
    ''' </summary>
    Private Function CompareNormalizedData(search As NormalizedSongData, candidate As NormalizedSongData) As Double
        ' LEVEL 1: Exact match on original text
        If search.OriginalText.Trim().Equals(candidate.OriginalText.Trim(), StringComparison.OrdinalIgnoreCase) Then
            Return 100.0
        End If

        ' LEVEL 2: Compare ALL artists (main + featured combined)
        Dim allSearchArtists As New List(Of String)(search.MainArtists)
        allSearchArtists.AddRange(search.FeaturedArtists)

        Dim allCandidateArtists As New List(Of String)(candidate.MainArtists)
        allCandidateArtists.AddRange(candidate.FeaturedArtists)

        Dim artistScore As Double = CompareArtistLists(allSearchArtists, allCandidateArtists)

        ' LEVEL 3: Compare title words
        Dim titleScore As Double = CompareWordLists(search.TitleWords, candidate.TitleWords)

        ' LEVEL 4: Calculate weighted score
        Dim weightedScore As Double = (titleScore * 0.7) + (artistScore * 0.3)

        ' LEVEL 5: Apply bonuses
        If titleScore = 100.0 AndAlso artistScore = 100.0 Then
            Return 100.0
        End If

        If titleScore >= 90.0 AndAlso artistScore >= 70.0 Then
            Return Math.Min(100.0, weightedScore + 5.0)
        End If

        Return weightedScore
    End Function

    ''' <summary>
    ''' Compare two lists of artists intelligently
    ''' Handles: one side having more artists (featured), order differences
    ''' </summary>
    Private Function CompareArtistLists(searchArtists As List(Of String), candidateArtists As List(Of String)) As Double
        If searchArtists.Count = 0 AndAlso candidateArtists.Count = 0 Then Return 100.0
        If searchArtists.Count = 0 OrElse candidateArtists.Count = 0 Then Return 50.0

        ' Find smaller and larger list
        Dim smallerList = If(searchArtists.Count <= candidateArtists.Count, searchArtists, candidateArtists)
        Dim largerList = If(searchArtists.Count > candidateArtists.Count, searchArtists, candidateArtists)

        Dim matchCount As Integer = 0

        ' Check how many from smaller list match ANY in larger list
        For Each smallArtist In smallerList
            For Each largeArtist In largerList
                Dim similarity As Double = FuzzyMatchStrings(smallArtist, largeArtist)
                If similarity >= 70.0 Then
                    matchCount += 1
                    Exit For
                End If
            Next
        Next

        ' Calculate match percentage
        Dim matchPercentage As Double = (matchCount / smallerList.Count) * 100.0

        ' Apply penalty for size difference
        If matchCount = smallerList.Count Then
            ' All matched - apply small penalty for extra artists
            Dim sizeDiff As Integer = Math.Abs(searchArtists.Count - candidateArtists.Count)
            Return Math.Max(70.0, matchPercentage - (sizeDiff * 5.0))
        Else
            ' Partial match
            Return matchPercentage * 0.8
        End If
    End Function

    ''' <summary>
    ''' Compare two lists of words using bidirectional containment
    ''' </summary>
    Private Function CompareWordLists(words1 As List(Of String), words2 As List(Of String)) As Double
        If words1.Count = 0 OrElse words2.Count = 0 Then Return 0.0

        ' Exact match check
        If words1.Count = words2.Count Then
            Dim allMatch As Boolean = True
            For i As Integer = 0 To words1.Count - 1
                If Not words1(i).Equals(words2(i), StringComparison.OrdinalIgnoreCase) Then
                    allMatch = False
                    Exit For
                End If
            Next
            If allMatch Then Return 100.0
        End If

        ' Count matches from list1 to list2
        Dim matches1to2 As Integer = 0
        For Each word1 In words1
            For Each word2 In words2
                If FuzzyMatchStrings(word1, word2) >= 80.0 Then
                    matches1to2 += 1
                    Exit For
                End If
            Next
        Next

        ' Count matches from list2 to list1
        Dim matches2to1 As Integer = 0
        For Each word2 In words2
            For Each word1 In words1
                If FuzzyMatchStrings(word1, word2) >= 80.0 Then
                    matches2to1 += 1
                    Exit For
                End If
            Next
        Next

        ' Bidirectional match percentage
        Dim forward As Double = (matches1to2 / words1.Count) * 100.0
        Dim reverse As Double = (matches2to1 / words2.Count) * 100.0

        ' SPECIAL CASE: Subset matching (one list completely contained in other)
        ' Example: "Одиночество" vs "Одиночество-скука"
        ' Forward = 100% (all search words found), Reverse = 50% (only half of candidate words found)
        ' Lowered threshold to 33% to handle cases like "Девушки" vs "Девушки (Девушки бывают разные)"
        If forward = 100.0 AndAlso reverse >= 33.0 Then
            ' Search is a complete subset of candidate - likely same song with extended title
            ' Use arithmetic mean instead of harmonic mean for better scoring
            Return (forward + reverse) / 2.0
        ElseIf reverse = 100.0 AndAlso forward >= 33.0 Then
            ' Candidate is a complete subset of search - also likely same song
            Return (forward + reverse) / 2.0
        End If

        ' Normal case: Use harmonic mean to prevent false positives on different songs
        If forward > 0 AndAlso reverse > 0 Then
            Return (2 * forward * reverse) / (forward + reverse)
        Else
            Return 0.0
        End If
    End Function

    ''' <summary>
    ''' Fuzzy match two strings using Levenshtein distance
    ''' </summary>
    Private Function FuzzyMatchStrings(str1 As String, str2 As String) As Double
        If String.IsNullOrWhiteSpace(str1) OrElse String.IsNullOrWhiteSpace(str2) Then Return 0.0

        ' Exact match
        If str1.Equals(str2, StringComparison.OrdinalIgnoreCase) Then Return 100.0

        ' Calculate Levenshtein distance
        Dim distance As Integer = LevenshteinDistance(str1.ToLower(), str2.ToLower())
        Dim maxLen As Integer = Math.Max(str1.Length, str2.Length)

        If maxLen = 0 Then Return 100.0

        Dim similarity As Double = (1.0 - (distance / maxLen)) * 100.0
        Return Math.Max(0.0, similarity)
    End Function

#End Region


    ' ============================================================================
    ' HELPER FUNCTIONS (from original ArtistTitleMatcher.vb)
    ' ============================================================================

#Region "Helper Functions - String Operations"

    ''' <summary>
    ''' Try to split "Artist - Title" format
    ''' </summary>
    Public Function TrySplitArtistTitle(query As String, ByRef artist As String, ByRef title As String) As Boolean
        Dim separators As String() = {" - ", " – ", " — ", " | ", " / ", " by ", " BY "}

        For Each separator As String In separators
            Dim index As Integer = query.IndexOf(separator, StringComparison.OrdinalIgnoreCase)
            If index > 0 AndAlso index < query.Length - separator.Length Then
                artist = query.Substring(0, index).Trim()
                title = query.Substring(index + separator.Length).Trim()

                If artist.Length > 1 AndAlso title.Length > 1 Then
                    Return True
                End If
            End If
        Next

        Return False
    End Function

    ''' <summary>
    ''' Remove parentheses content from text
    ''' </summary>
    Private Function RemoveParenthesesContent(text As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return text

        Dim cleaned As String = text
        Dim parenthesesPairs As New Dictionary(Of String, String) From {
            {"(", ")"},
            {"[", "]"},
            {"{", "}"},
            {"ã€", "ã€‘"},
            {"ã€Š", "ã€‹"},
            {"âŸ¨", "âŸ©"},
            {"ï½›", "ï½"},
            {"ï¼»", "ï¼½"},
            {"ï¼ˆ", "ï¼‰"}
        }

        For Each pair In parenthesesPairs
            cleaned = RemoveParenthesesPair(cleaned, pair.Key, pair.Value)
        Next

        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "\s+", " ").Trim()
        cleaned = cleaned.TrimEnd(","c, "."c, ";"c, ":"c, "-"c, "–"c, "—"c).Trim()

        Return cleaned
    End Function

    Private Function RemoveParenthesesPair(text As String, openChar As String, closeChar As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return text

        Dim result As New System.Text.StringBuilder()
        Dim depth As Integer = 0
        Dim i As Integer = 0

        While i < text.Length
            Dim currentChar As String = text(i).ToString()

            If currentChar = openChar Then
                depth += 1
            ElseIf currentChar = closeChar Then
                If depth > 0 Then
                    depth -= 1
                Else
                    result.Append(currentChar)
                End If
            ElseIf depth = 0 Then
                result.Append(currentChar)
            End If

            i += 1
        End While

        Return result.ToString()
    End Function

    ''' <summary>
    ''' Remove punctuation/separators for comparison
    ''' Also normalizes hyphens to spaces: "A-B" → "A B" (splits into separate words)
    ''' </summary>
    Private Function RemovePunctuation(text As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return text

        Dim result As String = text

        ' First, normalize spaces around hyphens - replace with space to split words
        result = System.Text.RegularExpressions.Regex.Replace(result, "\s*[-–—]\s*", " ", System.Text.RegularExpressions.RegexOptions.None)

        ' Remove other punctuation
        result = result.Replace("_", "")
        result = result.Replace("•", "")
        result = result.Replace("|", "")
        result = result.Replace("/", "")
        result = result.Replace("\", "")
        result = result.Replace(".", "")
        result = result.Replace(",", "")
        result = result.Replace("!", "")
        result = result.Replace("?", "")
        result = result.Replace(":", "")
        result = result.Replace(";", "")
        result = result.Replace("'", "")
        result = result.Replace("""", "")
        result = result.Replace("`", "")

        ' Clean up multiple spaces and trim
        result = System.Text.RegularExpressions.Regex.Replace(result, "\s+", " ").Trim()

        Return result
    End Function

    ''' <summary>
    ''' Calculate Levenshtein distance between two strings
    ''' </summary>
    Private Function LevenshteinDistance(str1 As String, str2 As String) As Integer
        If String.IsNullOrEmpty(str1) Then Return If(String.IsNullOrEmpty(str2), 0, str2.Length)
        If String.IsNullOrEmpty(str2) Then Return str1.Length

        Dim matrix(str2.Length, str1.Length) As Integer

        For i As Integer = 0 To str2.Length
            matrix(i, 0) = i
        Next

        For j As Integer = 0 To str1.Length
            matrix(0, j) = j
        Next

        For i As Integer = 1 To str2.Length
            For j As Integer = 1 To str1.Length
                If str2(i - 1) = str1(j - 1) Then
                    matrix(i, j) = matrix(i - 1, j - 1)
                Else
                    matrix(i, j) = Math.Min(Math.Min(
                        matrix(i - 1, j - 1) + 1,
                        matrix(i, j - 1) + 1),
                        matrix(i - 1, j) + 1)
                End If
            Next
        Next

        Return matrix(str2.Length, str1.Length)
    End Function

#End Region


#Region "Transliteration - Simple Unicode to ASCII Mapping"
    ''' <summary>
    ''' Transliterate and normalize for matching/comparison
    ''' Applies lowercase and phonetic normalization
    ''' </summary>
    Public Function TransliterateAndNormalize(text As String, Optional NormalizeMode As Boolean = False) As String
        If String.IsNullOrEmpty(text) Then Return ""

        Dim result As String = TransliterateUnicodeToAscii(text)
        If NormalizeMode Then
            result = result.ToLowerInvariant()    ' Lowercase for comparison
            result = NormalizeCharacters(result)  ' Phonetic normalization
            result = RemoveDoubleLetters(result)
        End If
        Return result
    End Function

    ''' <summary>
    ''' Simple Unicode to ASCII transliteration table
    ''' </summary>
    Private Function TransliterateUnicodeToAscii(text As String) As String
        Dim sb As New System.Text.StringBuilder()

        For Each c As Char In text
            Dim mapped As String = GetAsciiMapping(c)
            sb.Append(mapped)
        Next

        Return sb.ToString()
    End Function

    ''' <summary>
    ''' Get ASCII equivalent for any Unicode character
    ''' </summary>
    Private Function GetAsciiMapping(c As Char) As String
        ' Already ASCII
        If c < ChrW(&H80) Then Return c.ToString()

        ' Cyrillic (Russian, Ukrainian, etc.) - U+0400 to U+04FF
        If c >= ChrW(&H400) AndAlso c <= ChrW(&H4FF) Then
            Select Case c
                Case ChrW(&H410) : Return "A"  ' А
                Case ChrW(&H411) : Return "B"  ' Б
                Case ChrW(&H412) : Return "V"  ' В
                Case ChrW(&H413) : Return "G"  ' Г
                Case ChrW(&H414) : Return "D"  ' Д
                Case ChrW(&H415), ChrW(&H401) : Return "E"  ' Е, Ё
                Case ChrW(&H416) : Return "Zh" ' Ж
                Case ChrW(&H417) : Return "Z"  ' З
                Case ChrW(&H418) : Return "I"  ' И
                Case ChrW(&H419) : Return "Y"  ' Й
                Case ChrW(&H41A) : Return "K"  ' К
                Case ChrW(&H41B) : Return "L"  ' Л
                Case ChrW(&H41C) : Return "M"  ' М
                Case ChrW(&H41D) : Return "N"  ' Н
                Case ChrW(&H41E) : Return "O"  ' О
                Case ChrW(&H41F) : Return "P"  ' П
                Case ChrW(&H420) : Return "R"  ' Р
                Case ChrW(&H421) : Return "S"  ' С
                Case ChrW(&H422) : Return "T"  ' Т
                Case ChrW(&H423) : Return "U"  ' У
                Case ChrW(&H424) : Return "F"  ' Ф
                Case ChrW(&H425) : Return "Kh" ' Х
                Case ChrW(&H426) : Return "Ts" ' Ц
                Case ChrW(&H427) : Return "Ch" ' Ч
                Case ChrW(&H428) : Return "Sh" ' Ш
                Case ChrW(&H429) : Return "Sch" ' Щ
                Case ChrW(&H42A) : Return ""   ' Ъ
                Case ChrW(&H42B) : Return "Y"  ' Ы
                Case ChrW(&H42C) : Return ""   ' Ь
                Case ChrW(&H42D) : Return "E"  ' Э
                Case ChrW(&H42E) : Return "Yu" ' Ю
                Case ChrW(&H42F) : Return "Ya" ' Я
                ' Lowercase
                Case ChrW(&H430) : Return "a"  ' а
                Case ChrW(&H431) : Return "b"  ' б
                Case ChrW(&H432) : Return "v"  ' в
                Case ChrW(&H433) : Return "g"  ' г
                Case ChrW(&H434) : Return "d"  ' д
                Case ChrW(&H435), ChrW(&H451) : Return "e"  ' е, ё
                Case ChrW(&H436) : Return "zh" ' ж
                Case ChrW(&H437) : Return "z"  ' з
                Case ChrW(&H438) : Return "i"  ' и
                Case ChrW(&H439) : Return "y"  ' й
                Case ChrW(&H43A) : Return "k"  ' к
                Case ChrW(&H43B) : Return "l"  ' л
                Case ChrW(&H43C) : Return "m"  ' м
                Case ChrW(&H43D) : Return "n"  ' н
                Case ChrW(&H43E) : Return "o"  ' о
                Case ChrW(&H43F) : Return "p"  ' п
                Case ChrW(&H440) : Return "r"  ' р
                Case ChrW(&H441) : Return "s"  ' с
                Case ChrW(&H442) : Return "t"  ' т
                Case ChrW(&H443) : Return "u"  ' у
                Case ChrW(&H444) : Return "f"  ' ф
                Case ChrW(&H445) : Return "kh" ' х
                Case ChrW(&H446) : Return "ts" ' ц
                Case ChrW(&H447) : Return "ch" ' ч
                Case ChrW(&H448) : Return "sh" ' ш
                Case ChrW(&H449) : Return "sch" ' щ
                Case ChrW(&H44A) : Return ""   ' ъ
                Case ChrW(&H44B) : Return "y"  ' ы
                Case ChrW(&H44C) : Return ""   ' ь
                Case ChrW(&H44D) : Return "e"  ' э
                Case ChrW(&H44E) : Return "yu" ' ю
                Case ChrW(&H44F) : Return "ya" ' я
                Case Else : Return c.ToString()
            End Select
        End If

        ' Armenian - U+0531 to U+0587
        If c >= ChrW(&H531) AndAlso c <= ChrW(&H587) Then
            Select Case c
                ' Uppercase
                Case ChrW(&H531) : Return "A"  ' Ա
                Case ChrW(&H532) : Return "B"  ' Բ
                Case ChrW(&H533) : Return "G"  ' Գ
                Case ChrW(&H534) : Return "D"  ' Դ
                Case ChrW(&H535) : Return "E"  ' Ե
                Case ChrW(&H536) : Return "Z"  ' Զ
                Case ChrW(&H537) : Return "E"  ' Է
                Case ChrW(&H538) : Return "E"  ' Ը
                Case ChrW(&H539) : Return "T"  ' Թ
                Case ChrW(&H53A) : Return "Zh" ' Ժ
                Case ChrW(&H53B) : Return "I"  ' Ի
                Case ChrW(&H53C) : Return "L"  ' Լ
                Case ChrW(&H53D) : Return "Kh" ' Խ
                Case ChrW(&H53E) : Return "Ts" ' Ծ
                Case ChrW(&H53F) : Return "K"  ' Կ
                Case ChrW(&H540) : Return "H"  ' Հ
                Case ChrW(&H541) : Return "Dz" ' Ձ
                Case ChrW(&H542) : Return "Gh" ' Ղ
                Case ChrW(&H543) : Return "Ch" ' Ճ
                Case ChrW(&H544) : Return "M"  ' Մ
                Case ChrW(&H545) : Return "Y"  ' Յ
                Case ChrW(&H546) : Return "N"  ' Ն
                Case ChrW(&H547) : Return "Sh" ' Շ
                Case ChrW(&H548) : Return "O"  ' Ո
                Case ChrW(&H549) : Return "Ch" ' Չ
                Case ChrW(&H54A) : Return "P"  ' Պ
                Case ChrW(&H54B) : Return "J"  ' Ջ
                Case ChrW(&H54C) : Return "R"  ' Ռ
                Case ChrW(&H54D) : Return "S"  ' Ս
                Case ChrW(&H54E) : Return "V"  ' Վ
                Case ChrW(&H54F) : Return "T"  ' Տ
                Case ChrW(&H550) : Return "R"  ' Ր
                Case ChrW(&H551) : Return "C"  ' Ց
                Case ChrW(&H553) : Return "P"  ' Փ
                Case ChrW(&H554) : Return "K"  ' Ք
                Case ChrW(&H555) : Return "O"  ' Օ
                Case ChrW(&H556) : Return "F"  ' Ֆ
                ' Lowercase
                Case ChrW(&H561) : Return "a"  ' ա
                Case ChrW(&H562) : Return "b"  ' բ
                Case ChrW(&H563) : Return "g"  ' գ
                Case ChrW(&H564) : Return "d"  ' դ
                Case ChrW(&H565) : Return "e"  ' ե
                Case ChrW(&H566) : Return "z"  ' զ
                Case ChrW(&H567) : Return "e"  ' է
                Case ChrW(&H568) : Return "e"  ' ը
                Case ChrW(&H569) : Return "t"  ' թ
                Case ChrW(&H56A) : Return "zh" ' ժ
                Case ChrW(&H56B) : Return "i"  ' ի
                Case ChrW(&H56C) : Return "l"  ' լ
                Case ChrW(&H56D) : Return "kh" ' խ
                Case ChrW(&H56E) : Return "ts" ' ծ
                Case ChrW(&H56F) : Return "k"  ' կ
                Case ChrW(&H570) : Return "h"  ' հ
                Case ChrW(&H571) : Return "dz" ' ձ
                Case ChrW(&H572) : Return "gh" ' ղ
                Case ChrW(&H573) : Return "ch" ' ճ
                Case ChrW(&H574) : Return "m"  ' մ
                Case ChrW(&H575) : Return "y"  ' յ
                Case ChrW(&H576) : Return "n"  ' ն
                Case ChrW(&H577) : Return "sh" ' շ
                Case ChrW(&H578) : Return "o"  ' ո
                Case ChrW(&H579) : Return "ch" ' չ
                Case ChrW(&H57A) : Return "p"  ' պ
                Case ChrW(&H57B) : Return "j"  ' ջ
                Case ChrW(&H57C) : Return "r"  ' ռ
                Case ChrW(&H57D) : Return "s"  ' ս
                Case ChrW(&H57E) : Return "v"  ' վ
                Case ChrW(&H57F) : Return "t"  ' տ
                Case ChrW(&H580) : Return "r"  ' ր
                Case ChrW(&H581) : Return "c"  ' ց
                Case ChrW(&H583) : Return "p"  ' փ
                Case ChrW(&H584) : Return "k"  ' ք
                Case ChrW(&H585) : Return "o"  ' օ
                Case ChrW(&H586) : Return "f"  ' ֆ
                Case ChrW(&H587) : Return "ev" ' և
                Case Else : Return c.ToString()
            End Select
        End If

        ' Latin Extended-A (diacritics) - U+0100 to U+017F
        If c >= ChrW(&H100) AndAlso c <= ChrW(&H17F) Then
            Select Case c
                Case ChrW(&H100), ChrW(&H101) : Return If(Char.IsUpper(c), "A", "a") ' Ā ā
                Case ChrW(&H102), ChrW(&H103) : Return If(Char.IsUpper(c), "A", "a") ' Ă ă
                Case ChrW(&H104), ChrW(&H105) : Return If(Char.IsUpper(c), "A", "a") ' Ą ą
                Case ChrW(&H106), ChrW(&H107) : Return If(Char.IsUpper(c), "C", "c") ' Ć ć
                Case ChrW(&H108), ChrW(&H109) : Return If(Char.IsUpper(c), "C", "c") ' Ĉ ĉ
                Case ChrW(&H10A), ChrW(&H10B) : Return If(Char.IsUpper(c), "C", "c") ' Ċ ċ
                Case ChrW(&H10C), ChrW(&H10D) : Return If(Char.IsUpper(c), "C", "c") ' Č č
                Case ChrW(&H10E), ChrW(&H10F) : Return If(Char.IsUpper(c), "D", "d") ' Ď ď
                Case ChrW(&H112), ChrW(&H113) : Return If(Char.IsUpper(c), "E", "e") ' Ē ē
                Case ChrW(&H114), ChrW(&H115) : Return If(Char.IsUpper(c), "E", "e") ' Ĕ ĕ
                Case ChrW(&H116), ChrW(&H117) : Return If(Char.IsUpper(c), "E", "e") ' Ė ė
                Case ChrW(&H118), ChrW(&H119) : Return If(Char.IsUpper(c), "E", "e") ' Ę ę
                Case ChrW(&H11A), ChrW(&H11B) : Return If(Char.IsUpper(c), "E", "e") ' Ě ě
                Case ChrW(&H11C), ChrW(&H11D) : Return If(Char.IsUpper(c), "G", "g") ' Ĝ ĝ
                Case ChrW(&H11E), ChrW(&H11F) : Return If(Char.IsUpper(c), "G", "g") ' Ğ ğ
                Case ChrW(&H120), ChrW(&H121) : Return If(Char.IsUpper(c), "G", "g") ' Ġ ġ
                Case ChrW(&H122), ChrW(&H123) : Return If(Char.IsUpper(c), "G", "g") ' Ģ ģ
                Case ChrW(&H124), ChrW(&H125) : Return If(Char.IsUpper(c), "H", "h") ' Ĥ ĥ
                Case ChrW(&H128), ChrW(&H129) : Return If(Char.IsUpper(c), "I", "i") ' Ĩ ĩ
                Case ChrW(&H12A), ChrW(&H12B) : Return If(Char.IsUpper(c), "I", "i") ' Ī ī
                Case ChrW(&H12C), ChrW(&H12D) : Return If(Char.IsUpper(c), "I", "i") ' Ĭ ĭ
                Case ChrW(&H12E), ChrW(&H12F) : Return If(Char.IsUpper(c), "I", "i") ' Į į
                Case ChrW(&H130) : Return "I"  ' İ
                Case ChrW(&H131) : Return "i"  ' ı
                Case ChrW(&H134), ChrW(&H135) : Return If(Char.IsUpper(c), "J", "j") ' Ĵ ĵ
                Case ChrW(&H136), ChrW(&H137) : Return If(Char.IsUpper(c), "K", "k") ' Ķ ķ
                Case ChrW(&H139), ChrW(&H13A) : Return If(Char.IsUpper(c), "L", "l") ' Ĺ ĺ
                Case ChrW(&H13B), ChrW(&H13C) : Return If(Char.IsUpper(c), "L", "l") ' Ļ ļ
                Case ChrW(&H13D), ChrW(&H13E) : Return If(Char.IsUpper(c), "L", "l") ' Ľ ľ
                Case ChrW(&H141), ChrW(&H142) : Return If(Char.IsUpper(c), "L", "l") ' Ł ł
                Case ChrW(&H143), ChrW(&H144) : Return If(Char.IsUpper(c), "N", "n") ' Ń ń
                Case ChrW(&H145), ChrW(&H146) : Return If(Char.IsUpper(c), "N", "n") ' Ņ ņ
                Case ChrW(&H147), ChrW(&H148) : Return If(Char.IsUpper(c), "N", "n") ' Ň ň
                Case ChrW(&H14C), ChrW(&H14D) : Return If(Char.IsUpper(c), "O", "o") ' Ō ō
                Case ChrW(&H14E), ChrW(&H14F) : Return If(Char.IsUpper(c), "O", "o") ' Ŏ ŏ
                Case ChrW(&H150), ChrW(&H151) : Return If(Char.IsUpper(c), "O", "o") ' Ő ő
                Case ChrW(&H154), ChrW(&H155) : Return If(Char.IsUpper(c), "R", "r") ' Ŕ ŕ
                Case ChrW(&H156), ChrW(&H157) : Return If(Char.IsUpper(c), "R", "r") ' Ŗ ŗ
                Case ChrW(&H158), ChrW(&H159) : Return If(Char.IsUpper(c), "R", "r") ' Ř ř
                Case ChrW(&H15A), ChrW(&H15B) : Return If(Char.IsUpper(c), "S", "s") ' Ś ś
                Case ChrW(&H15C), ChrW(&H15D) : Return If(Char.IsUpper(c), "S", "s") ' Ŝ ŝ
                Case ChrW(&H15E), ChrW(&H15F) : Return If(Char.IsUpper(c), "S", "s") ' Ş ş
                Case ChrW(&H160), ChrW(&H161) : Return If(Char.IsUpper(c), "S", "s") ' Š š
                Case ChrW(&H162), ChrW(&H163) : Return If(Char.IsUpper(c), "T", "t") ' Ţ ţ
                Case ChrW(&H164), ChrW(&H165) : Return If(Char.IsUpper(c), "T", "t") ' Ť ť
                Case ChrW(&H168), ChrW(&H169) : Return If(Char.IsUpper(c), "U", "u") ' Ũ ũ
                Case ChrW(&H16A), ChrW(&H16B) : Return If(Char.IsUpper(c), "U", "u") ' Ū ū
                Case ChrW(&H16C), ChrW(&H16D) : Return If(Char.IsUpper(c), "U", "u") ' Ŭ ŭ
                Case ChrW(&H16E), ChrW(&H16F) : Return If(Char.IsUpper(c), "U", "u") ' Ů ů
                Case ChrW(&H170), ChrW(&H171) : Return If(Char.IsUpper(c), "U", "u") ' Ű ű
                Case ChrW(&H172), ChrW(&H173) : Return If(Char.IsUpper(c), "U", "u") ' Ų ų
                Case ChrW(&H174), ChrW(&H175) : Return If(Char.IsUpper(c), "W", "w") ' Ŵ ŵ
                Case ChrW(&H176), ChrW(&H177) : Return If(Char.IsUpper(c), "Y", "y") ' Ŷ ŷ
                Case ChrW(&H178) : Return "Y"  ' Ÿ
                Case ChrW(&H179), ChrW(&H17A) : Return If(Char.IsUpper(c), "Z", "z") ' Ź ź
                Case ChrW(&H17B), ChrW(&H17C) : Return If(Char.IsUpper(c), "Z", "z") ' Ż ż
                Case ChrW(&H17D), ChrW(&H17E) : Return If(Char.IsUpper(c), "Z", "z") ' Ž ž
                Case Else : Return c.ToString()
            End Select
        End If

        ' Common Latin-1 diacritics - U+00C0 to U+00FF
        Select Case c
            Case ChrW(&HC0), ChrW(&HC1), ChrW(&HC2), ChrW(&HC3), ChrW(&HC4), ChrW(&HC5) : Return "A" ' À Á Â Ã Ä Å
            Case ChrW(&HE0), ChrW(&HE1), ChrW(&HE2), ChrW(&HE3), ChrW(&HE4), ChrW(&HE5) : Return "a" ' à á â ã ä å
            Case ChrW(&HC7) : Return "C" ' Ç
            Case ChrW(&HE7) : Return "c" ' ç
            Case ChrW(&HC8), ChrW(&HC9), ChrW(&HCA), ChrW(&HCB) : Return "E" ' È É Ê Ë
            Case ChrW(&HE8), ChrW(&HE9), ChrW(&HEA), ChrW(&HEB) : Return "e" ' è é ê ë
            Case ChrW(&HCC), ChrW(&HCD), ChrW(&HCE), ChrW(&HCF) : Return "I" ' Ì Í Î Ï
            Case ChrW(&HEC), ChrW(&HED), ChrW(&HEE), ChrW(&HEF) : Return "i" ' ì í î ï
            Case ChrW(&HD1) : Return "N" ' Ñ
            Case ChrW(&HF1) : Return "n" ' ñ
            Case ChrW(&HD2), ChrW(&HD3), ChrW(&HD4), ChrW(&HD5), ChrW(&HD6), ChrW(&HD8) : Return "O" ' Ò Ó Ô Õ Ö Ø
            Case ChrW(&HF2), ChrW(&HF3), ChrW(&HF4), ChrW(&HF5), ChrW(&HF6), ChrW(&HF8) : Return "o" ' ò ó ô õ ö ø
            Case ChrW(&HD9), ChrW(&HDA), ChrW(&HDB), ChrW(&HDC) : Return "U" ' Ù Ú Û Ü
            Case ChrW(&HF9), ChrW(&HFA), ChrW(&HFB), ChrW(&HFC) : Return "u" ' ù ú û ü
            Case ChrW(&HDD) : Return "Y" ' Ý
            Case ChrW(&HFD), ChrW(&HFF) : Return "y" ' ý ÿ
            Case ChrW(&HDF) : Return "ss" ' ß
            Case ChrW(&HE6) : Return "ae" ' æ
            Case ChrW(&HC6) : Return "AE" ' Æ
            Case ChrW(&HF0) : Return "d" ' ð
            Case ChrW(&HFE) : Return "th" ' þ
            Case ChrW(&HDE) : Return "TH" ' Þ
        End Select

        ' Default: return as-is
        Return c.ToString()
    End Function

    ''' <summary>
    ''' Normalize ASCII characters (diacritics already removed by transliteration)
    ''' Apply phonetic simplifications
    ''' </summary>
    ''' <summary>
    ''' Normalize ASCII characters (diacritics already removed by transliteration)
    ''' Apply phonetic simplifications - lowercase only
    ''' </summary>
    Private Function NormalizeCharacters(text As String) As String
        Dim result As String = text

        ' Phonetic unification (lowercase only since we already applied ToLowerInvariant)
        result = result.Replace("ph", "f")
        result = result.Replace("ck", "k")
        result = result.Replace("qu", "k")
        result = result.Replace("x", "ks")
        result = result.Replace("w", "v")

        ' Simplify digraphs
        result = result.Replace("zh", "j")
        result = result.Replace("ch", "c")
        result = result.Replace("sh", "s")
        result = result.Replace("kh", "h")
        result = result.Replace("ts", "c")
        result = result.Replace("gh", "g")
        result = result.Replace("dz", "z")

        ' Normalize vowel combinations
        result = result.Replace("ya", "ia")
        result = result.Replace("yu", "iu")
        result = result.Replace("yo", "io")

        ' End-of-word patterns
        result = System.Text.RegularExpressions.Regex.Replace(result, "ou\b", "u", System.Text.RegularExpressions.RegexOptions.IgnoreCase)

        Return result
    End Function

    ''' <summary>
    ''' Remove consecutive duplicate letters
    ''' </summary>
    Private Function RemoveDoubleLetters(text As String) As String
        If String.IsNullOrEmpty(text) Then Return ""

        Dim sb As New System.Text.StringBuilder()
        Dim lastChar As Char = CChar(vbNullChar)

        For Each c As Char In text
            If c <> lastChar OrElse Not Char.IsLetter(c) Then
                sb.Append(c)
            End If
            lastChar = c
        Next

        Return sb.ToString()
    End Function

#End Region

End Module
