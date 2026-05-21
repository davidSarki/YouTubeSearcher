' =====================================================
' CLEAN DirectoryTreeViewModule.vb
' Simple, working implementation
' =====================================================
Imports System.IO
Imports System.Windows.Forms
Imports System.Drawing
Imports Newtonsoft.Json
Imports System.Collections.Generic

Module DirectoryTreeViewModule

    Private isUpdatingNodeStates As Boolean = False
    Private selectedNodes As New List(Of TreeNode)

    ' Enumeration for add folder modes
    Public Enum AddFolderMode
        Replace = 0
        Append = 1
    End Enum

    ' Image indices
    Private Const IMG_MUSIC_FILE As Integer = 0
    Private Const IMG_UNCHECKED_FILE As Integer = 1
    Private Const IMG_UNCHECKED_FOLDER As Integer = 2
    Private Const IMG_MUSIC_FOLDER As Integer = 3
    Private Const IMG_SUB_FOLDER As Integer = 4

    ' Data class for saving/loading TreeView state
    Public Class TreeNodeState
        Public Property FileFullPath As String
        Public Property Text As String
        Public Property IsChecked As Boolean
        Public Property IsFolder As Boolean
        Public Property Children As List(Of TreeNodeState)
        Public Property IsExpanded As Boolean

        Public Sub New()
            Children = New List(Of TreeNodeState)
        End Sub
    End Class

    ' Simple DirectoryTreeNode class
    Public Class DirectoryTreeNode
        Inherits TreeNode

        Public Property FileFullPath As String
        Public Property IsFolder As Boolean
        Public Property LastRefreshTime As DateTime

        Public Sub New()
            MyBase.New()
            LastRefreshTime = DateTime.Now
        End Sub

        Public Sub New(text As String, fullPath As String, isFolder As Boolean)
            MyBase.New(text)
            Me.FileFullPath = fullPath
            Me.IsFolder = isFolder
            Me.LastRefreshTime = DateTime.Now
        End Sub
    End Class

    ' Data class to hold information about children that need to be moved

    Private Class ChildToMoveInfo
        Public Property NodeToMove As DirectoryTreeNode
        Public Property OriginalParent As TreeNode
        Public Property RelativePathFromNewParent As String

        Public Sub New(nodeToMove As DirectoryTreeNode, originalParent As TreeNode, relativePath As String)
            Me.NodeToMove = nodeToMove
            Me.OriginalParent = originalParent
            Me.RelativePathFromNewParent = relativePath
        End Sub
    End Class

    ' Initialize TreeView
    Public Sub InitializeDirectoryTreeView(treeView As TreeView, Optional enableMultiSelect As Boolean = False)
        If treeView Is Nothing Then Return

        treeView.CheckBoxes = True
        treeView.FullRowSelect = True
        treeView.ShowLines = True
        treeView.ShowPlusMinus = True
        treeView.ShowRootLines = True
        treeView.Sorted = False

        If enableMultiSelect Then
            treeView.HideSelection = False
            selectedNodes.Clear()
        End If
    End Sub

    ' Set TreeView ImageList
    Public Sub SetTreeViewImageList(treeView As TreeView, imageList As ImageList)
        If treeView IsNot Nothing AndAlso imageList IsNot Nothing Then
            treeView.ImageList = imageList
        End If
    End Sub

    ' Simple progress helpers
    Private Sub UpdateStatus(statusLabel As ToolStripStatusLabel, message As String)
        If statusLabel IsNot Nothing Then
            statusLabel.Text = message
            statusLabel.Owner.Refresh()
        End If
    End Sub

    Private Sub UpdateProgressWithCounter(progressBar As ToolStripProgressBar, statusLabel As ToolStripStatusLabel,
                                     currentValue As Integer, maxValue As Integer, itemName As String)
        If progressBar IsNot Nothing Then
            progressBar.Maximum = maxValue
            progressBar.Value = Math.Min(currentValue, maxValue)
        End If

        If statusLabel IsNot Nothing Then
            statusLabel.Text = $"Processing: {itemName} ({currentValue}/{maxValue})"
            statusLabel.Owner.Refresh()
        End If

        ' Allow UI updates
        Application.DoEvents()
    End Sub

    ' 1. Enhanced AddFolderTreeView with duplicate checking and smart hierarchy
    Public Sub AddFolderTreeView(treeView As TreeView, folderPath As String, mode As AddFolderMode,
                        Optional progressBar As ToolStripProgressBar = Nothing,
                        Optional statusLabel As ToolStripStatusLabel = Nothing)
        Try
            If treeView Is Nothing OrElse String.IsNullOrWhiteSpace(folderPath) Then Return
            If Not Directory.Exists(folderPath) Then Return

            UpdateStatus(statusLabel, "Analyzing folder: " & folderPath)

            ' Normalize the path
            Dim normalizedPath As String = Path.GetFullPath(folderPath)

            ' 1. CHECK FOR DUPLICATES - Prevent exact same folder from being added
            If FolderExistsInTree(treeView, normalizedPath) Then
                UpdateStatus(statusLabel, "Folder already exists in tree: " & Path.GetFileName(normalizedPath))
                MessageBox.Show($"The folder '{Path.GetFileName(normalizedPath)}' is already in the tree.",
                       "Duplicate Folder", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' Clear if replace mode
            If mode = AddFolderMode.Replace Then
                treeView.Nodes.Clear()
            End If

            ' 2. FIND PARENT - Check if this folder should go under an existing parent
            Dim parentInfo As ParentFolderInfo = FindBestParentForFolder(treeView, normalizedPath)

            ' 3. FIND CHILDREN - Check if any existing folders should become children of this new folder
            Dim childrenToMove As List(Of ChildToMoveInfo) = FindChildrenToReorganize(treeView, normalizedPath)

            If parentInfo.ParentNode IsNot Nothing Then
                ' Add under existing parent
                UpdateStatus(statusLabel, $"Adding under existing parent: {parentInfo.ParentNode.Text}")
                AddFolderUnderExistingParentWithReorganization(parentInfo.ParentNode, normalizedPath,
                                                          parentInfo.RelativePath, childrenToMove,
                                                          progressBar, statusLabel)
            Else
                ' Add as new root
                UpdateStatus(statusLabel, "Adding as new root folder")
                AddFolderAsRootWithReorganization(treeView, normalizedPath, childrenToMove,
                                            progressBar, statusLabel)
            End If

            UpdateStatus(statusLabel, "Folder added successfully with hierarchy reorganization")
            treeView.Refresh()

        Catch ex As Exception
            UpdateStatus(statusLabel, "Error: " & ex.Message)
        End Try
    End Sub

    ' Find children that should be reorganized under the new folder
    Private Function FindChildrenToReorganize(treeView As TreeView, newFolderPath As String) As List(Of ChildToMoveInfo)
        Dim childrenToMove As New List(Of ChildToMoveInfo)

        ' Check all nodes in the tree to see if any should become children of the new folder
        For Each rootNode As TreeNode In treeView.Nodes
            FindChildrenInSubtree(rootNode, Nothing, newFolderPath, childrenToMove)
        Next

        Return childrenToMove
    End Function

    Private Sub FindChildrenInSubtree(node As TreeNode, parent As TreeNode, newFolderPath As String,
                                 childrenToMove As List(Of ChildToMoveInfo))
        Dim dirNode As DirectoryTreeNode = TryCast(node, DirectoryTreeNode)
        If dirNode IsNot Nothing AndAlso dirNode.IsFolder Then
            ' Check if this folder should become a child of the new folder
            If dirNode.FileFullPath.StartsWith(newFolderPath, StringComparison.OrdinalIgnoreCase) AndAlso
           dirNode.FileFullPath.Length > newFolderPath.Length Then

                ' Calculate relative path from new parent
                Dim relativePath As String = dirNode.FileFullPath.Substring(newFolderPath.Length).TrimStart("\"c, "/"c)

                ' Only move direct children, not deeper descendants
                Dim pathParts As String() = relativePath.Split({"\"c, "/"c}, StringSplitOptions.RemoveEmptyEntries)
                If pathParts.Length > 0 Then
                    ' This should be moved under the new folder
                    childrenToMove.Add(New ChildToMoveInfo(dirNode, parent, relativePath))
                    Return ' Don't process children of nodes that will be moved
                End If
            End If
        End If

        ' Recursively check children if this node is not being moved
        For Each childNode As TreeNode In node.Nodes
            FindChildrenInSubtree(childNode, node, newFolderPath, childrenToMove)
        Next
    End Sub

    ' Add folder as root with reorganization
    Private Sub AddFolderAsRootWithReorganization(treeView As TreeView, folderPath As String,
                                            childrenToMove As List(Of ChildToMoveInfo),
                                            Optional progressBar As ToolStripProgressBar = Nothing,
                                            Optional statusLabel As ToolStripStatusLabel = Nothing)

        Dim folderName As String = Path.GetFileName(folderPath)
        If String.IsNullOrEmpty(folderName) Then folderName = folderPath

        ' Check if root with same name already exists
        For Each existingRoot As TreeNode In treeView.Nodes
            Dim dirRoot As DirectoryTreeNode = TryCast(existingRoot, DirectoryTreeNode)
            If dirRoot IsNot Nothing AndAlso
           String.Equals(dirRoot.Text, folderName, StringComparison.OrdinalIgnoreCase) Then
                UpdateStatus(statusLabel, "Root folder with same name already exists: " & folderName)
                Return
            End If
        Next

        UpdateStatus(statusLabel, "Creating root folder: " & folderName)

        ' Create new root node
        Dim newRootNode As New DirectoryTreeNode(folderName, folderPath, True)
        newRootNode.Checked = True
        treeView.Nodes.Add(newRootNode)

        ReorganizeChildren(newRootNode, childrenToMove, progressBar, statusLabel)
        LoadFolderContentsWithExclusions(newRootNode, childrenToMove, progressBar, statusLabel)

        UpdateNodeAppearance(newRootNode)
    End Sub

    ' Add folder under existing parent with reorganization
    Private Sub AddFolderUnderExistingParentWithReorganization(parentNode As DirectoryTreeNode, folderPath As String,
                                                          relativePath As String, childrenToMove As List(Of ChildToMoveInfo),
                                                          Optional progressBar As ToolStripProgressBar = Nothing,
                                                          Optional statusLabel As ToolStripStatusLabel = Nothing)

        ' Split the relative path into parts to create intermediate folders if needed
        Dim pathParts As String() = relativePath.Split({"\"c, "/"c}, StringSplitOptions.RemoveEmptyEntries)

        Dim currentNode As DirectoryTreeNode = parentNode
        Dim currentPath As String = parentNode.FileFullPath

        ' Create intermediate folders if needed
        For i As Integer = 0 To pathParts.Length - 1
            Dim part As String = pathParts(i)
            currentPath = Path.Combine(currentPath, part)

            ' Check if this path already exists as a child
            Dim existingChild As DirectoryTreeNode = FindDirectChildByPath(currentNode, currentPath)

            If existingChild Is Nothing Then
                ' Create new node
                UpdateStatus(statusLabel, "Creating folder: " & part)
                Dim newNode As New DirectoryTreeNode(part, currentPath, True)
                newNode.Checked = True

                ' Insert in correct position
                Dim insertIndex As Integer = FindInsertPosition(currentNode, newNode)
                currentNode.Nodes.Insert(insertIndex, newNode)

                currentNode = newNode

                ' If this is the final folder, load its contents
                If i = pathParts.Length - 1 Then
                    LoadFolderContents(currentNode, progressBar, statusLabel)

                    ' Reorganize children under this new folder
                    ReorganizeChildren(currentNode, childrenToMove, progressBar, statusLabel)
                End If

                UpdateNodeAppearance(currentNode)
            Else
                ' Use existing node
                currentNode = existingChild
            End If
        Next

        ' Update parent nodes up the tree
        UpdateParentFolderStates(currentNode)
    End Sub

    ' Reorganize children under the new parent folder
    Private Sub ReorganizeChildren(newParentNode As DirectoryTreeNode, childrenToMove As List(Of ChildToMoveInfo),
                             Optional progressBar As ToolStripProgressBar = Nothing,
                             Optional statusLabel As ToolStripStatusLabel = Nothing)

        If childrenToMove.Count = 0 Then Return

        UpdateStatus(statusLabel, $"Reorganizing {childrenToMove.Count} folder(s) under new parent")

        ' Group children by their immediate parent path under the new folder
        Dim groupedChildren As New Dictionary(Of String, List(Of ChildToMoveInfo))

        For Each childInfo As ChildToMoveInfo In childrenToMove
            Dim pathParts As String() = childInfo.RelativePathFromNewParent.Split({"\"c, "/"c}, StringSplitOptions.RemoveEmptyEntries)

            If pathParts.Length > 0 Then
                Dim immediateParentPath As String = pathParts(0) ' First part is the immediate parent

                If Not groupedChildren.ContainsKey(immediateParentPath) Then
                    groupedChildren(immediateParentPath) = New List(Of ChildToMoveInfo)
                End If
                groupedChildren(immediateParentPath).Add(childInfo)
            End If
        Next

        ' Process each group
        For Each kvp As KeyValuePair(Of String, List(Of ChildToMoveInfo)) In groupedChildren
            Dim immediateParentPath As String = kvp.Key
            Dim childrenInGroup As List(Of ChildToMoveInfo) = kvp.Value

            ' Find or create the immediate parent folder under newParentNode
            Dim immediateParentFullPath As String = Path.Combine(newParentNode.FileFullPath, immediateParentPath)
            Dim immediateParentNode As DirectoryTreeNode = FindDirectChildByPath(newParentNode, immediateParentFullPath)

            ' Create hierarchy as needed and move the children
            For Each childInfo As ChildToMoveInfo In childrenInGroup
                MoveNodeToNewParent(childInfo, newParentNode, progressBar, statusLabel)
            Next
        Next

        ' Update appearances after all moves
        UpdateNodeAppearance(newParentNode)
        UpdateParentFolderStates(newParentNode)
    End Sub

    ' Load folder contents while excluding items that are being reorganized from elsewhere
    Private Sub LoadFolderContentsWithExclusions(folderNode As DirectoryTreeNode,
                                            childrenToMove As List(Of ChildToMoveInfo),
                                            Optional progressBar As ToolStripProgressBar = Nothing,
                                            Optional statusLabel As ToolStripStatusLabel = Nothing)
        Try
            If Not Directory.Exists(folderNode.FileFullPath) Then
                folderNode.ForeColor = Color.Red
                folderNode.Checked = False
                Return
            End If

            ' Build a set of paths that are being moved here from elsewhere in the tree
            Dim excludePaths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each childInfo As ChildToMoveInfo In childrenToMove
                excludePaths.Add(childInfo.NodeToMove.FileFullPath)
            Next

            ' Get directories and files
            Dim directories As String() = Directory.GetDirectories(folderNode.FileFullPath)
            Dim files As String() = Directory.GetFiles(folderNode.FileFullPath, "*.mp3")

            ' Filter out directories that are being moved from elsewhere
            Dim filteredDirectories As New List(Of String)
            For Each dir As String In directories
                If Not excludePaths.Contains(dir) AndAlso Not AlreadyExistsAsChild(folderNode, dir) Then
                    filteredDirectories.Add(dir)
                End If
            Next

            ' Filter out files that already exist as children
            Dim filteredFiles As New List(Of String)
            For Each file As String In files
                If Not AlreadyExistsAsChild(folderNode, file) Then
                    filteredFiles.Add(file)
                End If
            Next

            Dim totalItems As Integer = filteredDirectories.Count + filteredFiles.Count
            Dim currentItem As Integer = 0

            ' Load filtered directories
            filteredDirectories.Sort(StringComparer.OrdinalIgnoreCase)

            For Each dir As String In filteredDirectories
                Try
                    Dim dirName As String = Path.GetFileName(dir)
                    currentItem += 1
                    UpdateProgressWithCounter(progressBar, statusLabel, currentItem, totalItems, dirName)

                    Dim dirNode As New DirectoryTreeNode(dirName, dir, True)
                    dirNode.Checked = True

                    Dim insertIndex As Integer = FindInsertPosition(folderNode, dirNode)
                    folderNode.Nodes.Insert(insertIndex, dirNode)

                    LoadFolderContents(dirNode, progressBar, statusLabel)
                    UpdateNodeAppearance(dirNode)

                Catch ex As Exception
                    Continue For
                End Try
            Next

            ' Load filtered MP3 files
            filteredFiles.Sort(StringComparer.OrdinalIgnoreCase)

            For Each file As String In filteredFiles
                Try
                    Dim fileName As String = Path.GetFileName(file)
                    currentItem += 1
                    UpdateProgressWithCounter(progressBar, statusLabel, currentItem, totalItems, fileName)

                    Dim fileNode As New DirectoryTreeNode(fileName, file, False)
                    fileNode.Checked = True

                    Dim insertIndex As Integer = FindInsertPosition(folderNode, fileNode)
                    folderNode.Nodes.Insert(insertIndex, fileNode)

                    UpdateNodeAppearance(fileNode)

                Catch ex As Exception
                    Continue For
                End Try
            Next

            folderNode.LastRefreshTime = DateTime.Now

        Catch ex As Exception
            folderNode.ForeColor = Color.Red
            folderNode.Checked = False
            UpdateStatus(statusLabel, "Error: " & ex.Message)
        End Try
    End Sub

    ' Check if a path already exists as a direct child of the given node
    Private Function AlreadyExistsAsChild(parentNode As DirectoryTreeNode, fullPath As String) As Boolean
        For Each child As TreeNode In parentNode.Nodes
            Dim dirChild As DirectoryTreeNode = TryCast(child, DirectoryTreeNode)
            If dirChild IsNot Nothing AndAlso
           String.Equals(dirChild.FileFullPath, fullPath, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next
        Return False
    End Function
    Private Sub MoveNodeToNewParent(childInfo As ChildToMoveInfo, newParentRoot As DirectoryTreeNode,
                               Optional progressBar As ToolStripProgressBar = Nothing,
                               Optional statusLabel As ToolStripStatusLabel = Nothing)

        UpdateStatus(statusLabel, $"Moving folder: {childInfo.NodeToMove.Text}")

        ' Remove from original parent
        If childInfo.OriginalParent IsNot Nothing Then
            childInfo.OriginalParent.Nodes.Remove(childInfo.NodeToMove)

            ' Update the original parent's appearance
            Dim originalDirParent As DirectoryTreeNode = TryCast(childInfo.OriginalParent, DirectoryTreeNode)
            If originalDirParent IsNot Nothing Then
                UpdateNodeAppearance(originalDirParent)
                UpdateParentFolderStates(originalDirParent)
            End If
        Else
            ' Remove from tree root
            childInfo.NodeToMove.TreeView.Nodes.Remove(childInfo.NodeToMove)
        End If

        ' Create intermediate path if needed
        Dim pathParts As String() = childInfo.RelativePathFromNewParent.Split({"\"c, "/"c}, StringSplitOptions.RemoveEmptyEntries)

        Dim currentParent As DirectoryTreeNode = newParentRoot
        Dim currentPath As String = newParentRoot.FileFullPath

        ' Create intermediate folders (all parts except the last, which is the node being moved)
        For i As Integer = 0 To pathParts.Length - 2
            Dim part As String = pathParts(i)
            currentPath = Path.Combine(currentPath, part)

            Dim existingChild As DirectoryTreeNode = FindDirectChildByPath(currentParent, currentPath)
            If existingChild Is Nothing Then
                ' Create intermediate folder
                Dim intermediateNode As New DirectoryTreeNode(part, currentPath, True)
                intermediateNode.Checked = True

                Dim insertIndex As Integer = FindInsertPosition(currentParent, intermediateNode)
                currentParent.Nodes.Insert(insertIndex, intermediateNode)

                ' Load contents for the intermediate folder if it exists physically
                If Directory.Exists(currentPath) Then
                    LoadFolderContents(intermediateNode, progressBar, statusLabel)
                End If

                UpdateNodeAppearance(intermediateNode)
                currentParent = intermediateNode
            Else
                currentParent = existingChild
            End If
        Next

        ' Add the moved node to its final parent
        Dim finalInsertIndex As Integer = FindInsertPosition(currentParent, childInfo.NodeToMove)
        currentParent.Nodes.Insert(finalInsertIndex, childInfo.NodeToMove)

        ' Update appearances
        UpdateNodeAppearance(childInfo.NodeToMove)
        UpdateNodeAppearance(currentParent)
    End Sub

    ' Enhanced AddFolderAsRoot with duplicate checking
    Private Sub AddFolderAsRoot(treeView As TreeView, folderPath As String,
                           Optional progressBar As ToolStripProgressBar = Nothing,
                           Optional statusLabel As ToolStripStatusLabel = Nothing)

        Dim folderName As String = Path.GetFileName(folderPath)
        If String.IsNullOrEmpty(folderName) Then folderName = folderPath

        ' Check if root with same name already exists - use different variable name
        For Each existingRoot As TreeNode In treeView.Nodes
            Dim dirRoot As DirectoryTreeNode = TryCast(existingRoot, DirectoryTreeNode)
            If dirRoot IsNot Nothing AndAlso
           String.Equals(dirRoot.Text, folderName, StringComparison.OrdinalIgnoreCase) Then
                UpdateStatus(statusLabel, "Root folder with same name already exists: " & folderName)
                Return
            End If
        Next

        UpdateStatus(statusLabel, "Creating root folder: " & folderName)

        Dim newRootNode As New DirectoryTreeNode(folderName, folderPath, True)
        newRootNode.Checked = True
        treeView.Nodes.Add(newRootNode)

        LoadFolderContents(newRootNode, progressBar, statusLabel)
        UpdateNodeAppearance(newRootNode)
    End Sub


    ' Add folder under existing parent with proper hierarchy
    Private Sub AddFolderUnderExistingParent(parentNode As DirectoryTreeNode, folderPath As String,
                                        relativePath As String,
                                        Optional progressBar As ToolStripProgressBar = Nothing,
                                        Optional statusLabel As ToolStripStatusLabel = Nothing)

        ' Split the relative path into parts
        Dim pathParts As String() = relativePath.Split({"\"c, "/"c}, StringSplitOptions.RemoveEmptyEntries)

        Dim currentNode As DirectoryTreeNode = parentNode
        Dim currentPath As String = parentNode.FileFullPath

        ' Create intermediate folders if needed
        For i As Integer = 0 To pathParts.Length - 1
            Dim part As String = pathParts(i)
            currentPath = Path.Combine(currentPath, part)

            ' Check if this path already exists as a child
            Dim existingChild As DirectoryTreeNode = FindDirectChildByPath(currentNode, currentPath)

            If existingChild Is Nothing Then
                ' Create new node
                UpdateStatus(statusLabel, "Creating intermediate folder: " & part)
                Dim newNode As New DirectoryTreeNode(part, currentPath, True)
                newNode.Checked = True

                ' Insert in correct position
                Dim insertIndex As Integer = FindInsertPosition(currentNode, newNode)
                currentNode.Nodes.Insert(insertIndex, newNode)

                currentNode = newNode

                ' If this is the final folder, load its contents
                If i = pathParts.Length - 1 Then
                    LoadFolderContents(currentNode, progressBar, statusLabel)
                End If

                UpdateNodeAppearance(currentNode)
            Else
                ' Use existing node
                currentNode = existingChild
            End If
        Next

        ' Update parent nodes up the tree
        UpdateParentFolderStates(currentNode)
    End Sub

    ' Find direct child by path (not recursive)
    Private Function FindDirectChildByPath(parentNode As TreeNode, fullPath As String) As DirectoryTreeNode
        For Each childNode As TreeNode In parentNode.Nodes
            Dim dirNode As DirectoryTreeNode = TryCast(childNode, DirectoryTreeNode)
            If dirNode IsNot Nothing AndAlso
           String.Equals(dirNode.FileFullPath, fullPath, StringComparison.OrdinalIgnoreCase) Then
                Return dirNode
            End If
        Next
        Return Nothing
    End Function

    ' Data class to hold parent folder information
    Private Class ParentFolderInfo
        Public Property ParentNode As DirectoryTreeNode
        Public Property RelativePath As String

        Public Sub New(parentNode As DirectoryTreeNode, relativePath As String)
            Me.ParentNode = parentNode
            Me.RelativePath = relativePath
        End Sub
    End Class

    ' Check if folder already exists in tree
    Private Function FolderExistsInTree(treeView As TreeView, folderPath As String) As Boolean
        For Each treeRoot As TreeNode In treeView.Nodes
            If FolderExistsInNode(treeRoot, folderPath) Then
                Return True
            End If
        Next
        Return False
    End Function

    Private Function FolderExistsInNode(node As TreeNode, folderPath As String) As Boolean
        Dim dirNode As DirectoryTreeNode = TryCast(node, DirectoryTreeNode)
        If dirNode IsNot Nothing Then
            ' Check exact match
            If String.Equals(dirNode.FileFullPath, folderPath, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If

            ' Check children recursively
            For Each child As TreeNode In node.Nodes
                If FolderExistsInNode(child, folderPath) Then
                    Return True
                End If
            Next
        End If
        Return False
    End Function

    ' Find the best parent folder for the new folder
    Private Function FindBestParentForFolder(treeView As TreeView, folderPath As String) As ParentFolderInfo
        Dim bestParent As DirectoryTreeNode = Nothing
        Dim longestMatchPath As String = ""
        Dim relativePath As String = ""

        ' Check all nodes in the tree - use different variable name
        For Each treeRoot As TreeNode In treeView.Nodes
            Dim result As ParentFolderInfo = FindParentInNodeTree(treeRoot, folderPath)
            If result IsNot Nothing AndAlso result.ParentNode IsNot Nothing Then
                ' Check if this is a better match (longer common path)
                If result.ParentNode.FileFullPath.Length > longestMatchPath.Length Then
                    bestParent = result.ParentNode
                    longestMatchPath = result.ParentNode.FileFullPath
                    relativePath = result.RelativePath
                End If
            End If
        Next

        Return New ParentFolderInfo(bestParent, relativePath)
    End Function


    Private Function FindParentInNodeTree(node As TreeNode, folderPath As String) As ParentFolderInfo
        Dim dirNode As DirectoryTreeNode = TryCast(node, DirectoryTreeNode)
        If dirNode Is Nothing OrElse Not dirNode.IsFolder Then Return Nothing

        ' Check if this node is a parent of the target folder
        If folderPath.StartsWith(dirNode.FileFullPath, StringComparison.OrdinalIgnoreCase) AndAlso
       folderPath.Length > dirNode.FileFullPath.Length Then

            ' Calculate relative path
            Dim relativePath As String = folderPath.Substring(dirNode.FileFullPath.Length).TrimStart("\"c, "/"c)

            ' Check if this is a direct child or deeper
            Dim pathParts As String() = relativePath.Split({"\"c, "/"c}, StringSplitOptions.RemoveEmptyEntries)

            ' Look for a better (deeper) parent in children
            For Each child As TreeNode In node.Nodes
                Dim childResult As ParentFolderInfo = FindParentInNodeTree(child, folderPath)
                If childResult IsNot Nothing AndAlso childResult.ParentNode IsNot Nothing Then
                    Return childResult ' Found a deeper parent
                End If
            Next

            ' This node is the best parent found
            Return New ParentFolderInfo(dirNode, relativePath)
        End If

        Return Nothing
    End Function

    ' Load folder contents
    Private Sub LoadFolderContents(folderNode As DirectoryTreeNode,
                              Optional progressBar As ToolStripProgressBar = Nothing,
                              Optional statusLabel As ToolStripStatusLabel = Nothing)
        Try
            If Not Directory.Exists(folderNode.FileFullPath) Then
                folderNode.ForeColor = Color.Red
                folderNode.Checked = False
                Return
            End If

            folderNode.Nodes.Clear()

            ' Count items for progress
            Dim directories As String() = Directory.GetDirectories(folderNode.FileFullPath)
            Dim files As String() = Directory.GetFiles(folderNode.FileFullPath, "*.mp3")
            Dim totalItems As Integer = directories.Length + files.Length
            Dim currentItem As Integer = 0

            ' Load directories
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase)

            For Each dir As String In directories
                Try
                    Dim dirName As String = Path.GetFileName(dir)
                    currentItem += 1
                    UpdateProgressWithCounter(progressBar, statusLabel, currentItem, totalItems, dirName)

                    Dim dirNode As New DirectoryTreeNode(dirName, dir, True)
                    dirNode.Checked = True
                    folderNode.Nodes.Add(dirNode)

                    LoadFolderContents(dirNode, progressBar, statusLabel)
                    UpdateNodeAppearance(dirNode)

                Catch ex As Exception
                    Continue For
                End Try
            Next

            ' Load MP3 files only
            Array.Sort(files, StringComparer.OrdinalIgnoreCase)

            For Each file As String In files
                Try
                    Dim fileName As String = Path.GetFileName(file)
                    currentItem += 1
                    UpdateProgressWithCounter(progressBar, statusLabel, currentItem, totalItems, fileName)

                    Dim fileNode As New DirectoryTreeNode(fileName, file, False)
                    fileNode.Checked = True
                    folderNode.Nodes.Add(fileNode)

                    UpdateNodeAppearance(fileNode)

                Catch ex As Exception
                    Continue For
                End Try
            Next

            folderNode.LastRefreshTime = DateTime.Now

        Catch ex As Exception
            folderNode.ForeColor = Color.Red
            folderNode.Checked = False
            UpdateStatus(statusLabel, "Error: " & ex.Message)
        End Try
    End Sub
    ' Update node appearance (images and colors)
    ' Fixed UpdateNodeAppearance function
    Private Sub UpdateNodeAppearance(node As DirectoryTreeNode)
        If node Is Nothing Then Return

        If node.IsFolder Then
            If node.Nodes.Count = 0 Then
                ' Empty folder - should be unchecked and gray
                If node.Checked Then  ' Only change if different
                    node.Checked = False
                End If
                node.ForeColor = Color.Gray
                node.ImageIndex = IMG_UNCHECKED_FOLDER
                node.SelectedImageIndex = IMG_UNCHECKED_FOLDER
            Else
                ' Folder has children - determine state based on children
                Dim childrenAreChecked As Boolean = HasCheckedChildren(node)
                Dim childrenAreUnchecked As Boolean = HasUncheckedChildren(node)

                ' Determine what the check state should be
                Dim shouldBeChecked As Boolean = childrenAreChecked

                ' Only change check state if it's different (to avoid triggering events)
                If node.Checked <> shouldBeChecked Then
                    node.Checked = shouldBeChecked
                End If

                ' Set colors based on final state
                If childrenAreChecked AndAlso Not childrenAreUnchecked Then
                    ' All children checked
                    node.ForeColor = Color.Black
                ElseIf Not childrenAreChecked AndAlso childrenAreUnchecked Then
                    ' All children unchecked
                    node.ForeColor = Color.Gray
                Else
                    ' Mixed state
                    node.ForeColor = Color.DarkGray
                End If

                ' Set folder image based on final check state and content
                If Not node.Checked Then
                    node.ImageIndex = IMG_UNCHECKED_FOLDER
                    node.SelectedImageIndex = IMG_UNCHECKED_FOLDER
                Else
                    If HasOnlyMp3Files(node) Then
                        node.ImageIndex = IMG_MUSIC_FOLDER
                        node.SelectedImageIndex = IMG_MUSIC_FOLDER
                    Else
                        node.ImageIndex = IMG_SUB_FOLDER
                        node.SelectedImageIndex = IMG_SUB_FOLDER
                    End If
                End If
            End If
        Else
            ' File appearance - don't change check state here
            If node.Checked Then
                node.ForeColor = Color.Black
                node.ImageIndex = IMG_MUSIC_FILE
                node.SelectedImageIndex = IMG_MUSIC_FILE
            Else
                node.ForeColor = Color.Gray
                node.ImageIndex = IMG_UNCHECKED_FILE
                node.SelectedImageIndex = IMG_UNCHECKED_FILE
            End If
        End If
    End Sub

    ' Update parent folder states after children are modified
    Private Sub UpdateParentFolderStates(node As DirectoryTreeNode)
        Dim parent As DirectoryTreeNode = TryCast(node.Parent, DirectoryTreeNode)
        While parent IsNot Nothing
            ' Update parent's appearance which will recalculate its state based on children
            UpdateNodeAppearance(parent)

            ' Move up to next parent
            parent = TryCast(parent.Parent, DirectoryTreeNode)
        End While
    End Sub
    ' Update folder state after its children have been modified
    Private Sub UpdateFolderAfterChildrenChanged(folderNode As DirectoryTreeNode)
        If folderNode Is Nothing OrElse Not folderNode.IsFolder Then Return

        ' Set the flag to prevent event loops
        Dim wasUpdating As Boolean = isUpdatingNodeStates
        isUpdatingNodeStates = True

        Try
            ' Update this folder's state based on its current children
            UpdateNodeAppearance(folderNode)

            ' Update all parent folders up the tree
            UpdateParentFolderStates(folderNode)

        Finally
            isUpdatingNodeStates = wasUpdating
        End Try
    End Sub

    ' Check if folder has only MP3 files (no subfolders)
    Private Function HasOnlyMp3Files(folderNode As DirectoryTreeNode) As Boolean
        If Not folderNode.IsFolder Then Return False

        Dim hasFiles As Boolean = False
        Dim hasSubfolders As Boolean = False

        For Each child As TreeNode In folderNode.Nodes
            Dim dirChild As DirectoryTreeNode = TryCast(child, DirectoryTreeNode)
            If dirChild IsNot Nothing Then
                If dirChild.IsFolder Then
                    hasSubfolders = True
                Else
                    hasFiles = True
                End If
            End If
        Next

        Return hasFiles AndAlso Not hasSubfolders
    End Function

    Private Function HasUncheckedChildren(folderNode As DirectoryTreeNode) As Boolean
        If folderNode Is Nothing Then Return False

        For Each child As TreeNode In folderNode.Nodes
            If Not child.Checked Then Return True
        Next
        Return False
    End Function

    ' Also fix HasCheckedChildren for consistency
    Private Function HasCheckedChildren(folderNode As DirectoryTreeNode) As Boolean
        If folderNode Is Nothing Then Return False

        For Each child As TreeNode In folderNode.Nodes
            If child.Checked Then Return True
        Next
        Return False
    End Function

    ' Handle checkbox changes
    ' Enhanced TreeView_AfterCheck with proper parent updates
    Public Sub TreeView_AfterCheck(sender As Object, e As TreeViewEventArgs)
        ' Prevent recursive calls during programmatic updates
        If isUpdatingNodeStates Then Return

        Dim node As DirectoryTreeNode = TryCast(e.Node, DirectoryTreeNode)
        If node Is Nothing Then Return

        ' Set flag to prevent recursion
        isUpdatingNodeStates = True

        Try
            If node.IsFolder Then
                ' Folder checked/unchecked - apply to all children
                SetChildrenState(node, node.Checked)
            End If

            ' Update appearance of this node
            UpdateNodeAppearance(node)

            ' Update parent nodes appearance and check states
            UpdateParentFolderStates(node)

        Finally
            ' Always reset the flag
            isUpdatingNodeStates = False
        End Try
    End Sub

    ' Set all children to same state as parent
    ' Fixed SetChildrenState - don't change the parent node that called it
    Private Sub SetChildrenState(folderNode As DirectoryTreeNode, isChecked As Boolean)
        ' Only update children, not the parent folder itself
        For Each child As TreeNode In folderNode.Nodes
            child.Checked = isChecked
            Dim dirChild As DirectoryTreeNode = TryCast(child, DirectoryTreeNode)
            If dirChild IsNot Nothing Then
                If dirChild.IsFolder Then
                    ' Recursively set children of subfolders
                    SetChildrenState(dirChild, isChecked)
                End If
                UpdateNodeAppearance(dirChild)
            End If
        Next
    End Sub

    ' Update parent nodes appearance
    Private Sub UpdateParentNodes(node As DirectoryTreeNode)
        Dim parent As DirectoryTreeNode = TryCast(node.Parent, DirectoryTreeNode)
        While parent IsNot Nothing
            UpdateNodeAppearance(parent)
            parent = TryCast(parent.Parent, DirectoryTreeNode)
        End While
    End Sub

    ' 2. Remove element
    Public Sub RemoveElement(treeView As TreeView, index As Integer)
        Try
            If treeView Is Nothing OrElse index < 0 OrElse index >= treeView.Nodes.Count Then Return
            treeView.Nodes.RemoveAt(index)
            treeView.Refresh()
        Catch ex As Exception
        End Try
    End Sub

    ' 3. Refresh element
    Public Sub RefreshElement(treeView As TreeView, Optional index As Integer = 0,
                         Optional progressBar As ToolStripProgressBar = Nothing,
                         Optional statusLabel As ToolStripStatusLabel = Nothing)
        Try
            If treeView Is Nothing Then Return

            UpdateStatus(statusLabel, "Starting refresh...")

            If index = 0 Then
                ' Refresh all
                For i As Integer = 0 To treeView.Nodes.Count - 1
                    RefreshSingleNode(treeView, i, progressBar, statusLabel)
                Next
            Else
                ' Refresh specific
                If index > 0 AndAlso index <= treeView.Nodes.Count Then
                    RefreshSingleNode(treeView, index - 1, progressBar, statusLabel)
                End If
            End If

            UpdateStatus(statusLabel, "Refresh completed")
            treeView.Refresh()

        Catch ex As Exception
            UpdateStatus(statusLabel, "Error: " & ex.Message)
        End Try
    End Sub
    ' Refresh single node
    Private Sub RefreshSingleNode(treeView As TreeView, nodeIndex As Integer,
                             Optional progressBar As ToolStripProgressBar = Nothing,
                             Optional statusLabel As ToolStripStatusLabel = Nothing)
        Dim rootNode As DirectoryTreeNode = TryCast(treeView.Nodes(nodeIndex), DirectoryTreeNode)
        If rootNode Is Nothing OrElse Not rootNode.IsFolder Then Return

        ' Check if root folder still exists
        If Not Directory.Exists(rootNode.FileFullPath) Then
            UpdateStatus(statusLabel, "Root folder no longer exists: " & rootNode.Text)

            ' Ask user if they want to remove the root node
            Dim result As DialogResult = MessageBox.Show(
            $"The folder '{rootNode.Text}' no longer exists.{vbcrlf}{vbcrlf}Do you want to remove it from the tree?",
            "Folder Not Found",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question)

            If result = DialogResult.Yes Then
                treeView.Nodes.RemoveAt(nodeIndex)
                UpdateStatus(statusLabel, "Removed missing root folder: " & rootNode.Text)
            Else
                ' Mark as red but keep it
                rootNode.ForeColor = Color.Red
                rootNode.Checked = False
                UpdateNodeAppearance(rootNode)
                UpdateStatus(statusLabel, "Marked missing folder as unavailable: " & rootNode.Text)
            End If
            Return
        End If

        ' Store current check states of all nodes
        Dim checkStates As Dictionary(Of String, Boolean) = CollectCheckStates(rootNode)

        ' Store expansion state
        Dim wasExpanded As Boolean = rootNode.IsExpanded

        ' Reload contents
        RefreshFolderContents(rootNode, checkStates, progressBar, statusLabel)
        ' Update this folder's appearance based on final children state
        UpdateNodeAppearance(rootNode)

        ' Update parent folders up the tree
        UpdateParentFolderStates(rootNode)
        rootNode.LastRefreshTime = DateTime.Now

        ' IMPORTANT: Update the entire tree hierarchy after refresh
        UpdateFolderAfterChildrenChanged(rootNode)

        ' Restore expansion
        If wasExpanded Then rootNode.Expand()

    End Sub



    ' Collect all check states recursively
    Private Function CollectCheckStates(node As TreeNode) As Dictionary(Of String, Boolean)
        Dim states As New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase)
        CollectCheckStatesRecursive(node, states)
        Return states
    End Function

    Private Sub CollectCheckStatesRecursive(node As TreeNode, states As Dictionary(Of String, Boolean))
        Dim dirNode As DirectoryTreeNode = TryCast(node, DirectoryTreeNode)
        If dirNode IsNot Nothing Then
            states(dirNode.FileFullPath) = dirNode.Checked
        End If

        For Each childNode As TreeNode In node.Nodes
            CollectCheckStatesRecursive(childNode, states)
        Next
    End Sub

    ' Updated RefreshFolderContents with state preservation
    ' Updated RefreshFolderContents with proper parent updates
    Private Sub RefreshFolderContents(folderNode As DirectoryTreeNode,
                                 checkStates As Dictionary(Of String, Boolean),
                                 Optional progressBar As ToolStripProgressBar = Nothing,
                                 Optional statusLabel As ToolStripStatusLabel = Nothing)
        Try
            If Not Directory.Exists(folderNode.FileFullPath) Then
                folderNode.ForeColor = Color.Red
                folderNode.Checked = False
                Return
            End If

            ' Set flag to prevent AfterCheck events during refresh
            isUpdatingNodeStates = True

            ' Get current directory contents first
            Dim currentDirectories As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim currentFiles As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            Try
                For Each dir As String In Directory.GetDirectories(folderNode.FileFullPath)
                    currentDirectories.Add(dir)
                Next

                For Each file As String In Directory.GetFiles(folderNode.FileFullPath, "*.mp3")
                    currentFiles.Add(file)
                Next
            Catch ex As Exception
                folderNode.ForeColor = Color.Red
                UpdateStatus(statusLabel, "Access denied: " & folderNode.Text)
                Return
            End Try

            ' Track if any children were added/removed to know if we need parent updates
            Dim childrenChanged As Boolean = False

            ' REMOVE nodes that no longer exist physically
            For i As Integer = folderNode.Nodes.Count - 1 To 0 Step -1
                Dim child As DirectoryTreeNode = TryCast(folderNode.Nodes(i), DirectoryTreeNode)
                If child IsNot Nothing Then
                    Dim stillExists As Boolean = If(child.IsFolder,
                                               currentDirectories.Contains(child.FileFullPath),
                                               currentFiles.Contains(child.FileFullPath))

                    If Not stillExists Then
                        UpdateStatus(statusLabel, "Removing: " & child.Text)
                        folderNode.Nodes.RemoveAt(i)
                        childrenChanged = True
                    End If
                End If
            Next

            ' Store remaining existing nodes info
            Dim existingNodes As New Dictionary(Of String, DirectoryTreeNode)(StringComparer.OrdinalIgnoreCase)
            For Each child As TreeNode In folderNode.Nodes
                Dim dirChild As DirectoryTreeNode = TryCast(child, DirectoryTreeNode)
                If dirChild IsNot Nothing Then
                    existingNodes(dirChild.FileFullPath) = dirChild
                End If
            Next

            ' Add new directories
            For Each dir As String In currentDirectories
                If Not existingNodes.ContainsKey(dir) Then
                    Dim dirName As String = Path.GetFileName(dir)
                    UpdateStatus(statusLabel, "New folder: " & dirName)

                    Dim newNode As New DirectoryTreeNode(dirName, dir, True)
                    newNode.Checked = If(checkStates.ContainsKey(dir), checkStates(dir), True)

                    Dim insertIndex As Integer = FindInsertPosition(folderNode, newNode)
                    folderNode.Nodes.Insert(insertIndex, newNode)

                    RefreshFolderContents(newNode, checkStates, progressBar, statusLabel)
                    UpdateNodeAppearance(newNode)
                    childrenChanged = True
                End If
            Next

            ' Add new files
            For Each file As String In currentFiles
                If Not existingNodes.ContainsKey(file) Then
                    Dim fileName As String = Path.GetFileName(file)
                    UpdateStatus(statusLabel, "New file: " & fileName)

                    Dim newNode As New DirectoryTreeNode(fileName, file, False)
                    newNode.Checked = If(checkStates.ContainsKey(file), checkStates(file), True)

                    Dim insertIndex As Integer = FindInsertPosition(folderNode, newNode)
                    folderNode.Nodes.Insert(insertIndex, newNode)

                    UpdateNodeAppearance(newNode)
                    childrenChanged = True
                End If
            Next

            ' Restore check states and refresh existing items
            For Each child As TreeNode In folderNode.Nodes
                Dim dirChild As DirectoryTreeNode = TryCast(child, DirectoryTreeNode)
                If dirChild IsNot Nothing Then
                    If checkStates.ContainsKey(dirChild.FileFullPath) Then
                        dirChild.Checked = checkStates(dirChild.FileFullPath)
                    End If

                    UpdateNodeAppearance(dirChild)

                    If dirChild.IsFolder Then
                        RefreshFolderContents(dirChild, checkStates, progressBar, statusLabel)
                    End If
                End If
            Next

            ' IMPORTANT: Update this folder's appearance based on final children state
            UpdateNodeAppearance(folderNode)

            ' If children changed, update parent folders up the tree
            If childrenChanged Then
                UpdateParentFolderStates(folderNode)
            End If

            folderNode.LastRefreshTime = DateTime.Now

        Catch ex As Exception
            folderNode.ForeColor = Color.Red
            folderNode.Checked = False
            UpdateStatus(statusLabel, "Error refreshing: " & folderNode.Text)
        Finally
            isUpdatingNodeStates = False
        End Try
    End Sub

    ' Find correct insert position (folders first, then alphabetical)
    Private Function FindInsertPosition(parentNode As TreeNode, newNode As DirectoryTreeNode) As Integer
        For i As Integer = 0 To parentNode.Nodes.Count - 1
            Dim existingNode As DirectoryTreeNode = TryCast(parentNode.Nodes(i), DirectoryTreeNode)
            If existingNode Is Nothing Then Continue For

            ' Folders come before files
            If newNode.IsFolder AndAlso Not existingNode.IsFolder Then
                Continue For
            ElseIf Not newNode.IsFolder AndAlso existingNode.IsFolder Then
                Return i
            End If

            ' Alphabetical order within same type
            If String.Compare(newNode.Text, existingNode.Text, StringComparison.OrdinalIgnoreCase) < 0 Then
                Return i
            End If
        Next

        Return parentNode.Nodes.Count
    End Function

    ' 4. Save state
    ' Updated SaveTreeViewState with dialog
    Public Sub SaveTreeViewState(treeView As TreeView,
                            Optional progressBar As ToolStripProgressBar = Nothing,
                            Optional statusLabel As ToolStripStatusLabel = Nothing,
                                 Optional silentmode As Boolean = False)
        Try
            Dim saveFilename As String = "TreeviewState.json"
            If treeView Is Nothing Then Return
            If Not silentmode Then
                ' Show SaveFileDialog
                Using saveDialog As New SaveFileDialog()
                    saveDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
                    saveDialog.DefaultExt = "json"
                    saveDialog.Title = "Save TreeView State"
                    saveDialog.FileName = "TreeViewState.json"

                    If saveDialog.ShowDialog() = DialogResult.OK Then
                        UpdateStatus(statusLabel, "Saving state...")

                        saveFilename = saveDialog.FileName
                    Else
                        UpdateStatus(statusLabel, "Save cancelled")
                        saveFilename = ""
                    End If
                End Using
            End If
            If Not saveFilename = "" Then
                Dim rootStates As New List(Of TreeNodeState)
                Dim totalNodes As Integer = CountAllNodes(treeView)
                Dim currentNode As Integer = 0

                For Each rootNode As TreeNode In treeView.Nodes
                    Dim state As TreeNodeState = ConvertNodeToState(rootNode, progressBar, statusLabel, currentNode, totalNodes)
                    If state IsNot Nothing Then rootStates.Add(state)
                Next

                UpdateStatus(statusLabel, "Writing to file...")
                Dim json As String = JsonConvert.SerializeObject(rootStates, Formatting.Indented)
                File.WriteAllText(saveFilename, json, System.Text.Encoding.UTF8)

                UpdateStatus(statusLabel, "State saved successfully to: " & Path.GetFileName(saveFilename))
            End If
        Catch ex As Exception
            UpdateStatus(statusLabel, "Save error: " & ex.Message)
            MessageBox.Show("Error saving TreeView state: " & ex.Message, "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally

        End Try
    End Sub

    ' 5. Load state
    ' Updated LoadTreeViewState with dialog
    Public Sub LoadTreeViewState(treeView As TreeView,
                            Optional progressBar As ToolStripProgressBar = Nothing,
                            Optional statusLabel As ToolStripStatusLabel = Nothing,
                                   Optional silentmode As Boolean = False)
        Try
            Dim LoadFilename As String = "TreeviewState.json"
            If treeView Is Nothing Then Return
            If Not silentmode Then
                ' Show OpenFileDialog
                Using openDialog As New OpenFileDialog()
                    openDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
                    openDialog.Title = "Load TreeView State"
                    openDialog.CheckFileExists = True

                    If openDialog.ShowDialog() = DialogResult.OK Then
                        UpdateStatus(statusLabel, "Loading state...")
                        LoadFilename = openDialog.FileName
                    Else
                        UpdateStatus(statusLabel, "Load cancelled")
                        LoadFilename = ""
                    End If
                End Using
            End If
            If Not LoadFilename = "" And System.IO.File.Exists(LoadFilename) Then
                Dim json As String = File.ReadAllText(LoadFilename, System.Text.Encoding.UTF8)
                Dim rootStates As List(Of TreeNodeState) = JsonConvert.DeserializeObject(Of List(Of TreeNodeState))(json)

                If rootStates Is Nothing Then
                    MessageBox.Show("Invalid file format", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Return
                End If

                treeView.Nodes.Clear()
                Dim totalNodes As Integer = CountStatesRecursive(rootStates)
                Dim currentNode As Integer = 0

                For Each rootState As TreeNodeState In rootStates
                    Dim rootNode As DirectoryTreeNode = ConvertStateToNode(rootState, progressBar, statusLabel, currentNode, totalNodes)
                    If rootNode IsNot Nothing Then
                        treeView.Nodes.Add(rootNode)
                        If rootState.IsExpanded Then rootNode.Expand()
                    End If
                Next

                UpdateStatus(statusLabel, "State loaded successfully from: " & Path.GetFileName(LoadFilename))
                treeView.Refresh()
            End If
        Catch ex As Exception
            UpdateStatus(statusLabel, "Load error: " & ex.Message)
            MessageBox.Show("Error loading TreeView state: " & ex.Message, "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' Count all nodes for progress tracking
    Private Function CountAllNodes(treeView As TreeView) As Integer
        Dim count As Integer = 0
        For Each rootNode As TreeNode In treeView.Nodes
            count += CountNodesRecursive(rootNode)
        Next
        Return count
    End Function

    Private Function CountNodesRecursive(node As TreeNode) As Integer
        Dim count As Integer = 1 ' Count the node itself
        For Each childNode As TreeNode In node.Nodes
            count += CountNodesRecursive(childNode)
        Next
        Return count
    End Function

    ' Count states recursively
    Private Function CountStatesRecursive(states As List(Of TreeNodeState)) As Integer
        Dim count As Integer = 0
        For Each state In states
            count += 1 ' Count the state itself
            count += CountStatesRecursive(state.Children)
        Next
        Return count
    End Function

    ' Enhanced ConvertNodeToState with progress
    Private Function ConvertNodeToState(node As TreeNode,
                                       Optional progressBar As ToolStripProgressBar = Nothing,
                                       Optional statusLabel As ToolStripStatusLabel = Nothing,
                                       Optional ByRef currentNode As Integer = 0,
                                       Optional totalNodes As Integer = 0) As TreeNodeState
        Dim dirNode As DirectoryTreeNode = TryCast(node, DirectoryTreeNode)
        If dirNode Is Nothing Then Return Nothing

        currentNode += 1
        If progressBar IsNot Nothing AndAlso totalNodes > 0 Then
            UpdateProgressWithCounter(progressBar, statusLabel, currentNode, totalNodes, "Saving: " & dirNode.Text)
        End If

        Dim state As New TreeNodeState With {
            .FileFullPath = dirNode.FileFullPath,
            .Text = dirNode.Text,
            .IsChecked = dirNode.Checked,
            .IsFolder = dirNode.IsFolder,
            .IsExpanded = dirNode.IsExpanded
        }

        For Each child As TreeNode In node.Nodes
            Dim childState As TreeNodeState = ConvertNodeToState(child, progressBar, statusLabel, currentNode, totalNodes)
            If childState IsNot Nothing Then state.Children.Add(childState)
        Next

        Return state
    End Function

    ' Enhanced ConvertStateToNode with progress
    Private Function ConvertStateToNode(state As TreeNodeState,
                                       Optional progressBar As ToolStripProgressBar = Nothing,
                                       Optional statusLabel As ToolStripStatusLabel = Nothing,
                                       Optional ByRef currentNode As Integer = 0,
                                       Optional totalNodes As Integer = 0) As DirectoryTreeNode
        Try
            currentNode += 1
            If progressBar IsNot Nothing AndAlso totalNodes > 0 Then
                UpdateProgressWithCounter(progressBar, statusLabel, currentNode, totalNodes, "Loading: " & state.Text)
            End If

            Dim node As New DirectoryTreeNode(state.Text, state.FileFullPath, state.IsFolder)
            node.Checked = state.IsChecked

            ' Check if exists
            Dim exists As Boolean = If(state.IsFolder, Directory.Exists(state.FileFullPath), File.Exists(state.FileFullPath))
            If Not exists Then
                node.ForeColor = Color.Red
                node.Checked = False
            End If

            ' Add children
            For Each childState As TreeNodeState In state.Children
                Dim childNode As DirectoryTreeNode = ConvertStateToNode(childState, progressBar, statusLabel, currentNode, totalNodes)
                If childNode IsNot Nothing Then node.Nodes.Add(childNode)
            Next

            UpdateNodeAppearance(node)
            Return node

        Catch ex As Exception
            Return Nothing
        End Try
    End Function



    ' Handle multiple selection with Ctrl+Click and Shift+Click
    Public Sub TreeView_MouseDown(sender As Object, e As MouseEventArgs)
        Dim treeView As TreeView = TryCast(sender, TreeView)
        If treeView Is Nothing Then Return

        Dim clickedNode As TreeNode = treeView.GetNodeAt(e.Location)
        If clickedNode Is Nothing Then Return

        ' Check if Ctrl or Shift is pressed
        If Control.ModifierKeys = Keys.Control Then
            ' Ctrl+Click: Toggle selection
            If selectedNodes.Contains(clickedNode) Then
                selectedNodes.Remove(clickedNode)
                clickedNode.BackColor = treeView.BackColor
            Else
                selectedNodes.Add(clickedNode)
                clickedNode.BackColor = SystemColors.Highlight
                clickedNode.ForeColor = SystemColors.HighlightText
            End If
        ElseIf Control.ModifierKeys = Keys.Shift AndAlso selectedNodes.Count > 0 Then
            ' Shift+Click: Select range
            Dim lastSelected As TreeNode = selectedNodes.LastOrDefault()
            If lastSelected IsNot Nothing Then
                SelectRange(treeView, lastSelected, clickedNode)
            End If
        Else
            ' Normal click: Clear selection and select this node
            ClearSelection(treeView)
            selectedNodes.Add(clickedNode)
            clickedNode.BackColor = SystemColors.Highlight
            clickedNode.ForeColor = SystemColors.HighlightText
        End If

        treeView.SelectedNode = clickedNode
    End Sub

    ' Clear all selections
    Private Sub ClearSelection(treeView As TreeView)
        For Each node As TreeNode In selectedNodes
            node.BackColor = treeView.BackColor
            node.ForeColor = Color.Black
        Next
        selectedNodes.Clear()
    End Sub

    ' Select range between two nodes
    Private Sub SelectRange(treeView As TreeView, startNode As TreeNode, endNode As TreeNode)
        ClearSelection(treeView)

        Dim allNodes As New List(Of TreeNode)
        GetAllNodesFlat(treeView, allNodes)

        Dim startIndex As Integer = allNodes.IndexOf(startNode)
        Dim endIndex As Integer = allNodes.IndexOf(endNode)

        If startIndex = -1 OrElse endIndex = -1 Then Return

        ' Ensure start is before end
        If startIndex > endIndex Then
            Dim temp As Integer = startIndex
            startIndex = endIndex
            endIndex = temp
        End If

        ' Select all nodes in range
        For i As Integer = startIndex To endIndex
            selectedNodes.Add(allNodes(i))
            allNodes(i).BackColor = SystemColors.Highlight
            allNodes(i).ForeColor = SystemColors.HighlightText
        Next
    End Sub

    ' Get all nodes in flat list (for range selection)
    Private Sub GetAllNodesFlat(treeView As TreeView, nodeList As List(Of TreeNode))
        For Each node As TreeNode In treeView.Nodes
            GetAllNodesRecursive(node, nodeList)
        Next
    End Sub

    Private Sub GetAllNodesRecursive(node As TreeNode, nodeList As List(Of TreeNode))
        nodeList.Add(node)
        For Each child As TreeNode In node.Nodes
            GetAllNodesRecursive(child, nodeList)
        Next
    End Sub

    ' Get currently selected nodes
    Public Function GetSelectedNodes() As List(Of TreeNode)
        Return New List(Of TreeNode)(selectedNodes)
    End Function

    ' Bulk operations on selected nodes
    ' Fixed CheckSelectedNodes - properly update the selected nodes themselves
    Public Sub CheckSelectedNodes(checkState As Boolean)
        isUpdatingNodeStates = True
        Try
            For Each node As TreeNode In selectedNodes
                Dim dirNode As DirectoryTreeNode = TryCast(node, DirectoryTreeNode)
                If dirNode IsNot Nothing Then
                    ' FIRST: Set the selected node's check state
                    dirNode.Checked = checkState

                    ' SECOND: If it's a folder, update all children
                    If dirNode.IsFolder Then
                        SetChildrenState(dirNode, checkState)
                    End If

                    ' THIRD: Update the selected node's appearance
                    UpdateNodeAppearance(dirNode)

                    ' FOURTH: Update parent folders
                    UpdateParentFolderStates(dirNode)
                Else
                    ' For regular TreeNode (shouldn't happen with our DirectoryTreeNode, but just in case)
                    node.Checked = checkState
                End If
            Next
        Finally
            isUpdatingNodeStates = False
        End Try
    End Sub
End Module