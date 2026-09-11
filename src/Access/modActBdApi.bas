Attribute VB_Name = "modActBdApi"
Option Compare Database
Option Explicit

' =========================================================
' CONFIGURACION POSTGRESQL & BACKEND API
' =========================================================

Private Const PG_SERVER As String = "localhost"
Private Const PG_PORT As String = "5432"
Private Const PG_DATABASE As String = "repredisl_api"
Private Const PG_USER As String = "postgres"
Private Const PG_PASSWORD As String = "Marc"
Private Const BATCH_SIZE As Long = 500

Private db As Object
Private cn As Object

' Constantes DAO (para evitar fallos de compilacion por falta de referencia de tipo)
Private Const dbOpenSnapshot As Long = 4
Private Const dbBoolean As Long = 1
Private Const dbByte As Long = 2
Private Const dbInteger As Long = 3
Private Const dbLong As Long = 4
Private Const dbCurrency As Long = 5
Private Const dbSingle As Long = 6
Private Const dbDouble As Long = 7
Private Const dbDate As Long = 8
Private Const dbText As Long = 10
Private Const dbLongBinary As Long = 11
Private Const dbMemo As Long = 12
Private Const dbGUID As Long = 15
Private Const dbBigInt As Long = 16
Private Const dbDecimal As Long = 20

' =========================================================
' EXPORTACION PRINCIPAL Y ACTUALIZACION AUTOMATICA
' =========================================================

Public Function ExportarTablas() As Boolean

    Dim qdf As Object
    Dim qryNameLower As String
    Dim nucleo As String
    Dim nomTab As String
    Dim countExportadas As Long

    RegistrarLogSync "DEBUG STEP 1: Entrando en ExportarTablas"

    On Error GoTo Err_Handler

    RegistrarLogSync "DEBUG STEP 2: Control de la sincronizacion activo"

    RegistrarLogSync "DEBUG STEP 5: AbrirConexionPostgres..."
    Set db = CurrentDb
    Set cn = AbrirConexionPostgres()
    RegistrarLogSync "DEBUG STEP 6: Conectado a Postgres"

    countExportadas = 0
    For Each qdf In db.QueryDefs
        qryNameLower = LCase$(qdf.Name)
        ' Solo consultas que empiecen por qry y terminen por api
        If Left$(qryNameLower, 3) = "qry" And Right$(qryNameLower, 3) = "api" And Len(qryNameLower) > 6 Then
            nucleo = Mid$(qryNameLower, 4, Len(qryNameLower) - 6)
            Select Case nucleo
                Case "uventas"
                    nomTab = "historial"
                Case "precios"
                    nomTab = "catalogo"
                Case "tarifas"
                    nomTab = "tarifas"
                Case Else
                    nomTab = nucleo
            End Select
            RegistrarLogSync "DEBUG STEP 7: Exportando " & qdf.Name & " -> " & nomTab
            ExportarQueryAPostgres qdf.Name, nomTab
            countExportadas = countExportadas + 1
        End If
    Next qdf

    RegistrarLogSync "DEBUG STEP 8: Permisos (" & CStr(countExportadas) & " tablas exportadas)"
    AplicarPermisosAPostgres

    RegistrarLogSync "Sincronizacion masiva finalizada con exito [OK]. Consultas exportadas: " & CStr(countExportadas)
    ExportarTablas = True

Salir:
    On Error Resume Next
    If Not cn Is Nothing Then
        If cn.State <> 0 Then cn.Close
    End If
    Set cn = Nothing
    Set db = Nothing
    Exit Function

Err_Handler:
    Dim NumeroError As Long, DescripcionError As String
    NumeroError = Err.Number
    DescripcionError = Err.Description

    ExportarTablas = False
    RegistrarLogSync "ERROR IN HANDLER: " & NumeroError & " - " & DescripcionError
    If Application.UserControl Then
        MsgBox "Error durante la exportacion." & vbCrLf & vbCrLf & _
               "Error " & NumeroError & vbCrLf & DescripcionError, _
               vbCritical, "PostgreSQL"
    End If
    Resume Salir
End Function

' =========================================================
' EXPORTAR QUERY ACCESS A POSTGRESQL (OPTIMIZADO EN BATCHES)
' =========================================================

Private Sub ExportarQueryAPostgres(ByVal NombreQuery As String, ByVal NombreTabla As String)

    Dim rs As DAO.Recordset
    Dim n As Long
    Dim totalFilas As Long
    Dim strCreaVista As String
    Dim transaccionActiva As Boolean
    Dim fld As DAO.Field
    Dim CamposHeader As String
    Dim ValFila As String
    Dim BatchValues As String
    Dim BatchCount As Long
    Dim NombreMostrar As String

    On Error GoTo Err_Handler

    NombreMostrar = UCase$(Left$(NombreTabla, 1)) & Mid$(NombreTabla, 2)

    Dim sqlSource As String
    Dim fldCheck As DAO.Field
    Dim rsSchema As DAO.Recordset

    sqlSource = "SELECT * FROM [" & NombreQuery & "]"

    Set rs = db.OpenRecordset(sqlSource, dbOpenSnapshot)

    ' Calcular total de filas
    If Not rs.EOF Then
        rs.MoveLast
        totalFilas = rs.RecordCount
        rs.MoveFirst
    Else
        totalFilas = 0
    End If

    RegistrarLogSync "Actualizando " & NombreMostrar & " (0 de " & Format$(totalFilas, "#,##0") & ") ..."

    If TablaExistePostgres(cn, NombreTabla) Then
        On Error Resume Next
        cn.Execute "DROP TABLE public." & Q(NombreTabla) & " CASCADE"
        On Error GoTo Err_Handler
    End If
    CrearTablaPostgres cn, NombreTabla, rs

    CamposHeader = ""
    For Each fld In rs.Fields
        If CamposHeader <> "" Then CamposHeader = CamposHeader & ","
        CamposHeader = CamposHeader & Q(LCase$(fld.Name))
    Next fld

    cn.BeginTrans
    transaccionActiva = True

    BatchValues = ""
    BatchCount = 0

    Do While Not rs.EOF
        ValFila = ""
        For Each fld In rs.Fields
            If ValFila <> "" Then ValFila = ValFila & ","
            ValFila = ValFila & ValorSQL(fld.Value)
        Next fld

        If BatchValues <> "" Then BatchValues = BatchValues & ","
        BatchValues = BatchValues & "(" & ValFila & ")"
        BatchCount = BatchCount + 1
        n = n + 1

        If BatchCount >= BATCH_SIZE Then
            cn.Execute "INSERT INTO public." & Q(NombreTabla) & " (" & CamposHeader & ") VALUES " & BatchValues
            RegistrarLogSync "Actualizando " & NombreMostrar & " (" & Format$(n, "#,##0") & " de " & Format$(totalFilas, "#,##0") & ") ..."
            BatchValues = ""
            BatchCount = 0
        End If

        rs.MoveNext
    Loop

    If BatchValues <> "" Then
        cn.Execute "INSERT INTO public." & Q(NombreTabla) & " (" & CamposHeader & ") VALUES " & BatchValues
        RegistrarLogSync "Actualizando " & NombreMostrar & " (" & Format$(n, "#,##0") & " de " & Format$(totalFilas, "#,##0") & ") [OK]"
    End If

    cn.CommitTrans
    transaccionActiva = False

    strCreaVista = "CREATE OR REPLACE VIEW api." & Q(NombreTabla) & " AS SELECT * FROM public." & Q(NombreTabla)
    cn.Execute strCreaVista

    Debug.Print NombreTabla & ": " & CStr(n) & " registros exportados en lotes."

Salir:
    On Error Resume Next
    If Not rs Is Nothing Then rs.Close
    Set rs = Nothing
    Exit Sub

Err_Handler:
    Dim NumeroError As Long, DescripcionError As String
    NumeroError = Err.Number
    DescripcionError = Err.Description

    On Error Resume Next
    If transaccionActiva Then cn.RollbackTrans
    If Not rs Is Nothing Then rs.Close
    Set rs = Nothing
    On Error GoTo 0

    Err.Raise NumeroError, "ExportarQueryAPostgres (" & NombreTabla & ")", DescripcionError
End Sub

' =========================================================
' REGISTRADOR DE PROGRESO DE REGISTRO EN TIEMPO REAL
' =========================================================

Private Sub RegistrarLogSync(ByVal Mensaje As String)
    On Error Resume Next
    Dim fileNum As Integer
    Dim logPath As String
    Dim errLogPath As String

    logPath = CurrentProject.Path & "\sync_progress.log"
    fileNum = FreeFile
    Open logPath For Append As #fileNum
    Print #fileNum, "[" & Format$(Now, "hh:nn:ss") & "] " & Mensaje
    Close #fileNum

    If (InStr(1, Mensaje, "ERROR IN HANDLER", vbTextCompare) > 0 Or InStr(1, Mensaje, "Fallo", vbTextCompare) > 0) And InStr(1, Mensaje, "DEBUG STEP", vbTextCompare) = 0 Then
        errLogPath = CurrentProject.Path & "\sync_errors.log"
        fileNum = FreeFile
        Open errLogPath For Append As #fileNum
        Print #fileNum, "[" & Format$(Now, "hh:nn:ss") & "] " & Mensaje
        Close #fileNum
    End If
End Sub

Public Sub RegistrarNuevoPedido(ByVal numPedido As String, ByVal cliente As String, ByVal importe As Double)
    RegistrarLogSync "[NUEVO PEDIDO] Recibido pedido N. " & numPedido & " | Cliente: " & cliente & " | Importe: " & Format$(importe, "#,##0.00") & " EUR"
End Sub

' =========================================================
' CONEXION POSTGRESQL Y AUXILIARES
' =========================================================

Private Function AbrirConexionPostgres() As Object
    Dim conexion As Object
    Dim Cadena As String
    Set conexion = CreateObject("ADODB.Connection")

    Cadena = "Driver={PostgreSQL Unicode};" & _
             "Server=" & PG_SERVER & ";" & _
             "Port=" & PG_PORT & ";" & _
             "Database=" & PG_DATABASE & ";" & _
             "Uid=" & PG_USER & ";" & _
             "Pwd=" & PG_PASSWORD & ";"

    conexion.Open Cadena
    Set AbrirConexionPostgres = conexion
End Function

Private Function TablaExistePostgres(ByVal cn As Object, ByVal NombreTabla As String) As Boolean
    Dim rs As Object
    Dim sql As String
    sql = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = " & ValorSQL(LCase$(NombreTabla)) & ") AS existe"
    Set rs = cn.Execute(sql)
    TablaExistePostgres = CBool(rs.Fields("existe").Value)
    rs.Close
    Set rs = Nothing
End Function

Private Sub CrearTablaPostgres(ByVal cn As Object, ByVal NombreTabla As String, ByVal rs As DAO.Recordset)
    Dim fld As DAO.Field
    Dim sql As String
    sql = "CREATE TABLE public." & Q(NombreTabla) & " ("
    For Each fld In rs.Fields
        sql = sql & Q(LCase$(fld.Name)) & " " & TipoPostgres(fld) & ","
    Next fld
    sql = Left$(sql, Len(sql) - 1) & ")"
    cn.Execute sql
End Sub

Private Function TipoPostgres(ByVal fld As DAO.Field) As String
    Select Case fld.Type
        Case dbBoolean: TipoPostgres = "boolean"
        Case dbByte: TipoPostgres = "smallint"
        Case dbInteger: TipoPostgres = "integer"
        Case dbLong: TipoPostgres = "integer"
        Case dbSingle: TipoPostgres = "real"
        Case dbDouble: TipoPostgres = "double precision"
        Case dbCurrency: TipoPostgres = "numeric(19,4)"
        Case dbDecimal: TipoPostgres = "numeric"
        Case dbDate: TipoPostgres = "timestamp"
        Case dbText
            If fld.Size > 0 And fld.Size <= 10485760 Then
                TipoPostgres = "varchar(" & CStr(fld.Size) & ")"
            Else
                TipoPostgres = "text"
            End If
        Case dbMemo: TipoPostgres = "text"
        Case dbGUID: TipoPostgres = "uuid"
        Case Else: TipoPostgres = "text"
    End Select
End Function

Private Function ValorSQL(ByVal Valor As Variant) As String
    If IsNull(Valor) Then
        ValorSQL = "NULL"
        Exit Function
    End If
    Select Case VarType(Valor)
        Case vbBoolean
            If Valor Then ValorSQL = "TRUE" Else ValorSQL = "FALSE"
        Case vbDate
            ValorSQL = "'" & Format$(Valor, "yyyy-mm-dd hh:nn:ss") & "'"
        Case vbByte, vbInteger, vbLong, vbSingle, vbDouble, vbCurrency, vbDecimal
            ValorSQL = Replace$(CStr(Valor), ",", ".")
        Case Else
            ValorSQL = "'" & Replace$(CStr(Valor), "'", "''") & "'"
    End Select
End Function

Private Function Q(ByVal Nombre As String) As String
    Q = chr(34) & Replace$(Nombre, chr(34), chr(34) & chr(34)) & chr(34)
End Function

Private Sub AplicarPermisosAPostgres()
    On Error GoTo ErrHandler
    cn.Execute "GRANT USAGE ON SCHEMA api TO web_anon;"
    cn.Execute "GRANT SELECT ON ALL TABLES IN SCHEMA api TO web_anon;"
    cn.Execute "ALTER DEFAULT PRIVILEGES IN SCHEMA api GRANT SELECT ON TABLES TO web_anon;"
    cn.Execute "NOTIFY pgrst, 'reload schema';"
    Exit Sub
ErrHandler:
    Err.Raise Err.Number, "AplicarPermisosAPostgres", Err.Description
End Sub
