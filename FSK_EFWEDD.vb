''' <summary>
'''   $Header: /EarthSoft/EDP/Formats/HaleyAldrich_EFWEDD/HaleyAldrich_EFWEDD.vb   2   2010-04-23 10:43:43-06:00   bryce.mathews $
'''		$UTCDate: 2010-04-23 16:43:43Z $
''' </summary>
Option Strict Off

Imports EarthSoft.Common
Imports EarthSoft.Edp
Imports System
Imports System.Collections
Imports System.Runtime.InteropServices
Imports System.Reflection

<Assembly: AssemblyCompany("EarthSoft, Inc.")>
<Assembly: AssemblyProduct("EQuIS 5")>
<Assembly: AssemblyCopyright("Copyright © 2002-2010, EarthSoft, Inc.")>
<Assembly: AssemblyTrademark("")>
<Assembly: AssemblyVersion("2.05.2")>

''' <summary>
'''     This is the parent class that will handle the EFWEDD (4-File) format
''' </summary>
'''
Public Class FSK_EDDHandler
  Inherits EarthSoft.EDP.EddCustomHandler
  Private _OpenDialog As Object
  Private pkg As EddPackage

  ''' <summary>We use these tables for several checks, so we'll just keep references.</summary>
  'Private EFW2FSample As EarthSoft.EDP.EddTable
  Private EFW2LabSMP As EarthSoft.EDP.EddTable
  Private EFW2LabRES As EarthSoft.EDP.EddTable
  Private EFW2LabTST As EarthSoft.EDP.EddTable
  Private FieldSampleKey As EarthSoft.EDP.EddTable  'added by DH 20160914

  ''' <summary>This are used for checking for child rows</summary>
  Private Smp_Res As EarthSoft.EDP.EddRelation
  Private FSample_Tst As EarthSoft.EDP.EddRelation
  Private LabSMP_Tst As EarthSoft.EDP.EddRelation

  ''' <summary>Several checks lookup the sample for a given test.  Instead of looking it up over, and over,
  ''' we will cache each lookup in this variable so we don't have to look it up again.</summary>
  Private sampleRow As System.Data.DataRow

  Private sampleTypeCode_for_ERR21 As New System.Collections.Specialized.StringCollection
  Private sampleTypeCode_for_ERR39 As New System.Collections.Specialized.StringCollection
  Private sampleTypeCode_for_DQM21 As New System.Collections.Specialized.StringCollection
  Private customError10Message As String
  ' common checks
  Private resultChecker As EarthSoft.EDP.Checks.Result

  Public Overrides Property Err() As EddError
    Get
      Return MyBase.Err
    End Get
    Set(ByVal Value As EddError)
      Me._Err = Value
      Me._Err.SetStatus(EddErrors.CustomError21, EddError.ErrorStatus.Warning)
      Me._Err.SetStatus(EddErrors.CustomError22, EddError.ErrorStatus.Warning)
      Me._Err.SetStatus(EddErrors.CustomError23, EddError.ErrorStatus.Warning)
    End Set
  End Property

  Public Sub New()
    ' call the base class constructor
    MyBase.New()


    'create stringcollection object for ERR24 and ERR39
    sampleTypeCode_for_ERR39.Add("TB")
    sampleTypeCode_for_ERR39.Add("N")
    sampleTypeCode_for_ERR39.Add("MB")
    sampleTypeCode_for_ERR39.Add("FD")
    sampleTypeCode_for_ERR39.Add("FB")
    sampleTypeCode_for_ERR39.Add("EB")
    sampleTypeCode_for_ERR39.Add("AB")

    'create stringcollection object for DQM01
    sampleTypeCode_for_DQM21.Add("N")
    sampleTypeCode_for_DQM21.Add("FD")
    sampleTypeCode_for_DQM21.Add("FR")
    sampleTypeCode_for_DQM21.Add("FS")
    sampleTypeCode_for_DQM21.Add("LR")
    sampleTypeCode_for_DQM21.Add("MS")
    sampleTypeCode_for_DQM21.Add("SD")
    sampleTypeCode_for_DQM21.Add("MSD")

    ' create the object to do the common result checks
    Me.resultChecker = New EarthSoft.EDP.Checks.Result(Me)

  End Sub

  Public Overrides Sub AddDataHandlers(ByRef Efd As EarthSoft.EDP.EddFormatDefinition)

    Me.FieldSampleKey = Efd.Tables.Item("FieldSampleKey")  'added by DH 20160914

    Me.EFW2LabRES = Efd.Tables.Item("EFW2LabRES")

    'Me.EFW2FSample = Efd.Tables.Item("EFW2FSample")

    Me.EFW2LabSMP = Efd.Tables.Item("EFW2LabSMP")
    Me.EFW2LabTST = Efd.Tables.Item("EFW2LabTST")
    'KP_Case9239_20060327
    Me.pkg = Efd.package


    AddHandler Me.FieldSampleKey.ColumnChanging, AddressOf Me.ColumnChanging
    AddHandler Me.FieldSampleKey.ColumnChanged, AddressOf Me.Check_FSK
    AddHandler Me.FieldSampleKey.BeforeDataLoad, AddressOf Me.BeforeDataLoad

    AddHandler Me.EFW2LabRES.ColumnChanged, AddressOf Me.Check_EFW2LabRES
    AddHandler Me.EFW2LabRES.BeforeDataLoad, AddressOf Me.BeforeDataLoad

    'AddHandler Me.EFW2FSample.ColumnChanging, AddressOf Me.ColumnChanging
    'AddHandler Me.EFW2FSample.ColumnChanged, AddressOf Me.Check_EFW2FSample
    'AddHandler Me.EFW2FSample.BeforeDataLoad, AddressOf Me.BeforeDataLoad

    AddHandler Me.EFW2LabSMP.ColumnChanging, AddressOf Me.ColumnChanging
    AddHandler Me.EFW2LabSMP.ColumnChanged, AddressOf Me.Check_EFW2LabSMP
    AddHandler Me.EFW2LabSMP.BeforeDataLoad, AddressOf Me.BeforeDataLoad

    AddHandler Me.EFW2LabTST.ColumnChanging, AddressOf Me.ColumnChanging
    AddHandler Me.EFW2LabTST.ColumnChanged, AddressOf Me.Check_EFW2LabTST
    AddHandler Me.EFW2LabTST.BeforeDataLoad, AddressOf Me.BeforeDataLoad

    'ERR20 will listen to RowChanged of EFW2LabSMP so it can check for child rows
    AddHandler Me.EFW2LabSMP.RowChanged, AddressOf Me.ERR20

    ' get the relations to find child rows
    Me.Smp_Res = CType(EFW2LabRES, EddTable).ParentRelations.Item("FK_EFW2LabRES_EFW2LabSMP")
    'Me.FSample_Tst = CType(EFW2LabTST, EddTable).ParentRelations.Item("FK_EFW2LabTST_EFW2FSample")
    Me.LabSMP_Tst = CType(EFW2LabTST, EddTable).ParentRelations.Item("FK_EFW2LabTST_EFW2LabSMP")

    If Efd.package.Tables.Contains("rt_sample_type") AndAlso Efd.package.Tables("rt_sample_type").Columns.Contains("needs_parent_sample") Then
      'VJN_Case_5282_20050525 Get the sample types that requires parent sample code.
      For Each dr As System.Data.DataRow In Efd.package.Tables("rt_sample_type").Select("needs_parent_sample='Y'")
        sampleTypeCode_for_ERR21.Add(Utilities.String.ToUpper(dr.Item("sample_type_code")))
        customError10Message = customError10Message & dr.Item("sample_type_code").ToString & ","
      Next
      'remove trailing comma
      customError10Message = customError10Message.Substring(0, customError10Message.Length - 1)
    End If
  End Sub


  'FB.11291: we need to clear the member variables before reloading data
  Private Sub BeforeDataLoad(ByVal eddTable As EarthSoft.EDP.EddTable)
    Me.sampleRow = Nothing
  End Sub

  Public Overloads Overrides Function ErrorMessage(ByVal err As EddErrors) As String
    Select Case err
      Case EddErrors.CustomError1
        Return "Percent_moisture cannot be null when sample matrix = SO or SE and sample type = N, FD, FR, FS, LR, MS, SD or MSD. (1)"
      Case EddErrors.CustomError2
        Return "Reportable_result cannot be 'Yes' where lab_qualifiers=E, G, P, or R. (2)"
      Case EddErrors.CustomError3
        Return "Datum_unit cannot be null if datum_value is not null. (3)"
      Case EddErrors.CustomError4
        Return "Sample must have related test/result. (20)"
      Case EddErrors.CustomError5
        Return "Reporting_detection_limit cannot be null when detect_flag=N. (5)"
      Case EddErrors.CustomError6
        Return "Result_value is required where detect_flag=Y and result_type_code=TRG, TIC. (6)"
      Case EddErrors.CustomError7
        Return "Cannot be less than the original concentration. (7)"
      Case EddErrors.CustomError8
        Return "Subsample_amount and subsample_amount_unit cannot be null when sample type = N, FD, FR, FS, LR, MS, SD or MSD. (8)"
      Case EddErrors.CustomError9
        Return "Date cannot precede sample_date. (9)"
      Case EddErrors.CustomError10
        Return "Parent_sample_code is required where sample_type_code= " & customError10Message & " (10)"
      Case EddErrors.CustomError11
        Return "Sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)"
      Case EddErrors.CustomError12
        Return "Start_depth cannot be null when sample_matrix_code=SO or SE. (12)"
      Case EddErrors.CustomError13
        Return "If start_depth is not null, end_depth must be greater than start_depth. (13)"
      Case EddErrors.CustomError14
        Return "Lab_name_code cannot be null when analysis_location=LB or FL. (14)"
      Case EddErrors.CustomError15
        Return "Lab_sample_id cannot be null when analysis_location=LB. (15)"
      Case EddErrors.CustomError16
        Return "Result_unit cannot be null when result_value is not null. (16)"
      Case EddErrors.CustomError17
        Return "Detection_limit_unit cannot be null when reporting_detection_limit is not null. (17)"
      Case EddErrors.CustomError18
        Return "Depth_unit cannot be null if start_depth is not null. (18)"
      Case EddErrors.CustomError19
        Return "Sample_time cannot be null when sample_type_code=TB, N, MB, FD, FB, EB, AB. (19)"
      Case EddErrors.CustomError20
        Return "If analysis_location=LB, sample_delivery_group, sent_to_lab_date, and sample_receipt_date cannot be null. (20)"
      Case EddErrors.CustomError21
        Return "Warning: sys_sample_code does not exist in the database."
      Case EddErrors.CustomError22
        Return "Warning: sys_sample_code already exists in the databse."
      Case EddErrors.CustomError23
        Return "Warning: total_or_dissolved should match dt_field_sample.filter_type in the databse."
    End Select

    Return String.Empty
  End Function

  Private _updateInfo As New UpdateInfo
  Private _performUpdate As Boolean = True
  Private _skipUpdate As Boolean = False
  Private GetChangingValue As Boolean = True
  Private Sub ColumnChanging(ByVal sender As Object, ByVal e As System.Data.DataColumnChangeEventArgs)
    'The column changing event gets called twice before the column changed event and the second time the value is already changed
    If Not TypeOf e.Column Is EddColumn OrElse Not GetChangingValue Then Return

    _updateInfo.columnName = e.Column.ColumnName
    _updateInfo.table = e.Row.Table
    _updateInfo.oldValue = e.Row.Item(e.Column.ColumnName).ToString
    _updateInfo.newValue = e.ProposedValue.ToString
    _performUpdate = True

    If IsPartOfPK(e.Row.Table.PrimaryKey, e.Column.ColumnName) Then
      If e.Row.IsNull(e.Column.ColumnName) OrElse e.Row.Item(e.Column.ColumnName).ToString.Trim.Length = 0 _
      OrElse e.ProposedValue Is DBNull.Value OrElse e.ProposedValue.ToString.Trim.Length = 0 Then

        _updateInfo.columnName = String.Empty
        _updateInfo.table = Nothing
        _updateInfo.oldValue = String.Empty
        _updateInfo.newValue = String.Empty
        _performUpdate = False
      End If
    End If

    GetChangingValue = False
  End Sub

  Private Function IsPartOfPK(ByVal primaryKey() As System.Data.DataColumn, ByVal columnName As String) As Boolean
    For Each key As System.Data.DataColumn In primaryKey
      If key.ColumnName = columnName Then Return True
    Next

    Return False
  End Function

  Private Function ShouldUpdate(ByVal e As System.Data.DataColumnChangeEventArgs) As Boolean
    If TypeOf e.Column Is EddColumn AndAlso _performUpdate AndAlso e.Column.ColumnName = _updateInfo.columnName AndAlso e.ProposedValue.ToString.ToUpper = _updateInfo.newValue.ToUpper _
    AndAlso Not _updateInfo.table Is Nothing AndAlso e.Row.Table.TableName = _updateInfo.table.TableName AndAlso _updateInfo.oldValue <> _updateInfo.newValue Then
      If IsPartOfPK(e.Row.Table.PrimaryKey, e.Column.ColumnName) Then
        If Not e.Row.IsNull(e.Column.ColumnName) AndAlso _updateInfo.newValue.Length > 0 AndAlso _updateInfo.oldValue.Length > 0 Then
          Return True
        Else
          Return False
        End If
      Else
        Return True
      End If

    Else
      Return False
    End If
  End Function

  Private Sub UpdateChildren(ByVal e As System.Data.DataColumnChangeEventArgs)
    _skipUpdate = True

    For Each relation As EddRelation In _updateInfo.table.ChildRelations
      If relation.ChildTable.TableName = e.Row.Table.TableName OrElse relation.ChildTable.Rows.Count = 0 OrElse Not relation.ChildTable.Columns.Contains(_updateInfo.columnName) Then Continue For

      Dim query As String
      If _updateInfo.oldValue = String.Empty Then
        query = String.Format("{0} is null", _updateInfo.columnName)
      Else
        query = String.Format("{0}='{1}'", _updateInfo.columnName, _updateInfo.oldValue)
      End If

      For Each key As System.Data.DataColumn In e.Row.Table.PrimaryKey
        If key.ColumnName <> _updateInfo.columnName Then
          If e.Row.IsNull(key.ColumnName) Then
            query += String.Format(" and {0} is null", key.ColumnName)
          Else
            query += String.Format(" and {0}='{1}'", key.ColumnName, e.Row.Item(key.ColumnName).ToString)
          End If
        End If

      Next

      Dim childRows() As System.Data.DataRow = relation.ChildTable.Select(query)
      For Each row As System.Data.DataRow In childRows
        row.Item(_updateInfo.columnName) = _updateInfo.newValue
      Next


    Next

    _skipUpdate = False
  End Sub
        Private Sub Check_FSK(ByVal sender As Object, ByVal e As System.Data.DataColumnChangeEventArgs)
        Select Case e.Column.ColumnName.ToLower
            Case "parent_sample_code"
                ERR21(e)         'ERR21: Parent_sample_code is required where sample_type_code=BD, FD, FR, FS, LR, MS, SD, or MSD. (10)
            Case "sample_type_code"
                ERR21(e)         'ERR21: Parent_sample_code is required where sample_type_code=BD, FD, FR, FS, LR, MS, SD, or MSD. (10)
                ERR24(e)                 'ERR24: sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)
            Case "sample_date"
                ERR24(e)                 'ERR24: sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)
        End Select
    End Sub

  Private Sub Check_EFW2LabRES(ByVal sender As Object, ByVal e As System.Data.DataColumnChangeEventArgs)
    'If _skipUpdate Then Return


    Select Case e.Column.ColumnName.ToLower
      Case "analysis_date"
        ERR08_09(e, e.Column.ColumnName, Me.Smp_Res)        'ERR08: Analysis_date cannot precede sample_date. (9)
        'KP_Case7617_20051031

        ' the following lines were commented out by jcm 12012005 until the logic can be defined more clearly
        ' Case "qc_spike_measured" -->
        'Me.resultChecker.qc_spike_measured_NotLessThan_qc_spike_added(e) 			 'qc_spike_measured cannot be less than qc_spike_added
        'KP_Case7617_20051031
        ' Case "qc_spike_added"
        'Me.resultChecker.qc_spike_measured_NotLessThan_qc_spike_added(e) 				'qc_spike_measured cannot be less than qc_spike_added
        'Case "qc_dup_spike_measured"
        'Me.resultChecker.qc_dup_spike_measured_NotLessThan_qc_dup_original_conc(e)				 'ERR17: qc_dup_spike_measured cannot be less than qc_dup_original_conc
        'Case "qc_dup_original_conc"
        'Me.resultChecker.qc_dup_spike_measured_NotLessThan_qc_dup_original_conc(e)				 'ERR17: qc_dup_spike_measured cannot be less than qc_dup_original_conc

      Case "result_value"
        ERR22(e)         'ERR22: Result_value is required where detect_flag='Y' and result_type_code=TRG, TIC. (6)
      Case "detect_flag"
        ERR22(e)         'ERR22: Result_value is required where detect_flag='Y' and result_type_code=TRG, TIC. (6)
        ERR23(e)         'ERR23: Reporting_detection_limit cannot be null when detect_flag=N. (5)
      Case "result_type_code"
        ERR22(e)          'ERR22: Result_value is required where detect_flag='Y' and result_type_code=TRG, TIC. (6)
      Case "reporting_detection_limit"
        ERR23(e)         'ERR23: Reporting_detection_limit cannot be null when detect_flag=N. (5)
        ERR30(e)         'ERR30: Detection_limit_unit cannot be null when reporting_detection_limit is not null. (17)
      Case "detection_limit_unit"
        ERR30(e)         'ERR30: Detection_limit_unit cannot be null when reporting_detection_limit is not null. (17)

        'FB.8555: do NOT check lab_qualifiers
        'Case "lab_qualifiers"
        '	resultChecker.VerifyQualifiers(e)
    End Select

  End Sub

  Private Sub Check_EFW2FSample(ByVal sender As Object, ByVal e As System.Data.DataColumnChangeEventArgs)
    GetChangingValue = True
    If ShouldUpdate(e) Then UpdateChildren(e)

    Select Case e.Column.ColumnName.ToLower
      Case "sample_type_code"
        ERR21(e)         'ERR21: Parent_sample_code is required where sample_type_code=BD, FD, FR, FS, LR, MS, SD, or MSD. (10)
        ERR24(e)         'ERR24: sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)

        ' 20071031-mjw: we cannot call these functions here because the flag the error in LabTST (not FSample)
        'DQM01(e, Me.FSample_Tst)        'DQM01: Percent_Moisture is required where sample_matrix_code=SO or SE and sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
        'DQM02(e, Me.FSample_Tst)        'DQM02: Subsample_amount is required where sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
      Case "sample_matrix_code"
        'DQM01(e, Me.FSample_Tst)        'DQM01: Percent_Moisture is required where sample_matrix_code=SO or SE and sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
      Case "parent_sample_code"
        ERR21(e)         'ERR21: Parent_sample_code is required where sample_type_code=BD, FD, FR, FS, LR, MS, SD, or MSD. (10)
      Case "sample_date"
        ERR24(e)         'ERR24: sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)
      Case "start_depth"
        ERR25(e)         'ERR25: Start_depth cannot be null when sample_matrix_code=SO or SE. (12)
        ERR26(e)         'ERR26: If start_depth is not null, end_depth must be greater than start_depth. (13)
      Case "end_depth"
        ERR26(e)         'ERR26: If start_depth is not null, end_depth must be greater than start_depth. (13)
    End Select

  End Sub

  Private Sub Check_EFW2LabSMP(ByVal sender As Object, ByVal e As System.Data.DataColumnChangeEventArgs)
    'If _skipUpdate Then Return
    GetChangingValue = True
    If ShouldUpdate(e) Then UpdateChildren(e)

    Select Case e.Column.ColumnName.ToLower
      Case "sys_sample_code"
        ERR01(e)         'Field samples should already exist in the database.
      Case "parent_sample_code"
        ERR01(e)         'Field samples should already exist in the database.
        ERR21(e)         'ERR21: Parent_sample_code is required where sample_type_code=BD, FD, FR, FS, LR, MS, SD, or MSD. (10)
      Case "sample_type_code"
        ERR01(e)         'Field samples should already exist in the database.
        ERR21(e)         'ERR21: Parent_sample_code is required where sample_type_code=BD, FD, FR, FS, LR, MS, SD, or MSD. (10)
        ERR24(e)                 'ERR24: sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)
      Case "sample_date"
        ERR01(e)         'Field samples should already exist in the database.
        ERR24(e)         'ERR24: sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)
      Case "sample_time"
        ERR01(e)         'Field samples should already exist in the database.
      Case "sample_matrix_code"
        ERR01(e)         'Field samples should already exist in the database.
      Case "sample_source"
        ERR01(e)         'Field samples should already exist in the database.
    End Select
  End Sub

  Private Sub Check_EFW2LabTST(ByVal sender As Object, ByVal e As System.Data.DataColumnChangeEventArgs)
    'If _skipUpdate Then Return
    GetChangingValue = True
    If ShouldUpdate(e) Then UpdateChildren(e)

    Select Case e.Column.ColumnName.ToLower
      Case "total_or_dissolved"
        ERR02(e)      'Total_or_dissolved should match dt_field_sample.filter_type in the databse.
      Case "sys_sample_code"
        ERR02(e)      'Total_or_dissolved should match dt_field_sample.filter_type in the databse.
      Case "prep_date"
        ERR08_09(e, e.Column.ColumnName, Me.FSample_Tst)         'ERR09: prep_date cannot precede sample_date. (9)
      Case "analysis_date"
        ERR08_09(e, e.Column.ColumnName, Me.FSample_Tst)         'ERR09: analysis_date cannot precede sample_date. (9)
      Case "percent_moisture"
        DQM01(e, Me.FSample_Tst)        'DQM01: Percent_Moisture is required where sample_matrix_code=SO or SE and sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
        DQM01(e, Me.LabSMP_Tst)
      Case "subsample_amount"
        DQM02(e, Me.FSample_Tst)        'DQM02: Subsample_amount is required where sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
        DQM02(e, Me.LabSMP_Tst)
        DQM03(e)         'DQM03: Subsample_amount_unit cannot be null when Subsample_amount is not null. (16)
      Case "subsample_amount_unit"
        DQM03(e)         'DQM03: Subsample_amount_unit cannot be null when Subsample_amount is not null. (16)
      Case "lab_name_code"
        'The element lab_name_code is a required field in EFW2LabTST  format . So the Checks ERR27 can be skipped
        'ERR27(e)            'ERR27: Lab_name_code cannot be null when analysis_location=LB or FL. (14)
      Case "analysis_location"
        'The element lab_name_code is a required field in EFW2LabTST  format . So the Checks ERR27 can be skipped
        'ERR27(e)            'ERR27: Lab_name_code cannot be null when analysis_location=LB or FL. (14)
        ERR28(e)         'ERR28: Lab_sample_id cannot be null when analysis_location=LB. (15)
      Case "lab_sample_id"
        ERR28(e)         'ERR28: Lab_sample_id cannot be null when analysis_location=LB. (15)
    End Select
  End Sub

  'KP_Case9239_20060323
  Public Function CreateFieldSample(ByVal eddRow As System.Data.DataRow) As Boolean
    'KP_Case9239_20060327
    Dim row() As System.Data.DataRow = Me.pkg.Tables.Item("rt_sample_type").Select("sample_type_code = '" & eddRow.Item("sample_type_code") & "'")

    If row(0).Item("sample_type_class") = "FQ" Or row(0).Item("sample_type_class") = "NF" Then
      Return True
    Else
      Return False
    End If
  End Function
  Public Function CreateLabSample(ByVal eddRow As System.Data.DataRow) As Boolean
    Dim row() As System.Data.DataRow = Me.pkg.Tables.Item("rt_sample_type").Select("sample_type_code = '" & eddRow.Item("sample_type_code") & "'")

    If row(0).Item("sample_type_class") = "LQ" Then
      Return True
    Else
      Return False
    End If
  End Function

#Region "CustomChecks"
  ''' <summary>Field samples should already exist in the database.
  ''' Applies to:
  ''' <list type="bullet">
  '''   <item>EFW2LabSMP.sys_sample_code</item>
  '''   <item>EFW2LabSMP.sample_source</item>
  '''   <item>EFW2LabSMP.sample_matrix_code</item>
  '''   <item>EFW2LabSMP.sample_type_code</item>
  '''   <item>EFW2LabSMP.sample_date</item>
  '''   <item>EFW2LabSMP.sample_time</item>
  '''   <item>EFW2LabSMP.parent_sample_code</item>
  ''' </list>
  ''' </summary>
  ''' <history>
  ''' <mod user="bhm" case="49512" date="3/17/2010" remarks="created"/>
  ''' </history>
  Private Sub ERR01(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row

      If Me.pkg.Connection Is Nothing OrElse .IsNull("sys_sample_code") 
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sys_sample_code"), EddErrors.CustomError21)
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_source"), EddErrors.CustomError21)
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_matrix_code"), EddErrors.CustomError21)
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_type_code"), EddErrors.CustomError21)
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_date"), EddErrors.CustomError21)
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_time"), EddErrors.CustomError21)
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("parent_sample_code"), EddErrors.CustomError21)
        'Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sys_sample_code"), EddErrors.CustomError22)
      Else
        Dim query As String = String.Format("select * from dt_sample where facility_id={0} and sys_sample_code='{1}'", Me.pkg.Connection.FacilityId, .Item("sys_sample_code").ToString.Replace("'", "''"))

        Dim ds As New System.Data.DataSet
        Me.pkg.Connection.Fill(ds, "dt_sample", query)

        Dim sampleDate As Date

        Select Case .Item("sample_source").ToString.ToUpper
          Case "FIELD"
            'Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sys_sample_code"), EddErrors.CustomError22)

            If ds.Tables.Item("dt_sample").Rows.Count > 0 Then
              Dim row As System.Data.DataRow = ds.Tables.Item("dt_sample").Rows(0)
              Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sys_sample_code"), EddErrors.CustomError21)

              If .Item("sample_source").ToString.ToLower = row.Item("sample_source").ToString.ToLower Then
                Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_source"), EddErrors.CustomError21)
              Else
                Me.AddError(e.Row, e.Row.Table.Columns.Item("sample_source"), EddErrors.CustomError21)
              End If

              If .Item("sample_matrix_code").ToString.ToLower = row.Item("matrix_code").ToString.ToLower Then
                Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_matrix_code"), EddErrors.CustomError21)
              Else
                Me.AddError(e.Row, e.Row.Table.Columns.Item("sample_matrix_code"), EddErrors.CustomError21)
              End If

              If .Item("sample_type_code").ToString.ToLower = row.Item("sample_type_code").ToString.ToLower Then
                Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_type_code"), EddErrors.CustomError21)
              Else
                Me.AddError(e.Row, e.Row.Table.Columns.Item("sample_type_code"), EddErrors.CustomError21)
              End If

              If .Item("parent_sample_code").ToString.ToLower = row.Item("parent_sample_code").ToString.ToLower Then
                Me.RemoveError(e.Row, e.Row.Table.Columns.Item("parent_sample_code"), EddErrors.CustomError21)
              Else
                Me.AddError(e.Row, e.Row.Table.Columns.Item("parent_sample_code"), EddErrors.CustomError21)
              End If


              If row.IsNull("sample_date") Then
                If .IsNull("sample_date") Then
                  Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_date"), EddErrors.CustomError21)
                Else
                  Me.AddError(e.Row, e.Row.Table.Columns.Item("sample_date"), EddErrors.CustomError21)
                End If
                If .IsNull("sample_time") Then
                  Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_time"), EddErrors.CustomError21)
                Else
                  Me.AddError(e.Row, e.Row.Table.Columns.Item("sample_time"), EddErrors.CustomError21)
                End If

              Else
                Try
                  Dim eddDate As Date = GetSampleDate(e.Row)
                  Dim dbDate As Date = Date.Parse(row.Item("sample_date"))

                  If eddDate.ToShortDateString = dbDate.ToShortDateString Then
                    Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_date"), EddErrors.CustomError21)
                  Else
                    Me.AddError(e.Row, e.Row.Table.Columns.Item("sample_date"), EddErrors.CustomError21)
                  End If

                  If eddDate.ToShortTimeString = dbDate.ToShortTimeString Then
                    Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_time"), EddErrors.CustomError21)
                  Else
                    Me.AddError(e.Row, e.Row.Table.Columns.Item("sample_time"), EddErrors.CustomError21)
                  End If

                Catch ex As Exception
                  Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_date"), EddErrors.CustomError21)
                  Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_time"), EddErrors.CustomError21)
                End Try
              End If

            Else
              Me.AddError(e.Row, e.Row.Table.Columns.Item("sys_sample_code"), EddErrors.CustomError21)
              Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_source"), EddErrors.CustomError21)
              Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_matrix_code"), EddErrors.CustomError21)
              Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_type_code"), EddErrors.CustomError21)
              Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_date"), EddErrors.CustomError21)
              Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_time"), EddErrors.CustomError21)
              Me.RemoveError(e.Row, e.Row.Table.Columns.Item("parent_sample_code"), EddErrors.CustomError21)
            End If

            'bhm_case49512_20100416: do not check lab samples
            'Case "LAB"
            '  Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sys_sample_code"), EddErrors.CustomError21)

            '  If ds.Tables.Item("dt_sample").Rows.Count > 0 Then
            '    Me.AddError(e.Row, e.Row.Table.Columns.Item("sys_sample_code"), EddErrors.CustomError22)
            '  Else
            '    Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sys_sample_code"), EddErrors.CustomError22)
            '  End If

          Case Else
            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sys_sample_code"), EddErrors.CustomError21)
            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_source"), EddErrors.CustomError21)
            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_matrix_code"), EddErrors.CustomError21)
            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_type_code"), EddErrors.CustomError21)
            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_date"), EddErrors.CustomError21)
            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sample_time"), EddErrors.CustomError21)
            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("parent_sample_code"), EddErrors.CustomError21)
            'Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sys_sample_code"), EddErrors.CustomError22)
        End Select
      End If
    End With
  End Sub

  'Store the filter types retreived from the database so we don't have to query the database all the time
  Private storedFilterTypes As New System.Collections.Generic.Dictionary(Of String, String)
  ''' <summary>Total_or_dissolved should match dt_field_sample.filter_type in the databse.
  ''' Applies to:
  ''' <list type="bullet">
  '''   <item>EFW2LabTST.sys_sample_code</item>
  '''   <item>EFW2LabTST.total_or_dissolved</item>
  ''' </list>
  ''' </summary>
  ''' <history>
  ''' <mod user="bhm" case="49512" date="3/18/2010" remarks="created"/>
  ''' </history>
  Private Sub ERR02(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      If Me.pkg.Connection Is Nothing OrElse .IsNull("sys_sample_code") OrElse .IsNull("total_or_dissolved") Then
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("total_or_dissolved"), EddErrors.CustomError23)

      Else
        Dim sysSampleCode As String = .Item("sys_sample_code").ToString
        Dim totalOrDissolved As String = .Item("total_or_dissolved").ToString.ToUpper

        'Check if we have already stored the filter type
        If storedFilterTypes.Count > 0 AndAlso storedFilterTypes.ContainsKey(.Item("sys_sample_code").ToString.ToUpper) Then
          If storedFilterTypes.Item(sysSampleCode.ToUpper) = totalOrDissolved Then
            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("total_or_dissolved"), EddErrors.CustomError23)
          Else
            Me.AddError(e.Row, e.Row.Table.Columns.Item("total_or_dissolved"), EddErrors.CustomError23)
          End If

          Return
        End If

        Dim query As String = String.Format("select filter_type from dt_field_sample fs inner join dt_sample s on fs.facility_id = s.facility_id and fs.sample_id = s.sample_id where s.facility_id = {0} and s.sys_sample_code='{1}'", Me.pkg.Connection.FacilityId, sysSampleCode.Replace("'", "''"))
        Dim ds As New System.Data.DataSet
        Me.pkg.Connection.Fill(ds, "dt_field_sample", query)
        If ds.Tables.Item("dt_field_sample").Rows.Count > 0 Then
          Dim filterType As String
          If ds.Tables.Item("dt_field_sample").Rows(0).Item("filter_type") Is DBNull.Value Then
            filterType = String.Empty
          Else
            filterType = ds.Tables.Item("dt_field_sample").Rows(0).Item("filter_type").ToString.ToUpper
          End If

          'Store the filter type
          If Not storedFilterTypes.ContainsKey(sysSampleCode.ToUpper) Then
            storedFilterTypes.Add(sysSampleCode.ToUpper, filterType)
          End If

          If filterType = totalOrDissolved Then
            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("sys_sample_code"), EddErrors.CustomError23)
          Else
            Me.AddError(e.Row, e.Row.Table.Columns.Item("sys_sample_code"), EddErrors.CustomError23)
          End If

        Else

        End If
      End If

    End With
  End Sub


  'ERR08: Analysis_date cannot precede sample_date. (9)
  'ERR09: prep_date cannot precede sample_date. (9)
  Friend Sub ERR08_09(ByVal e As System.Data.DataColumnChangeEventArgs, ByVal date_field As String, ByVal relation As EarthSoft.EDP.EddRelation)

    ' if for some reason we don't have sample rows, just exit
    If (relation Is Me.Smp_Res) AndAlso (EFW2LabSMP.Rows.Count <= 0) Then
      Return
      'ElseIf (relation Is Me.FSample_Tst) AndAlso (EFW2FSample.Rows.Count <= 0) Then
      'Return
    End If

    With e.Row
      Try
        ' do we need to lookup the sample row?
        If (Me.sampleRow Is Nothing) OrElse (Not Me.sampleRow.Item("sys_sample_code").ToString.Equals(.Item("sys_sample_code"))) Then
          ' use the relation to get the parent row for this sample
          Me.sampleRow = relation.GetParentRow(e.Row)
          ' make sure it found the row
          If Me.sampleRow Is Nothing Then Return
        End If

        ' make sure both dates are non-null then compare
        If (Not .Item(date_field) Is DBNull.Value) AndAlso (Not Me.sampleRow.Item("sample_date") Is DBNull.Value) AndAlso _
         (System.DateTime.Compare(CType(.Item(date_field), Date), CType(Me.sampleRow.Item("sample_date"), Date)) < 0) Then
          Me.AddError(e.Row, DirectCast(.Table.Columns.Item(date_field), System.Data.DataColumn), EarthSoft.EDP.EddErrors.CustomError9)
        Else
          Me.RemoveError(e.Row, .Table.Columns.Item(date_field), EarthSoft.EDP.EddErrors.CustomError9)
        End If

      Catch ex As Exception
        'if date conversion doesn't work, we don't want error because comparison is moot.  Just move on...
      End Try
    End With
  End Sub

  ''' <summary>This method will check the EFW2LabSMP Table to verify child rows.
  ''' Parent rows for test/results are checked by default (Because of the xs:keyref)</summary>
  Public Sub ERR20(ByVal sender As Object, ByVal e As System.Data.DataRowChangeEventArgs)
    ' NOTE: the relation names must exactly match the xs:keyref names in the *.xsd

    Try
      If Me.EFW2LabRES.Rows.Count = 0 Then
        ' if there are no records in either child table, then assume no results were loaded, so there is no error
        Me.RemoveError(e.Row, EddErrors.CustomError4)

      ElseIf Me.Smp_Res.GetChildRows(e.Row).Length > 0 Then
        ' if there is at least one child row in either table, then there is no error
        Me.RemoveError(e.Row, EddErrors.CustomError4)
      Else
        ' there are rows in at least one of the child tables, but neither table contains a matching row
        Me.AddError(e.Row, EddErrors.CustomError4)
      End If

    Catch ex As Exception
      ' EarthSoft.Shared.MsgBox.Show(ex.ToString)
    End Try

  End Sub

  ''' <summary>Parent_sample_code is required where sample_type_code=BD, FD, FR, FS, LR, MS, SD, or MSD.
  ''' Applies to:
  ''' <list type="bullet">
  '''   <item>EFW2FSample.sample_type_code</item>
  '''   <item>EFW2FSample.parent_sample_code</item>
  '''   <item>EFW2LabSMP.sample_type_code</item>
  '''   <item>EFW2LabSMP.parent_sample_code</item>
  ''' </list>
  ''' </summary>
  Friend Sub ERR21(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row

      'KP_Case_4492_20040909:'passing the value to a function to be converted to uppercase
      If .Item("parent_sample_code") Is DBNull.Value And sampleTypeCode_for_ERR21.Contains(Utilities.String.ToUpper(.Item("sample_type_code"))) Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("parent_sample_code"), EddErrors.CustomError10)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("parent_sample_code"), EddErrors.CustomError10)
      End If
    End With
  End Sub

  'VJN_20041028
  'ERR22: Result_value is required where detect_flag=Y and result_type_code=TRG, TIC. (6)
  Friend Sub ERR22(ByVal e As System.Data.DataColumnChangeEventArgs)

    With e.Row
      If Not .Item("detect_flag") Is DBNull.Value AndAlso Utilities.String.ToUpper(.Item("detect_flag")) = "Y" AndAlso _
      (Utilities.String.ToUpper(.Item("result_type_code")) = "TRG" OrElse Utilities.String.ToUpper(.Item("result_type_code")) = "TIC") AndAlso _
      .Item("result_value") Is DBNull.Value Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("detect_flag"), EddErrors.CustomError6)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("detect_flag"), EddErrors.CustomError6)
      End If
    End With

  End Sub

  'ERR23: Reporting_detection_limit cannot be null when detect_flag=N. (5)
  Friend Sub ERR23(ByVal e As System.Data.DataColumnChangeEventArgs)
    Dim rtc As String

    With e.Row
      rtc = Utilities.String.ToUpper(.Item("result_type_code"))
      If Utilities.String.ToUpper(.Item("detect_flag")) = "N" AndAlso (rtc = "TRG" OrElse rtc = "TIC" OrElse rtc = "SC") AndAlso .Item("reporting_detection_limit") Is DBNull.Value Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("detect_flag"), EddErrors.CustomError5)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("detect_flag"), EddErrors.CustomError5)
      End If
    End With
  End Sub

  'ERR24: sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)
  Friend Sub ERR24(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      'KP_Case_4492_20040909:'passing the value to a function to be converted to uppercase
      If .Item("sample_date") Is DBNull.Value AndAlso sampleTypeCode_for_ERR39.Contains(Utilities.String.ToUpper(.Item("sample_type_code"))) Then
        'Me.AddError(e.Row, DirectCast(.Table.Columns.Item("sample_date"), System.Data.DataColumn), EarthSoft.Edp.EddErrors.CustomError11)
        Me.AddError(e.Row, .Table.Columns.Item("sample_date"), EddErrors.CustomError11)
      Else
        Me.RemoveError(e.Row, .Table.Columns.Item("sample_date"), EddErrors.CustomError11)
      End If
    End With
  End Sub

  'ERR25: Start_depth cannot be null when sample_matrix_code=SO or SE. (12)
  Friend Sub ERR25(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      'KP_Case_4492_20040909:'passing the value to a function to be converted to uppercase
      If (Utilities.String.ToUpper(.Item("sample_matrix_code")) = "SO" Or Utilities.String.ToUpper(.Item("sample_matrix_code")) = "SE") And .Item("start_depth") Is DBNull.Value Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("start_depth"), EddErrors.CustomError12)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("start_depth"), EddErrors.CustomError12)
      End If
    End With
  End Sub

  'ERR26: If start_depth is not null, end_depth must be greater than start_depth. (13)
  Friend Sub ERR26(ByVal e As System.Data.DataColumnChangeEventArgs)
    Dim start_depth, end_depth As Double
    Dim provider As System.IFormatProvider = Nothing

    With e.Row
      ' Rekha_Case4562_20041015
      ' convert the end_depth and start_depth to double
      If Not (.Item("end_depth") Is DBNull.Value) Then Double.TryParse(.Item("end_depth"), Globalization.NumberStyles.Any, provider, end_depth)
      If (Not .Item("start_depth") Is DBNull.Value) Then Double.TryParse(.Item("start_depth"), Globalization.NumberStyles.Any, provider, start_depth)

      If (((.Item("end_depth") Is DBNull.Value) AndAlso (Not .Item("start_depth") Is DBNull.Value)) _
      OrElse ((Not .Item("start_depth") Is DBNull.Value) AndAlso (Not .Item("end_depth") Is DBNull.Value) _
      AndAlso (end_depth < start_depth))) Then
        Me.AddError(e.Row, .Table.Columns.Item("end_depth"), EddErrors.CustomError13)
      Else
        Me.RemoveError(e.Row, .Table.Columns.Item("end_depth"), EddErrors.CustomError13)
      End If
    End With

  End Sub

  'ERR27: Lab_name_code cannot be null when analysis_location=LB or FL. (14)
  'The element lab_name_code is a required field in EFW2LabTST  format . So the Checks ERR27 can be skipped
  'Private Sub ERR27(ByVal e As System.Data.DataColumnChangeEventArgs)
  '    With e.Row
  '        If (.Item("analysis_location").ToUpper = "LB" OrElse .Item("analysis_location").ToUpper = "FL") AndAlso .Item("lab_name_code") Is DBNull.Value Then
  '            Me.AddError(e.Row, e.Row.Table.Columns.Item("lab_name_code"), EddErrors.CustomError1)
  '        Else
  '            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("lab_name_code"), EddErrors.CustomError1)
  '        End If
  '    End With
  'End Sub

  'ERR28: Lab_sample_id cannot be null when analysis_location=LB. (15)
  Friend Sub ERR28(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      'VJN_Case4562_20041019
      If (Utilities.String.ToUpper(.Item("analysis_location")) = "LB" AndAlso .Item("lab_sample_id") Is DBNull.Value) Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("lab_sample_id"), EddErrors.CustomError15)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("lab_sample_id"), EddErrors.CustomError15)
      End If
    End With
  End Sub

  'ERR29: Result_unit cannot be null when result_value is not null. (16)
  'vin: According to the revised document this check is not needed.
  'Private Sub ERR29(ByVal e As System.Data.DataColumnChangeEventArgs)
  '    With e.Row
  '        If (Not .Item("result_value") Is DBNull.Value AndAlso .Item("result_unit") Is DBNull.Value) Then
  '            Me.AddError(e.Row, e.Row.Table.Columns.Item("result_unit"), EddErrors.CustomError1)
  '        Else
  '            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("result_unit"), EddErrors.CustomError1)
  '        End If
  '    End With
  'End Sub

  'ERR30: Detection_limit_unit cannot be null when reporting_detection_limit is not null. (17)
  Friend Sub ERR30(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      If (Not .Item("reporting_detection_limit") Is DBNull.Value AndAlso .Item("detection_limit_unit") Is DBNull.Value) Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("detection_limit_unit"), EddErrors.CustomError17)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("detection_limit_unit"), EddErrors.CustomError17)
      End If
    End With
  End Sub

  'DQM01: Percent_Moisture is required where sample_matrix_code=SO or SE and sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
  ' Used fields are EFW2LabTST.Percent_Moisture,EFW2FSample.sample_type_code and EFW2FSample.sample_matrix_code
  Private Sub DQM01(ByVal e As System.Data.DataColumnChangeEventArgs, ByVal relation As EarthSoft.EDP.EddRelation)

    ' if for some reason we don't have sample rows, just exit
    'If relation Is Me.FSample_Tst AndAlso Me.EFW2FSample.Rows.Count <= 0 Then Return
    If relation Is Me.LabSMP_Tst AndAlso Me.EFW2LabSMP.Rows.Count <= 0 Then Return

    With e.Row
      Try
        ' do we need to lookup the sample row?
        If (Me.sampleRow Is Nothing) OrElse (Not Me.sampleRow.Item("sys_sample_code").ToString.Equals(.Item("sys_sample_code"))) Then
          ' use the relation to get the parent row for this sample
          Me.sampleRow = relation.GetParentRow(e.Row)
          ' make sure it found the row
          If Me.sampleRow Is Nothing Then Return
        End If

        If (.Item("percent_moisture") Is DBNull.Value) AndAlso (Utilities.String.ToUpper(Me.sampleRow.Item("sample_matrix_code")) = "SE" OrElse Utilities.String.ToUpper(Me.sampleRow.Item("sample_matrix_code")) = "SO") AndAlso (sampleTypeCode_for_DQM21.Contains(Utilities.String.ToUpper(Me.sampleRow.Item("sample_type_code")))) Then
          Me.AddError(e.Row, e.Row.Table.Columns.Item("percent_moisture"), EddErrors.CustomError1)
        Else
          Me.RemoveError(e.Row, e.Row.Table.Columns.Item("percent_moisture"), EddErrors.CustomError1)
        End If
      Catch ex As Exception
        '...
        'EarthSoft.Shared.MsgBox.Show("DQM1 : " & ex.ToString)
      End Try
    End With
  End Sub

  'DQM02: Subsample_amount is required where sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
  ' Used fields are EFW2LabTST.subsample_amount and EFW2FSample.sample_type_code
  Private Sub DQM02(ByVal e As System.Data.DataColumnChangeEventArgs, ByVal relation As EarthSoft.EDP.EddRelation)

    ' if for some reason we don't have sample rows, just exit
    'If relation Is Me.FSample_Tst AndAlso Me.EFW2FSample.Rows.Count <= 0 Then Return
    If relation Is Me.LabSMP_Tst AndAlso Me.EFW2LabSMP.Rows.Count <= 0 Then Return

    With e.Row
      Try
        ' do we need to lookup the sample row?
        If (Me.sampleRow Is Nothing) OrElse (Not Me.sampleRow.Item("sys_sample_code").ToString.Equals(.Item("sys_sample_code"))) Then
          ' use the relation to get the parent row for this sample
          Me.sampleRow = relation.GetParentRow(e.Row)
          ' make sure it found the row
          If Me.sampleRow Is Nothing Then Return
        End If

        If (.Item("subsample_amount") Is DBNull.Value) AndAlso (sampleTypeCode_for_DQM21.Contains(Utilities.String.ToUpper(Me.sampleRow.Item("sample_type_code")))) Then
          Me.AddError(e.Row, e.Row.Table.Columns.Item("subsample_amount"), EddErrors.CustomError8)
        Else
          Me.RemoveError(e.Row, e.Row.Table.Columns.Item("subsample_amount"), EddErrors.CustomError8)
        End If
      Catch ex As Exception
        '...
      End Try
    End With
  End Sub

  Private Sub DQM03(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      If (Not .Item("subsample_amount") Is DBNull.Value AndAlso .Item("subsample_amount_unit") Is DBNull.Value) Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("subsample_amount_unit"), EddErrors.CustomError8)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("subsample_amount_unit"), EddErrors.CustomError8)
      End If
    End With
  End Sub


#End Region

#Region "Grid Events"
  ''' This routine may be overriden to provide custom handling when a cell drop-down list closes up.
  Public Overrides Sub Grid_AfterCellListCloseUp(ByVal sender As Object, ByVal e As Object, ByVal edp As Object)

    ' to get access to the selected row (of the drop-down), use:
    ' CType(e.Cell.Column.ValueList, Infragistics.Win.UltraWinGrid.UltraDropDown).SelectedRow

    'NOTE: do we need to check to see if this is the right grid?

    'when they select a cas_number, populate the param_name
    If e.Cell.Column.Key = "cas_rn" Then

      ' Infragistics.Win.UltraWinGrid.UltraGridRow
      Dim row As Object

      ' CType(e.Cell.Column.ValueList, Infragistics.Win.UltraWinGrid.UltraDropDown).SelectedRow
      'VJN added : 20043004
      'Assigning value to the cell only if any row selected from the dropdown list
      If Not e.Cell.Column.ValueList.SelectedRow Is Nothing Then
        row = e.Cell.Column.ValueList.SelectedRow
        e.Cell.Row.Cells.Item("chemical_name").Value = row.Cells.Item("chemical_name").Value
      End If
    End If

  End Sub
  '''   <item>EFW2LabSMP.sys_sample_code</item>
  '''   <item>EFW2LabSMP.sample_source</item>
  '''   <item>EFW2LabSMP.sample_matrix_code</item>
  '''   <item>EFW2LabSMP.sample_type_code</item>
  '''   <item>EFW2LabSMP.sample_date</item>
  '''   <item>EFW2LabSMP.sample_time</item>
  '''   <item>EFW2LabSMP.parent_sample_code</item>

  'for most checks, if one cell is updated, the other needs to be explicitly updated
  'because the error will need to be added/removed to both columns
  Public Overrides Sub Grid_AfterCellUpdate(ByVal sender As Object, ByVal e As Object, ByVal edp As Object)
    ' make an explicit call to AfterCellUpdate to show/clear the error on the other cell
    Select Case e.Cell.Column.Key.ToLower
      Case "sys_sample_code"
        If (e.Cell.Row.Band.Key.ToUpper = "EFW2LABSMP") Then
          edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sample_source"))
          edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sample_matrix_code"))
          edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sample_type_code"))
          edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sample_date"))
          edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sample_time"))
          edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("parent_sample_code"))
        End If
        If (e.Cell.Row.Band.Key.ToUpper = "EFW2LABTST") Then edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("total_or_dissolved"))
      Case "parent_sample_code"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sys_sample_code"))
      Case "sample_source"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sys_sample_code"))
        If (e.Cell.Row.Band.Key.ToUpper = "EFW2LABSMP") Then
          edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sample_matrix_code"))
          edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sample_type_code"))
          edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sample_date"))
          edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sample_time"))
          edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("parent_sample_code"))
        End If
      Case "qc_original_conc"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("qc_spike_measured"))
      Case "qc_dup_original_conc"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("qc_dup_spike_measured"))
      Case "sample_type_code"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sample_date"))
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("parent_sample_code"))

        If (e.Cell.Row.Band.Key.ToUpper = "EFW2FSAMPLE") Then edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sys_loc_code"))
        If (e.Cell.Row.Band.Key.ToUpper = "EFW2LABTST") Then edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("subsample_amount"))
        If (e.Cell.Row.Band.Key.ToUpper = "EFW2LABSMP") Then edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sys_sample_code"))
      Case "sample_date"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sys_sample_code"))
        If (e.Cell.Row.Band.Key.ToUpper = "EFW2LABSMP") Then edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sample_time"))
      Case "sample_time"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sys_sample_code"))
      Case "result_value"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("detect_flag"))
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("result_unit"))
      Case "result_type_code"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("detect_flag"))
      Case "reporting_detection_limit"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("detect_flag"))
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("detection_limit_unit"))
      Case "start_depth"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("end_depth"))
      Case "analysis_location"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("lab_name_code"))
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("lab_sample_id"))
      Case "sample_matrix_code"
        If (e.Cell.Row.Band.Key.ToUpper = "EFW2FSAMPLE") Then edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("start_depth"))
        If (e.Cell.Row.Band.Key.ToUpper = "EFW2LABTST") Then edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("percent_moisture"))
        If (e.Cell.Row.Band.Key.ToUpper = "EFW2LABSMP") Then edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sys_sample_code"))
      Case "subsample_amount"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("subsample_amount_unit"))
    End Select
  End Sub
 
#End Region

#Region "Create"
  Public Function HasCOC(ByVal eddrow As System.Data.DataRow) As Boolean
    Return Not eddrow.IsNull("chain_of_custody")
  End Function

  Public Function HasTaskCode(ByVal eddrow As System.Data.DataRow) As Boolean
    Return Not eddrow.IsNull("task_code")
  End Function

  Public Function HasSDG(ByVal eddrow As System.Data.DataRow) As Boolean
    Return Not eddrow.IsNull("sample_delivery_group")
  End Function
#End Region

#Region "Open"

  Public Overloads Overrides Sub SetupOpenFileDialog(ByVal dialog As Object, ByVal FormatName As String)
    dialog.title = String.Format("Select {0} Data File", FormatName)
    dialog.multiselect = False
    Me._OpenDialog = dialog
  End Sub

#End Region

  Private Class UpdateInfo
    Public table As EddTable
    Public columnName As String
    Public oldValue As String
    Public newValue As String

    Public Sub New()

    End Sub
  End Class

End Class