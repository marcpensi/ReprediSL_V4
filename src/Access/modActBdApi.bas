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

Private db As DAO.Database
Private cn As Object

' =========================================================
' EXPORTACION PRINCIPAL Y ACTUALIZACION AUTOMATICA
' =========================================================

Public Sub ExportarTablas()

    Dim nomTabla(0 To 4) As String
    Dim i As Long
    Dim nomTab As String
    Dim nomQry As String

    On Error GoTo Err_Handler

    ' 1. Asegurar que las consultas y campos necesarios existan en Access
    ActualizarEstructuraAccess

    nomTabla(0) = "clientes"
    nomTabla(1) = "vendedores"
    nomTabla(2) = "tarifas"
    nomTabla(3) = "precios"
    nomTabla(4) = "uventas"

    Set db = CurrentDb
    Set cn = AbrirConexionPostgres()

    For i = LBound(nomTabla) To UBound(nomTabla)
        nomTab = nomTabla(i)
        nomQry = "Qry" & UCase$(Left$(nomTab, 1)) & Mid$(nomTab, 2) & "Api"
        ExportarQueryAPostgres nomQry, nomTab
    Next i

    AplicarPermisosAPostgres

    MsgBox "Exportacion optimizada por lotes finalizada correctamente.", _
           vbInformation, _
           "PostgreSQL"

Salir:
    On Error Resume Next
    If Not cn Is Nothing Then
        If cn.State <> 0 Then cn.Close
    End If
    Set cn = Nothing
    Set db = Nothing
    Exit Sub

Err_Handler:
    MsgBox "Error durante la exportacion." & vbCrLf & vbCrLf & _
           "Error " & Err.Number & vbCrLf & Err.Description, _
           vbCritical, "PostgreSQL"
    Resume Salir
End Sub

' =========================================================
' ACTUALIZADOR AUTOMATICO DE ESTRUCTURA Y CONSULTAS ACCESS
' =========================================================

Public Sub ActualizarEstructuraAccess()

    Dim localDb As DAO.Database
    Set localDb = CurrentDb

    On Error Resume Next

    ' 1. Asegurar Campos de Control en Tablas Principales (si existen)
    AsegurarCampoAccess localDb, "Clientes", "fecha_modificacion", "DATETIME"
    AsegurarCampoAccess localDb, "Clientes", "activo", "BIT"
    AsegurarCampoAccess localDb, "Productos", "fecha_modificacion", "DATETIME"

    ' 2. Crear / Actualizar las 5 Consultas API Requeridas
    CrearOCambiarConsultaAPI localDb, "QryClientesApi", _
        "SELECT CodCliente AS id_cliente, CodCliente AS codigo, NombreFiscal AS nombre, " & _
        "NombreComercial AS nombre_comercial, NifCIF AS nif, Telefono AS telefono, " & _
        "Movil AS movil, Email AS email, Web AS web, Direccion AS street, CodPostal AS codigo_postal, " & _
        "Poblacion AS city, Provincia AS state, DireccionEnvio AS direccionenvio, " & _
        "CodPostalEnvio AS cpostalenvio, PoblacionEnvio AS poblacionenvio, ProvinciaEnvio AS provinciaenvio, " & _
        "Banco AS nombre_banco, IBAN AS cuenta_bancaria, CodVendedor AS id_vendedor FROM Clientes"

    CrearOCambiarConsultaAPI localDb, "QryVendedoresApi", _
        "SELECT CodVendedor AS id_vendedor, Nombre AS nombre_vendedor, Serie AS serie FROM Vendedores"

    CrearOCambiarConsultaAPI localDb, "QryTarifasApi", _
        "SELECT CodTarifa AS id_tarifa, NombreTarifa AS nombre_tarifa, Descuento AS descuento FROM Tarifas"

    CrearOCambiarConsultaAPI localDb, "QryPreciosApi", _
        "SELECT CodProducto AS id_producto, CodProducto AS codigo, CodTarifa AS id_tarifa, PrecioVenta AS precio_venta FROM Precios"

    CrearOCambiarConsultaAPI localDb, "QryUventasApi", _
        "SELECT CodProducto AS id_producto, CodProducto AS codigo, UnidadesCaja AS unidades_caja, UnidadVenta AS unidad_venta FROM Uventas"

    Set localDb = Nothing
End Sub

Private Sub AsegurarCampoAccess(ByRef dbs As DAO.Database, ByVal tabla As String, ByVal campo As String, ByVal tipoSql As String)
    On Error Resume Next
    Dim tdf As DAO.TableDef
    Dim fld As DAO.Field
    Set tdf = dbs.TableDefs(tabla)
    If Err.Number = 0 Then
        Set fld = tdf.Fields(campo)
        If Err.Number <> 0 Then
            Err.Clear
            dbs.Execute "ALTER TABLE [" & tabla & "] ADD COLUMN [" & campo & "] " & tipoSql
        End If
    End If
    Err.Clear
End Sub

Private Sub CrearOCambiarConsultaAPI(ByRef dbs As DAO.Database, ByVal nomConsulta As String, ByVal sqlText As String)
    On Error Resume Next
    Dim qdf As DAO.QueryDef
    Set qdf = dbs.QueryDefs(nomConsulta)
    If Err.Number = 0 Then
        qdf.SQL = sqlText
    Else
        Err.Clear
        dbs.CreateQueryDef nomConsulta, sqlText
    End If
    Err.Clear
End Sub

' =========================================================
' EXPORTAR QUERY ACCESS A POSTGRESQL (OPTIMIZADO EN BATCHES)
' =========================================================

Private Sub ExportarQueryAPostgres(ByVal NombreQuery As String, ByVal NombreTabla As String)

    Dim rs As DAO.Recordset
    Dim n As Long
    Dim strCreaVista As String
    Dim transaccionActiva As Boolean
    Dim fld As DAO.Field
    Dim CamposHeader As String
    Dim ValFila As String
    Dim BatchValues As String
    Dim BatchCount As Long

    On Error GoTo Err_Handler

    Set rs = db.OpenRecordset(NombreQuery, dbOpenSnapshot)

    If Not TablaExistePostgres(cn, NombreTabla) Then
        CrearTablaPostgres cn, NombreTabla, rs
    Else
        cn.Execute "TRUNCATE TABLE public." & Q(NombreTabla)
    End If

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
            BatchValues = ""
            BatchCount = 0
        End If

        rs.MoveNext
    Loop

    If BatchValues <> "" Then
        cn.Execute "INSERT INTO public." & Q(NombreTabla) & " (" & CamposHeader & ") VALUES " & BatchValues
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
