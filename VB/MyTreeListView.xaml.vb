﻿Imports Microsoft.VisualBasic
Imports DevExpress.Xpf.Data
Imports DevExpress.Xpf.Grid
Imports DevExpress.Xpf.Grid.Native
Imports DevExpress.Xpf.Grid.TreeList
Imports System
Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Globalization
Imports System.Linq
Imports System.Reflection
Imports System.Text
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Documents
Imports System.Windows.Input
Imports System.Windows.Markup
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Windows.Navigation
Imports System.Windows.Shapes

Namespace DXTreeListSample
	Partial Public Class MyTreeListView
		Inherits TreeListView
		Public Sub New()
			InitializeComponent()
		End Sub
		Protected Overrides Function CreateDataProvider() As TreeListDataProvider
			Return New MyTreeListDataProvider(Me)
		End Function
		Public ReadOnly Property PublicDataProvider() As MyTreeListDataProvider
			Get
				Return CType(DataProviderBase, MyTreeListDataProvider)
			End Get
		End Property
		Public Event NodeSummaryUpdated As EventHandler(Of EventArgs)
		Public Sub RaiseNodeSummaryUpdated()
			RaiseEvent NodeSummaryUpdated(Me, New EventArgs())
		End Sub
	End Class
	Public Class MyTreeListDataProvider
		Inherits TreeListDataProvider
		Public Sub New(ByVal view As TreeListView)
			MyBase.New(view)
		End Sub
        Protected Overrides Sub CalcSummary(ByVal summary As IEnumerable(Of SummaryItemBase))
            MyBase.CalcSummary(summary)
            For Each node As TreeListNode In SummaryData.Keys
                Dim collection = New List(Of TreeListSummaryValue)()
                Dim summaryItem As SummaryItem = SummaryData(node)
                For Each summaryValue As TreeListSummaryValue In summaryItem.Values
                    collection.Add(summaryValue)
                Next summaryValue
                node.Tag = collection
            Next node
            CType(View, MyTreeListView).RaiseNodeSummaryUpdated()
        End Sub
	End Class
	Public Class GroupFooter
		Inherits Control
        Public Shared ReadOnly RowHandleProperty As DependencyProperty = DependencyProperty.Register("RowHandle", GetType(Integer), GetType(GroupFooter), New PropertyMetadata(-1, AddressOf OnRowHandlePropertyChanged))

		Private Shared Sub OnRowHandlePropertyChanged(ByVal d As DependencyObject, ByVal e As DependencyPropertyChangedEventArgs)
			CType(d, GroupFooter).OnRowHandleChanged(e)
		End Sub

		Public Sub New()
			DefaultStyleKey = GetType(GroupFooter)
			AddHandler DataContextChanged, AddressOf OnDataContextChanged
		End Sub

		#Region "Properties"
		Public Property RowHandle() As Integer
			Get
				Return CInt(Fix(GetValue(RowHandleProperty)))
			End Get
			Set(ByVal value As Integer)
				SetValue(RowHandleProperty, value)
			End Set
		End Property
		Public ReadOnly Property View() As MyTreeListView
			Get
				Return If(DataContext Is Nothing, Nothing, CType((CType(DataContext, RowData)).View, MyTreeListView))
			End Get
		End Property
		Public ReadOnly Property Grid() As TreeListControl
			Get
				Return CType(View.DataControl, TreeListControl)
			End Get
		End Property
		Public ReadOnly Property RowData() As TreeListRowData
			Get
				Return CType(DataContext, TreeListRowData)
			End Get
		End Property
		Private SummaryItemInfo As PropertyInfo = GetType(TreeListSummaryValue).GetProperty("SummaryItem", BindingFlags.Instance Or BindingFlags.NonPublic)
		#End Region

		Private Sub OnRowHandleChanged(ByVal e As DependencyPropertyChangedEventArgs)
			RefreshContent()
		End Sub
		Private Sub OnDataContextChanged(ByVal sender As Object, ByVal e As DependencyPropertyChangedEventArgs)
			AddHandler View.NodeSummaryUpdated, AddressOf OnViewNodeSummaryIsUpdated
			AddHandler Grid.Columns.CollectionChanged, AddressOf OnColumnsCollectionChanged
			SubscribeColumns()
		End Sub
		Private d As DependencyPropertyDescriptor = DependencyPropertyDescriptor.FromProperty(ColumnBase.VisibleProperty, GetType(ColumnBase))
		Private Sub OnColumnsCollectionChanged(ByVal sender As Object, ByVal e As System.Collections.Specialized.NotifyCollectionChangedEventArgs)
			RefreshContent()
			SubscribeColumns()
		End Sub
		Private Sub OnColumnVisibleChanged(ByVal sender As Object, ByVal e As EventArgs)
			RefreshContent()
		End Sub
		Private Sub OnViewNodeSummaryIsUpdated(ByVal sender As Object, ByVal e As EventArgs)
			RefreshContent()
		End Sub

		Private Sub SubscribeColumns()
			For Each column In Grid.Columns
				d.RemoveValueChanged(column, AddressOf OnColumnVisibleChanged)
				d.AddValueChanged(column, AddressOf OnColumnVisibleChanged)
			Next column
		End Sub
		Public Sub RefreshContent()
			If View Is Nothing Then
				Return
			End If
			Dim parent As TreeListNode = View.GetNodeByRowHandle(RowHandle).ParentNode
			If parent Is Nothing Then
				UpdateVisibility(-1)
			Else
				UpdateVisibility(parent.RowHandle)
				If Visibility = Visibility.Visible Then
					UpdateGroupFooterSummaryContent(parent.RowHandle)
				End If
			End If
		End Sub
		Private Sub UpdateGroupFooterSummaryContent(ByVal groupRowHandle As Integer)
			Dim summaryItems As List(Of TreeListSummaryValue) = TryCast(RowData.Node.ParentNode.Tag, List(Of TreeListSummaryValue))
			If summaryItems Is Nothing Then
				Return
			End If
			Dim summaryValues As New Dictionary(Of ColumnBase, String)()
			For i As Integer = 0 To View.VisibleColumns.Count - 1
				summaryValues.Add(View.VisibleColumns(i), String.Empty)
			Next i
			For Each value In summaryItems
				Dim item As TreeListSummaryItem = TryCast(SummaryItemInfo.GetValue(value, Nothing), TreeListSummaryItem)
				If (Not item.Visible) Then
					Continue For
				End If
				Dim col As TreeListColumn = Grid.Columns(item.FieldName)
				If (Not summaryValues.ContainsKey(col)) Then
					Continue For
				End If
				If (Not String.IsNullOrEmpty(summaryValues(col))) Then
                    summaryValues(Grid.Columns(item.FieldName)) += Microsoft.VisualBasic.Constants.vbLf
				End If
				summaryValues(Grid.Columns(item.FieldName)) += item.SummaryType & " = " & value.Value
			Next value
			Tag = summaryValues
		End Sub
		Private Sub UpdateVisibility(ByVal groupRowHandle As Integer)
			If groupRowHandle = -1 Then
				Visibility = Visibility.Collapsed
			Else
				Visibility = If(IsLastRowInGroup(groupRowHandle), Visibility.Visible, Visibility.Collapsed)
			End If
		End Sub
		Public Function IsLastRowInGroup(ByVal groupRowHandle As Integer) As Boolean
			Dim node As TreeListNode = View.GetNodeByRowHandle(groupRowHandle)
			Dim maxHandle As Integer = 0
			For i As Integer = 0 To node.Nodes.Count - 1
				Dim n As TreeListNode = node.Nodes(i)
				If n.RowHandle > maxHandle Then
					maxHandle = n.RowHandle
				End If
			Next i
			Return RowHandle = maxHandle
		End Function
	End Class
	Public Class Conv
		Inherits MarkupExtension
		Implements IValueConverter
		Public Function Convert(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.Convert
			Return If(TypeOf value Is String AndAlso (Not String.IsNullOrEmpty(CStr(value))), Visibility.Visible, Visibility.Hidden)
		End Function
		Public Function ConvertBack(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
			Throw New NotImplementedException()
		End Function
		Public Overrides Function ProvideValue(ByVal serviceProvider As IServiceProvider) As Object
			Return Me
		End Function
	End Class
End Namespace