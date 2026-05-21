Imports System.Windows.Forms.ListView

#Region "###### USAGE INSTRUCTIONS ######"
' Edit only column 1 (second column)
'UniversalListViewEditor.EditListViewItems(lvwSongs, Me, New List(Of Integer) From {1}) '

' Edit only columns 0 and 2 (first and third columns)
'UniversalListViewEditor.EditListViewItems(lvwConfig, Me, New List(Of Integer) From {0, 2})

' Edit all columns (same as before)
'UniversalListViewEditor.EditListViewItems(lvwJINGLES, Me)

' Edit only the last column
'Dim lastColumnIndex = lvwSongs.Columns.Count - 1
'UniversalListViewEditor.EditListViewItems(lvwSongs, Me, New List(Of Integer) From {lastColumnIndex})
#End Region

Public Class AddEditForm
    Public Enum EditorMode
        Add
        Edit
        MultiEdit ' New mode for multiple item editing
    End Enum

    Public Mode As EditorMode
    Public TargetListView As ListView
    Public TargetItems As List(Of ListViewItem) ' Store multiple selected items
    Public TargetIndex As Integer = -1 ' Only used for single Edit mode

    Private btnSave As Button
    Private btnCancel As Button

    ' Universal method to load ListView data for single or multiple items
    ' Universal method to load ListView data for single or multiple items
    Public Sub LoadFromListView(columns As ColumnHeaderCollection, Optional itemsToEdit As List(Of ListViewItem) = Nothing, Optional editableColumns As List(Of Integer) = Nothing)
        Me.Controls.Clear()
        Dim marginX = 20
        Dim currentY = 20
        Dim inputWidth = Me.ClientSize.Width - marginX * 2 - 10

        ' Store the items for later use
        If itemsToEdit IsNot Nothing Then
            TargetItems = itemsToEdit
            If itemsToEdit.Count > 1 Then
                Mode = EditorMode.MultiEdit
                Me.Text = $"Edit {itemsToEdit.Count} Items"
            Else
                Mode = EditorMode.Edit
                Me.Text = "Edit Item"
            End If
        Else
            Mode = EditorMode.Add
            Me.Text = "Add New Item"
        End If

        ' Add artwork display if editing single item with image
        Dim artworkPictureBox As PictureBox = Nothing
        If itemsToEdit IsNot Nothing AndAlso itemsToEdit.Count = 1 Then
            Dim firstItem = itemsToEdit(0)

            ' Check if item has an image
            If firstItem.ImageKey IsNot Nothing AndAlso Not String.IsNullOrEmpty(firstItem.ImageKey) Then
                Dim imageList = TargetListView.SmallImageList
                If imageList IsNot Nothing AndAlso imageList.Images.ContainsKey(firstItem.ImageKey) Then
                    Dim artworkImage = imageList.Images(firstItem.ImageKey)

                    ' Create PictureBox for artwork
                    artworkPictureBox = New PictureBox With {
                    .Image = artworkImage,
                    .SizeMode = PictureBoxSizeMode.Zoom,
                    .Width = 120,
                    .Height = 120,
                    .Location = New Point(Me.ClientSize.Width - 140, marginX),
                    .BorderStyle = BorderStyle.FixedSingle,
                    .BackColor = Color.Black
                }
                    Me.Controls.Add(artworkPictureBox)

                    ' Adjust input width to make room for artwork
                    inputWidth = Me.ClientSize.Width - 160 - marginX * 2
                End If
            End If
        End If

        ' Create input fields for each column
        For i = 0 To columns.Count - 1
            ' Check if this column should be editable
            Dim isEditable As Boolean = True
            If editableColumns IsNot Nothing Then
                isEditable = editableColumns.Contains(i)
            End If

            Dim lbl As New Label With {
            .Text = columns(i).Text,
            .Location = New Point(marginX, currentY),
            .Width = inputWidth,
            .Font = New Font("Segoe UI", 9, FontStyle.Bold)
        }

            ' Gray out label for non-editable columns
            If Not isEditable Then
                lbl.ForeColor = Color.Gray
            End If

            Me.Controls.Add(lbl)
            currentY += lbl.Height + 2

            ' Check if this is the URL column (column 6)
            Dim isUrlColumn As Boolean = (columns(i).Text.ToLower().Contains("url"))

            If isUrlColumn AndAlso itemsToEdit IsNot Nothing Then
                ' Create a panel to hold TextBox and Link button
                Dim urlPanel As New Panel With {
        .Name = $"panelField{i}",
        .Width = inputWidth,
        .Height = 30,
        .Location = New Point(marginX, currentY)
    }

                ' Create TextBox for URL editing
                Dim txt As New TextBox With {
        .Name = $"txtField{i}",
        .Width = inputWidth - 80,
        .Location = New Point(0, 3),
        .ReadOnly = Not isEditable
    }

                ' Set visual style for read-only fields
                If Not isEditable Then
                    txt.BackColor = Color.LightGray
                    txt.ForeColor = Color.DarkGray
                End If

                ' Get URL value
                Dim urlValue As String = ""
                If itemsToEdit IsNot Nothing Then
                    urlValue = GetCommonValue(itemsToEdit, i)
                    txt.Text = urlValue

                    ' Add visual indicator for multi-edit mode
                    If Mode = EditorMode.MultiEdit AndAlso String.IsNullOrEmpty(urlValue) AndAlso isEditable Then
                        txt.PlaceholderText = "<Multiple Values>"
                        txt.ForeColor = Color.Gray
                    End If
                End If

                ' Create "Open" button
                Dim btnOpen As New Button With {
        .Text = "🔗 Open",
        .Width = 75,
        .Height = 24,
        .Location = New Point(inputWidth - 75, 2),
        .FlatStyle = FlatStyle.System
    }

                ' Add click handler to open URL
                AddHandler btnOpen.Click, Sub(sender As Object, e As EventArgs)
                                              Dim url = txt.Text.Trim()
                                              If Not String.IsNullOrEmpty(url) Then
                                                  Try
                                                      Process.Start(New ProcessStartInfo(url) With {.UseShellExecute = True})
                                                  Catch ex As Exception
                                                      MessageBox.Show($"Could not open URL: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                                  End Try
                                              Else
                                                  MessageBox.Show("URL is empty", "No URL", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                              End If
                                          End Sub

                ' Add context menu for URL field
                If isEditable Then
                    Dim contextMenu As New ContextMenuStrip()

                    Dim openUrlItem As New ToolStripMenuItem("Open in Browser")
                    AddHandler openUrlItem.Click, Sub()
                                                      Dim url = txt.Text.Trim()
                                                      If Not String.IsNullOrEmpty(url) Then
                                                          Try
                                                              Process.Start(New ProcessStartInfo(url) With {.UseShellExecute = True})
                                                          Catch ex As Exception
                                                              MessageBox.Show($"Could not open URL: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                                          End Try
                                                      End If
                                                  End Sub
                    contextMenu.Items.Add(openUrlItem)

                    Dim copyUrlItem As New ToolStripMenuItem("Copy URL")
                    AddHandler copyUrlItem.Click, Sub()
                                                      If Not String.IsNullOrEmpty(txt.Text) Then
                                                          Clipboard.SetText(txt.Text)
                                                      End If
                                                  End Sub
                    contextMenu.Items.Add(copyUrlItem)

                    contextMenu.Items.Add(New ToolStripSeparator())

                    Dim clearItem As New ToolStripMenuItem("Clear URL")
                    AddHandler clearItem.Click, Sub()
                                                    txt.Text = ""
                                                    txt.ForeColor = SystemColors.WindowText
                                                    txt.Tag = "cleared"
                                                End Sub
                    contextMenu.Items.Add(clearItem)

                    contextMenu.Items.Add(New ToolStripSeparator())

                    Dim selectAllItem As New ToolStripMenuItem("Select All")
                    AddHandler selectAllItem.Click, Sub() txt.SelectAll()
                    contextMenu.Items.Add(selectAllItem)

                    txt.ContextMenuStrip = contextMenu

                    ' Add keyboard shortcut for clearing (Ctrl+Delete)
                    AddHandler txt.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                                If e.Control AndAlso e.KeyCode = Keys.Delete Then
                                                    txt.Text = ""
                                                    txt.ForeColor = SystemColors.WindowText
                                                    txt.Tag = "cleared"
                                                    e.Handled = True
                                                End If
                                            End Sub
                End If

                ' Add controls to panel
                urlPanel.Controls.Add(txt)
                urlPanel.Controls.Add(btnOpen)

                Me.Controls.Add(urlPanel)
                currentY += urlPanel.Height + 10
            Else
                ' Regular TextBox for non-URL columns
                Dim txt As New TextBox With {
                .Name = $"txtField{i}",
                .Width = inputWidth,
                .Location = New Point(marginX, currentY),
                .ReadOnly = Not isEditable
            }

                ' Set visual style for read-only fields
                If Not isEditable Then
                    txt.BackColor = Color.LightGray
                    txt.ForeColor = Color.DarkGray
                End If

                ' Determine what value to show in the textbox
                If itemsToEdit IsNot Nothing Then
                    Dim commonValue As String = GetCommonValue(itemsToEdit, i)
                    txt.Text = commonValue

                    ' Add visual indicator for multi-edit mode (only for editable fields)
                    If Mode = EditorMode.MultiEdit AndAlso String.IsNullOrEmpty(commonValue) AndAlso isEditable Then
                        txt.PlaceholderText = "<Multiple Values>"
                        txt.ForeColor = Color.Gray
                    End If
                End If

                ' Add context menu for editable fields to allow clearing
                If isEditable Then
                    Dim contextMenu As New ContextMenuStrip()
                    Dim clearItem As New ToolStripMenuItem("Clear Value")
                    AddHandler clearItem.Click, Sub()
                                                    txt.Text = ""
                                                    txt.ForeColor = SystemColors.WindowText
                                                    txt.Tag = "cleared" ' Mark as explicitly cleared
                                                End Sub
                    contextMenu.Items.Add(clearItem)

                    ' Add separator and additional options
                    contextMenu.Items.Add(New ToolStripSeparator())

                    Dim selectAllItem As New ToolStripMenuItem("Select All")
                    AddHandler selectAllItem.Click, Sub() txt.SelectAll()
                    contextMenu.Items.Add(selectAllItem)

                    txt.ContextMenuStrip = contextMenu

                    ' Add keyboard shortcut for clearing (Ctrl+Delete)
                    AddHandler txt.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                                If e.Control AndAlso e.KeyCode = Keys.Delete Then
                                                    txt.Text = ""
                                                    txt.ForeColor = SystemColors.WindowText
                                                    txt.Tag = "cleared"
                                                    e.Handled = True
                                                End If
                                            End Sub
                End If

                Me.Controls.Add(txt)
                currentY += txt.Height + 10
            End If
        Next

        ' Add multi-edit information label
        If Mode = EditorMode.MultiEdit Then
            Dim infoLabel As New Label With {
            .Text = "Note: Empty fields will keep existing values. Only modified fields will be updated." & vbCrLf & "Right-click on fields to clear values or use Ctrl+Delete.",
            .Location = New Point(marginX, currentY),
            .Width = inputWidth,
            .Height = 40,
            .ForeColor = Color.Blue,
            .Font = New Font("Segoe UI", 8, FontStyle.Italic)
        }
            Me.Controls.Add(infoLabel)
            currentY += infoLabel.Height + 10
        End If

        ' Create Save and Cancel buttons
        btnSave = New Button With {
        .Text = "Save",
        .Width = 90,
        .Location = New Point(Me.ClientSize.Width \ 2 - 100, currentY),
        .DialogResult = DialogResult.OK
    }
        AddHandler btnSave.Click, AddressOf BtnSave_Click
        Me.AcceptButton = btnSave
        Me.Controls.Add(btnSave)

        btnCancel = New Button With {
        .Text = "Cancel",
        .Width = 90,
        .Location = New Point(Me.ClientSize.Width \ 2 + 10, currentY),
        .DialogResult = DialogResult.Cancel
    }
        Me.CancelButton = btnCancel
        Me.Controls.Add(btnCancel)

        Me.Height = btnCancel.Bottom + 50
    End Sub

    ' Get common value across multiple items for a specific column
    Private Function GetCommonValue(items As List(Of ListViewItem), columnIndex As Integer) As String
        If items Is Nothing OrElse items.Count = 0 Then Return ""

        Dim firstValue As String = GetItemValue(items(0), columnIndex)

        ' Check if all items have the same value
        For Each item In items
            If GetItemValue(item, columnIndex) <> firstValue Then
                Return "" ' Return empty string if values differ
            End If
        Next

        Return firstValue
    End Function

    ' Safely get value from ListViewItem at specific column index
    Private Function GetItemValue(item As ListViewItem, columnIndex As Integer) As String
        If columnIndex = 0 Then
            Return item.Text
        ElseIf columnIndex < item.SubItems.Count Then
            Return item.SubItems(columnIndex).Text
        Else
            Return ""
        End If
    End Function

    ' Safely set value to ListViewItem at specific column index
    Public Sub SetItemValue(item As ListViewItem, columnIndex As Integer, value As String)
        If columnIndex = 0 Then
            item.Text = value
        Else
            ' Ensure we have enough SubItems
            While item.SubItems.Count <= columnIndex
                item.SubItems.Add("")
            End While

            ' Preserve the Tag when updating (especially for Link-Title column)
            Dim existingTag As Object = item.SubItems(columnIndex).Tag
            item.SubItems(columnIndex).Text = value

            ' Restore Tag if it existed (don't overwrite original data)
            If existingTag IsNot Nothing Then
                item.SubItems(columnIndex).Tag = existingTag
            End If
        End If
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As EventArgs)
        ' Validation for Add mode only
        If Mode = EditorMode.Add Then
            For Each txt In Me.Controls.OfType(Of TextBox)()
                If String.IsNullOrWhiteSpace(txt.Text) Then
                    MessageBox.Show("All fields must be filled.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Exit Sub
                End If
            Next
        End If

        ' Everything looks good → allow the form to close
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    ' Get the modified values from the form
    Public Function GetModifiedValues() As Dictionary(Of Integer, String)
        Dim modifiedValues As New Dictionary(Of Integer, String)

        ' Helper function to find all TextBox controls recursively (including those in Panels)
        Dim FindAllTextBoxes As Func(Of Control.ControlCollection, List(Of TextBox)) = Nothing
        FindAllTextBoxes = Function(controls As Control.ControlCollection) As List(Of TextBox)
                               Dim result As New List(Of TextBox)
                               For Each ctrl As Control In controls
                                   If TypeOf ctrl Is TextBox Then
                                       result.Add(DirectCast(ctrl, TextBox))
                                   ElseIf ctrl.HasChildren Then
                                       result.AddRange(FindAllTextBoxes(ctrl.Controls))
                                   End If
                               Next
                               Return result
                           End Function

        ' Get all TextBox controls (including those in Panels)
        Dim textBoxes = FindAllTextBoxes(Me.Controls) _
        .Where(Function(t) t.Name.StartsWith("txtField")) _
        .OrderBy(Function(t) Integer.Parse(t.Name.Replace("txtField", ""))) _
        .ToList()

        For Each txt In textBoxes
            ' Get the actual column index from the textbox name
            Dim columnIndex As Integer = Integer.Parse(txt.Name.Replace("txtField", ""))

            ' Skip read-only fields
            If txt.ReadOnly Then Continue For

            ' In multi-edit mode, include empty values if they were explicitly cleared
            If Mode = EditorMode.MultiEdit Then
                ' Check if field was modified (either has value or was explicitly cleared)
                If Not String.IsNullOrEmpty(txt.Text) OrElse txt.Modified OrElse txt.Tag?.ToString() = "cleared" Then
                    modifiedValues.Add(columnIndex, txt.Text)
                End If
            Else
                modifiedValues.Add(columnIndex, txt.Text)
            End If
        Next

        Return modifiedValues
    End Function

    Private Sub AddEditForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Form load event handler
    End Sub
End Class

' Universal ListView Editor Module
Public Module UniversalListViewEditor

    ''' <summary>
    ''' Universal method to edit ListView items - handles single or multiple selection
    ''' </summary>
    ''' <param name="targetListView">The ListView to edit</param>
    ''' <param name="parentForm">Parent form for proper dialog positioning</param>
    ''' <param name="editableColumns">List of column indices that can be edited (Nothing = all columns editable)</param>
    ''' <returns>True if items were modified, False if cancelled</returns>
    Public Function EditListViewItems(targetListView As ListView, Optional parentForm As Form = Nothing, Optional editableColumns As List(Of Integer) = Nothing) As Boolean
        ' Check if any items are selected
        If targetListView.SelectedItems.Count = 0 Then
            MessageBox.Show("Please select one or more items to edit.", "Edit Items", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return False
        End If

        ' Get selected items
        Dim selectedItems As New List(Of ListViewItem)
        For Each item As ListViewItem In targetListView.SelectedItems
            selectedItems.Add(item)
        Next

        ' Create and configure the edit form
        Dim frm As New AddEditForm()
        If parentForm IsNot Nothing Then
            frm.StartPosition = FormStartPosition.CenterParent
        End If

        frm.TargetListView = targetListView
        frm.LoadFromListView(targetListView.Columns, selectedItems, editableColumns)

        ' Show the dialog and process results
        If frm.ShowDialog(parentForm) = DialogResult.OK Then
            Dim modifiedValues = frm.GetModifiedValues()

            ' Apply changes to all selected items
            For Each item In selectedItems
                For Each kvp In modifiedValues
                    frm.SetItemValue(item, kvp.Key, kvp.Value)
                Next
            Next

            Return True
        End If

        Return False
    End Function

    ''' <summary>
    ''' Add new item to ListView
    ''' </summary>
    ''' <param name="targetListView">The ListView to add item to</param>
    ''' <param name="parentForm">Parent form for proper dialog positioning</param>
    ''' <param name="editableColumns">List of column indices that can be edited (Nothing = all columns editable)</param>
    ''' <returns>True if item was added, False if cancelled</returns>
    Public Function AddListViewItem(targetListView As ListView, Optional parentForm As Form = Nothing, Optional editableColumns As List(Of Integer) = Nothing) As Boolean
        Dim frm As New AddEditForm()
        If parentForm IsNot Nothing Then
            frm.StartPosition = FormStartPosition.CenterParent
        End If

        frm.TargetListView = targetListView
        frm.LoadFromListView(targetListView.Columns, Nothing, editableColumns)

        If frm.ShowDialog(parentForm) = DialogResult.OK Then
            Dim values = frm.GetModifiedValues()

            ' Create new item
            Dim newItem As ListViewItem
            If values.ContainsKey(0) Then
                newItem = New ListViewItem(values(0))
            Else
                newItem = New ListViewItem("")
            End If

            ' Add subitems
            For i = 1 To targetListView.Columns.Count - 1
                If values.ContainsKey(i) Then
                    newItem.SubItems.Add(values(i))
                Else
                    newItem.SubItems.Add("")
                End If
            Next

            targetListView.Items.Add(newItem)
            Return True
        End If

        Return False
    End Function
End Module