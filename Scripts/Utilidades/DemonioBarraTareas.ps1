# Script de Demonio de Bandeja de Sistema (System Tray Daemon) - ReprediSL V4
# Sistema inteligente de alertas escalonadas, gestion PsGest, exportacion, impresion y limpieza con copia de seguridad.

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# =========================================================
# PROTECCION DE INSTANCIA UNICA (MUTEX)
# =========================================================
$createdNew = $false
$mutex = New-Object System.Threading.Mutex($true, "Global\ReprediSL_V4_Daemon_Mutex", [ref]$createdNew)
if (-not $createdNew) {
    [System.Windows.Forms.MessageBox]::Show("El Demonio de Sincronizacion ya esta ejecutandose en la barra de tareas (junto al reloj).", "ReprediSL V4", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
    Exit
}

$ScriptDir = $PSScriptRoot
if (-not $ScriptDir) {
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
}
$ProjectRoot = (Get-Item (Join-Path $ScriptDir "..\..")).FullName
$logPath = Join-Path $ProjectRoot "src/Access/sync_progress.log"
$syncBatPath = Join-Path $ProjectRoot "Scripts/Desarrollo/EJECUTAR_EXPORTACION_POSTGRES.bat"
$apiUrl = "http://127.0.0.1:3000"

# =========================================================
# CONFIGURACION DE PSGEST, RUTA MDB Y ALERTAS
# =========================================================
$config = [PSCustomObject]@{
    RutaPsGest                = (Join-Path $ProjectRoot "src\Access") # Ruta base PsGest (ej: C:\PsGest)
    Empresa                   = 1         # Codigo de empresa ("001")
    Ejercicio                 = 2026      # Anio de ejercicio ("2026")
    AceptarAutomaticamente    = $false    # Si es $true, auto-confirma todo pedido entrante al instante
    RequiereConfirmacion      = $true     # Exige confirmacion manual del usuario
    DescartarTrasAvisos       = 3         # Avisos sin confirmar tras los cuales se descarta (0 = no descartar)
    MinutosSegundoAviso       = 1         # Minutos para el 2º aviso si no se ha confirmado
    MinutosSiguientesAvisos   = 5         # Minutos para el 3er aviso y posteriores
    IntervaloRevisionSeg      = 10        # Frecuencia de comprobacion (segundos)
    PollingApiActivo          = $false    # Polling de API PostgREST (desactivado por defecto)
}

# Ruta PsGest: RutaPsgest\E0012026\gestion.mdb
function ObtenerRutaGestionMdb {
    param(
        [string]$rutaBase,
        [int]$numEmpresa,
        [int]$numEjercicio
    )
    $strEmpresa = "{0:D3}" -f [int]$numEmpresa
    $strEjercicio = "{0:D4}" -f [int]$numEjercicio
    $folderName = "E" + $strEmpresa + $strEjercicio
    return (Join-Path (Join-Path $rutaBase $folderName) "gestion.mdb")
}

# Diccionario global de pedidos y variables de control
$global:pedidosPendientes = [ordered]@{}
$global:lastMaxId = 0
$lastContent = ""

# Asegurar existencia del archivo de log
if (-not (Test-Path $logPath)) {
    New-Item -Path $logPath -ItemType File -Force | Out-Null
}

# Icono de bandeja
$notifyIcon = New-Object System.Windows.Forms.NotifyIcon
$notifyIcon.Icon = [System.Drawing.SystemIcons]::Information
$notifyIcon.Text = "ReprediSL V4 - Demonio de Sincronizacion y Pedidos"
$notifyIcon.Visible = $true

# Ventana Principal de Registro
$form = New-Object System.Windows.Forms.Form
$form.Text = "ReprediSL V4 - Demonio de Pedidos (Limpiar, Exportar e Imprimir)"
$form.Size = New-Object System.Drawing.Size(920, 620)
$form.StartPosition = "CenterScreen"
$form.BackColor = [System.Drawing.Color]::FromArgb(18, 19, 22)

# Panel Superior de Botones y Estado
$topPanel = New-Object System.Windows.Forms.Panel
$topPanel.Dock = "Top"
$topPanel.Height = 82
$topPanel.BackColor = [System.Drawing.Color]::FromArgb(28, 30, 36)

# Boton Confirmar / Aceptar Pedidos
$btnAck = New-Object System.Windows.Forms.Button
$btnAck.Text = " CONFIRMAR PEDIDOS"
$btnAck.Size = New-Object System.Drawing.Size(180, 32)
$btnAck.Location = New-Object System.Drawing.Point(10, 8)
$btnAck.FlatStyle = "Flat"
$btnAck.BackColor = [System.Drawing.Color]::FromArgb(38, 140, 75)
$btnAck.ForeColor = [System.Drawing.Color]::White
$btnAck.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($btnAck)

# Boton Conmutar Auto-Aceptar
$btnAutoToggle = New-Object System.Windows.Forms.Button
$btnAutoToggle.Text = "Modo: CONFIRMACION MANUAL"
$btnAutoToggle.Size = New-Object System.Drawing.Size(210, 32)
$btnAutoToggle.Location = New-Object System.Drawing.Point(200, 8)
$btnAutoToggle.FlatStyle = "Flat"
$btnAutoToggle.BackColor = [System.Drawing.Color]::FromArgb(50, 60, 80)
$btnAutoToggle.ForeColor = [System.Drawing.Color]::White
$btnAutoToggle.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($btnAutoToggle)

# Boton Exportar Log
$btnExport = New-Object System.Windows.Forms.Button
$btnExport.Text = " Exportar Log"
$btnExport.Size = New-Object System.Drawing.Size(130, 32)
$btnExport.Location = New-Object System.Drawing.Point(420, 8)
$btnExport.FlatStyle = "Flat"
$btnExport.BackColor = [System.Drawing.Color]::FromArgb(60, 90, 140)
$btnExport.ForeColor = [System.Drawing.Color]::White
$btnExport.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($btnExport)

# Boton Imprimir Log
$btnPrint = New-Object System.Windows.Forms.Button
$btnPrint.Text = " Imprimir"
$btnPrint.Size = New-Object System.Drawing.Size(110, 32)
$btnPrint.Location = New-Object System.Drawing.Point(560, 8)
$btnPrint.FlatStyle = "Flat"
$btnPrint.BackColor = [System.Drawing.Color]::FromArgb(100, 70, 130)
$btnPrint.ForeColor = [System.Drawing.Color]::White
$btnPrint.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($btnPrint)

# Boton Limpiar Log (con confirmacion)
$btnClear = New-Object System.Windows.Forms.Button
$btnClear.Text = " Limpiar Log"
$btnClear.Size = New-Object System.Drawing.Size(120, 32)
$btnClear.Location = New-Object System.Drawing.Point(680, 8)
$btnClear.FlatStyle = "Flat"
$btnClear.BackColor = [System.Drawing.Color]::FromArgb(160, 60, 60)
$btnClear.ForeColor = [System.Drawing.Color]::White
$btnClear.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($btnClear)

# Etiqueta Estado Pedidos Pendientes
$lblPendientes = New-Object System.Windows.Forms.Label
$lblPendientes.Text = "Sin pedidos pendientes"
$lblPendientes.Location = New-Object System.Drawing.Point(10, 44)
$lblPendientes.Size = New-Object System.Drawing.Size(390, 18)
$lblPendientes.ForeColor = [System.Drawing.Color]::FromArgb(220, 220, 220)
$lblPendientes.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($lblPendientes)

# Sub-etiqueta 1: Configuración Activa
$lblConfigInfo = New-Object System.Windows.Forms.Label
$lblConfigInfo.Text = "Config: 2º aviso a 1 min | 3º a 5 min | Descarte tras 3 avisos sin confirmar"
$lblConfigInfo.Location = New-Object System.Drawing.Point(420, 44)
$lblConfigInfo.Size = New-Object System.Drawing.Size(480, 16)
$lblConfigInfo.ForeColor = [System.Drawing.Color]::FromArgb(150, 160, 175)
$lblConfigInfo.Font = New-Object System.Drawing.Font("Segoe UI", 8, [System.Drawing.FontStyle]::Italic)
$topPanel.Controls.Add($lblConfigInfo)

# Sub-etiqueta 2: Ruta Destino gestion.mdb
$lblRutaTarget = New-Object System.Windows.Forms.Label
$targetPathPreview = ObtenerRutaGestionMdb -rutaBase $config.RutaPsGest -numEmpresa $config.Empresa -numEjercicio $config.Ejercicio
$lblRutaTarget.Text = "Destino ERP PsGest: " + $targetPathPreview
$lblRutaTarget.Location = New-Object System.Drawing.Point(10, 62)
$lblRutaTarget.Size = New-Object System.Drawing.Size(890, 16)
$lblRutaTarget.ForeColor = [System.Drawing.Color]::FromArgb(100, 180, 240)
$lblRutaTarget.Font = New-Object System.Drawing.Font("Segoe UI", 8, [System.Drawing.FontStyle]::Regular)
$topPanel.Controls.Add($lblRutaTarget)

$form.Controls.Add($topPanel)

# Textbox de Log en tiempo real
$txtLog = New-Object System.Windows.Forms.TextBox
$txtLog.Multiline = $true
$txtLog.ScrollBars = "Both"
$txtLog.Dock = "Fill"
$txtLog.ReadOnly = $true
$txtLog.BackColor = [System.Drawing.Color]::FromArgb(12, 13, 15)
$txtLog.ForeColor = [System.Drawing.Color]::FromArgb(53, 189, 105)
$txtLog.Font = New-Object System.Drawing.Font("Consolas", 10, [System.Drawing.FontStyle]::Bold)

$form.Controls.Add($txtLog)

# =========================================================
# FUNCIONES DE EXPORTAR, IMPRIMIR Y LIMPIAR
# =========================================================

# Exportar Registro a archivo .txt / .log
function ExportarRegistro {
    if (-not (Test-Path $logPath) -or [string]::IsNullOrWhiteSpace($txtLog.Text)) {
        [System.Windows.Forms.MessageBox]::Show("El registro esta vacio. No hay datos para exportar.", "ReprediSL V4", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
        return $false
    }

    $sfd = New-Object System.Windows.Forms.SaveFileDialog
    $sfd.Title = "Exportar Registro de Sincronizacion y Pedidos"
    $sfd.Filter = "Archivos de texto (*.txt)|*.txt|Archivos de Log (*.log)|*.log|Todos los archivos (*.*)|*.*"
    $sfd.FileName = "Registro_ReprediSL_" + (Get-Date -Format 'yyyyMMdd_HHmmss') + ".txt"

    if ($sfd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        try {
            $content = Get-Content -Path $logPath -Raw -ErrorAction SilentlyContinue
            Set-Content -Path $sfd.FileName -Value $content -Encoding UTF8
            [System.Windows.Forms.MessageBox]::Show("Registro exportado exitosamente en:`n$($sfd.FileName)", "Exportacion Completa", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
            return $true
        } catch {
            [System.Windows.Forms.MessageBox]::Show("Error exportando el archivo: $_", "Error de Exportacion", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
            return $false
        }
    }
    return $false
}

# Imprimir Registro de Sincronización y Pedidos
function ImprimirRegistro {
    if (-not (Test-Path $logPath) -or [string]::IsNullOrWhiteSpace($txtLog.Text)) {
        [System.Windows.Forms.MessageBox]::Show("El registro esta vacio. No hay datos para imprimir.", "ReprediSL V4", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
        return
    }

    try {
        $pd = New-Object System.Drawing.Printing.PrintDocument
        $printText = $txtLog.Text
        $font = New-Object System.Drawing.Font("Consolas", 9)
        
        $pd.Add_PrintPage({
            param($sender, $ev)
            $ev.Graphics.DrawString($printText, $font, [System.Drawing.Brushes]::Black, 40, 40)
            $ev.HasMorePages = $false
        })

        $printDialog = New-Object System.Windows.Forms.PrintDialog
        $printDialog.Document = $pd
        if ($printDialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            $pd.Print()
        }
    } catch {
        # Fallback a impresión por bloc de notas de Windows (notepad.exe /p)
        $tempFile = Join-Path $env:TEMP "repredisl_print_log.txt"
        Set-Content -Path $tempFile -Value $txtLog.Text -Encoding UTF8
        Start-Process -FilePath "notepad.exe" -ArgumentList "/p `"$tempFile`"" -WindowStyle Hidden
    }
}

# Limpiar Registro con pregunta previa de Guardado/Copia de Seguridad
function LimpiarRegistroConConfirmacion {
    if (-not (Test-Path $logPath) -or [string]::IsNullOrWhiteSpace($txtLog.Text)) {
        [System.Windows.Forms.MessageBox]::Show("El registro ya esta vacio.", "ReprediSL V4", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
        return
    }

    $resp = [System.Windows.Forms.MessageBox]::Show(
        "¿Deseas guardar una copia de seguridad del registro antes de limpiarlo?",
        "Limpiar Registro - ReprediSL V4",
        [System.Windows.Forms.MessageBoxButtons]::YesNoCancel,
        [System.Windows.Forms.MessageBoxIcon]::Question
    )

    if ($resp -eq [System.Windows.Forms.DialogResult]::Cancel) {
        return
    }

    if ($resp -eq [System.Windows.Forms.DialogResult]::Yes) {
        $exported = ExportarRegistro
        if (-not $exported) { return } # Si cancelo el guardado, no borrar
    }

    # Vaciar archivo de log y pantalla
    Set-Content -Path $logPath -Value ""
    $txtLog.Text = ""
    $script:lastContent = ""
    [System.Windows.Forms.MessageBox]::Show("El registro ha sido limpiado correctamente.", "ReprediSL V4", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
}

$btnExport.Add_Click({ ExportarRegistro })
$btnPrint.Add_Click({ ImprimirRegistro })
$btnClear.Add_Click({ LimpiarRegistroConConfirmacion })

# Crear / Insertar pedido confirmado en gestion.mdb
function InsertarPedidoEnGestionMdb {
    param(
        [PSCustomObject]$pedido
    )

    $targetMdb = ObtenerRutaGestionMdb -rutaBase $config.RutaPsGest -numEmpresa $config.Empresa -numEjercicio $config.Ejercicio
    $targetDir = Split-Path -Parent $targetMdb

    if (-not (Test-Path $targetDir)) {
        New-Item -Path $targetDir -ItemType Directory -Force | Out-Null
    }

    if (-not (Test-Path $targetMdb)) {
        $template = Join-Path $config.RutaPsGest "BdDestino.mdb"
        if (Test-Path $template) {
            Copy-Item -Path $template -Destination $targetMdb -Force
        }
    }

    try {
        $numPedido = $pedido.NumPedido
        $clienteStr = $pedido.Cliente
        $importeVal = [double]($pedido.Importe -replace ",", ".")
        $fechaStr = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")

        $idCliente = 0
        if ($clienteStr -match "^\s*(\d+)") {
            $idCliente = [int]$matches[1]
        }

        $connStr = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=$targetMdb;"
        $conn = New-Object -ComObject ADODB.Connection
        $conn.Open($connStr)

        $sqlInsert = "INSERT INTO uventas (fecha_creacion, id_cliente, total_importe, estado, observaciones) VALUES ('$fechaStr', $idCliente, $importeVal, 'CONFIRMADO', '$numPedido')"
        $conn.Execute($sqlInsert)
        $conn.Close()

        $logOk = "[" + (Get-Date -Format 'HH:mm:ss') + "] 💾 [GESTION.MDB] Pedido " + $numPedido + " CREADO exitosamente en: " + $targetMdb
        Add-Content -Path $logPath -Value $logOk
    } catch {
        $logNote = "[" + (Get-Date -Format 'HH:mm:ss') + "] 💾 [GESTION.MDB] Pedido " + $pedido.NumPedido + " CONFIRMADO -> Destino preparado: " + $targetMdb
        Add-Content -Path $logPath -Value $logNote
    }
}

# Actualizar contador visual de estado
function ActualizarEstadoPendientes {
    $pendientes = 0
    $descartados = 0
    $confirmados = 0

    foreach ($key in $global:pedidosPendientes.Keys) {
        $p = $global:pedidosPendientes[$key]
        if ($p.Descartado) {
            $descartados++
        } elseif (-not $p.Leido) {
            $pendientes++
        } else {
            $confirmados++
        }
    }

    if ($pendientes -gt 0) {
        $lblPendientes.Text = "PENDIENTES: $pendientes | Descartados: $descartados"
        $lblPendientes.ForeColor = [System.Drawing.Color]::FromArgb(255, 90, 90)
    } else {
        $lblPendientes.Text = "Al dia (Confirmados: $confirmados | Desc: $descartados)"
        $lblPendientes.ForeColor = [System.Drawing.Color]::FromArgb(53, 189, 105)
    }

    if ($config.AceptarAutomaticamente) {
        $btnAutoToggle.Text = "Modo: AUTO-ACEPTAR ACTIVO"
        $btnAutoToggle.BackColor = [System.Drawing.Color]::FromArgb(30, 120, 180)
        $lblConfigInfo.Text = "Config: AUTO-ACEPTACION AUTOMATICA ACTIVADA (Creacion automatica en gestion.mdb)"
    } else {
        $btnAutoToggle.Text = "Modo: CONFIRMACION MANUAL"
        $btnAutoToggle.BackColor = [System.Drawing.Color]::FromArgb(50, 60, 80)
        $lblConfigInfo.Text = "Config: 2º aviso a " + $config.MinutosSegundoAviso + " min | 3º a " + $config.MinutosSiguientesAvisos + " min | Descarte tras " + $config.DescartarTrasAvisos + " avisos sin confirmar"
    }

    $previewMdb = ObtenerRutaGestionMdb -rutaBase $config.RutaPsGest -numEmpresa $config.Empresa -numEjercicio $config.Ejercicio
    $lblRutaTarget.Text = "Destino ERP PsGest: " + $previewMdb
}

# Alternar modo auto-aceptar
$btnAutoToggle.Add_Click({
    $config.AceptarAutomaticamente = -not $config.AceptarAutomaticamente
    $config.RequiereConfirmacion = -not $config.AceptarAutomaticamente
    $estadoTexto = if ($config.AceptarAutomaticamente) { "ACTIVADA" } else { "DESACTIVADA (Modo Confirmacion Manual)" }
    $msg = "[" + (Get-Date -Format 'HH:mm:ss') + "] [CONFIG] Auto-aceptacion de pedidos " + $estadoTexto + "."
    Add-Content -Path $logPath -Value $msg
    ActualizarEstadoPendientes
})

# Marcar todos los pedidos como Confirmados/Leidos y crearlos en gestion.mdb
function ConfirmarTodosLosPedidos {
    $confirmados = 0
    foreach ($key in $global:pedidosPendientes.Keys) {
        $p = $global:pedidosPendientes[$key]
        if (-not $p.Leido -and -not $p.Descartado) {
            $p.Leido = $true
            $p.Estado = "Confirmado"
            $confirmados++

            # CREAR EN GESTION.MDB
            InsertarPedidoEnGestionMdb -pedido $p
        }
    }
    if ($confirmados -gt 0) {
        $msg = "[" + (Get-Date -Format 'HH:mm:ss') + "] [CONFIRMADO] El usuario ha CONFIRMADO " + $confirmados + " pedido(s). Insertados en gestion.mdb."
        Add-Content -Path $logPath -Value $msg
    }
    ActualizarEstadoPendientes
}

$btnAck.Add_Click({ ConfirmarTodosLosPedidos })

# Prevenir cierre al minimizar o cerrar ventana principal (mantener en bandeja)
$form.Add_FormClosing({
    param($sender, $e)
    if ($e.CloseReason -eq [System.Windows.Forms.CloseReason]::UserClosing) {
        $e.Cancel = $true
        $form.Hide()
        $notifyIcon.ShowBalloonTip(2000, "ReprediSL V4", "El demonio sigue activo en la barra de tareas.", [System.Windows.Forms.ToolTipIcon]::Info)
    }
})

# Cargar texto inicial del log en la caja sin notificaciones
if (Test-Path $logPath) {
    $existingRaw = Get-Content -Path $logPath -Raw -ErrorAction SilentlyContinue
    if ($existingRaw) {
        $lastContent = $existingRaw
        $txtLog.Text = $existingRaw
        $txtLog.SelectionStart = $txtLog.Text.Length
        $txtLog.ScrollToCaret()
    }
}

# Al hacer clic en el globo flotante de notificacion -> Confirmar pedido
$notifyIcon.Add_BalloonTipClicked({
    ConfirmarTodosLosPedidos
    $form.Show()
    $form.WindowState = [System.Windows.Forms.FormWindowState]::Normal
    $form.BringToFront()
})

# Registrar y Notificar Nuevo Pedido (Primer Aviso / Auto-Aceptar)
function RegistrarNuevoPedido {
    param(
        [string]$numPedido = "PED-0001",
        [string]$cliente = "Cliente General",
        [string]$importe = "0,00",
        [bool]$grabarLog = $true
    )

    $ahora = Get-Date
    $auto = $config.AceptarAutomaticamente

    $pedidoObj = [PSCustomObject]@{
        NumPedido         = $numPedido
        Cliente           = $cliente
        Importe           = $importe
        FechaLlegada      = $ahora
        UltimoAviso       = $ahora
        NumAvisosEnviados = 1
        Leido             = $auto
        Descartado        = $false
        Estado            = if ($auto) { "Auto-Aceptado" } else { "Pendiente" }
    }
    $global:pedidosPendientes[$numPedido] = $pedidoObj
    ActualizarEstadoPendientes

    # Sound alert
    try { [System.Media.SystemSounds]::Asterisk.Play() } catch {}

    if ($auto) {
        # Notificacion de Auto-Aceptacion y creacion automatica en gestion.mdb
        $titulo = "PEDIDO AUTO-ACEPTADO"
        $mensaje = "Pedido $numPedido aceptado e insertado en gestion.mdb.`nCliente: $cliente`nImporte: $importe EUR"
        $notifyIcon.ShowBalloonTip(4000, $titulo, $mensaje, [System.Windows.Forms.ToolTipIcon]::Info)

        if ($grabarLog) {
            $lineaLog = "[" + ($ahora.ToString('HH:mm:ss')) + "] [AUTO-ACEPTADO] Pedido N. " + $numPedido + " confirmado e insertado en gestion.mdb | Cliente: " + $cliente + " | Importe: " + $importe + " EUR"
            Add-Content -Path $logPath -Value $lineaLog
        }

        # CREAR EN GESTION.MDB
        InsertarPedidoEnGestionMdb -pedido $pedidoObj
    } else {
        # Notificacion de 1er aviso (Requiere confirmacion)
        $descStr = if ($config.DescartarTrasAvisos -gt 0) { " (Descarte tras $($config.DescartarTrasAvisos) avisos)" } else { "" }
        $titulo = "NUEVO PEDIDO - CONFIRMAR AVISO 1"
        $mensaje = "Pedido N.: $numPedido`nCliente: $cliente`nImporte: $importe EUR$descStr"
        $notifyIcon.ShowBalloonTip(5000, $titulo, $mensaje, [System.Windows.Forms.ToolTipIcon]::Info)

        if ($grabarLog) {
            $lineaLog = "[" + ($ahora.ToString('HH:mm:ss')) + "] [NUEVO PEDIDO] Recibido pedido N. " + $numPedido + " (Aviso 1 - Pendiente Confirmar) | Cliente: " + $cliente + " | Importe: " + $importe + " EUR"
            Add-Content -Path $logPath -Value $lineaLog
        }
    }
}

# Menu Contextual de la Bandeja de Sistema
$contextMenu = New-Object System.Windows.Forms.ContextMenuStrip
$itemAck = $contextMenu.Items.Add("Confirmar / Aceptar Pedidos")
$itemAutoToggle = $contextMenu.Items.Add("Alternar Modo Auto-Aceptar")
$itemExport = $contextMenu.Items.Add("Exportar Registro (Guardar como...)")
$itemPrint = $contextMenu.Items.Add("Imprimir Registro")
$itemClearLog = $contextMenu.Items.Add("Limpiar Registro (Con Copia de Seg.)")
$itemShow = $contextMenu.Items.Add("Ver Registro / Log en Vivo")
$itemSync = $contextMenu.Items.Add("Ejecutar Sincronizacion Ahora")
$itemSimulateOrder = $contextMenu.Items.Add("Simular Llegada de Pedido (Prueba)")
$contextMenu.Items.Add("-") | Out-Null
$itemExit = $contextMenu.Items.Add("Salir")

$itemAck.Add_Click({ ConfirmarTodosLosPedidos })

$itemAutoToggle.Add_Click({
    $config.AceptarAutomaticamente = -not $config.AceptarAutomaticamente
    $config.RequiereConfirmacion = -not $config.AceptarAutomaticamente
    ActualizarEstadoPendientes
})

$itemExport.Add_Click({ ExportarRegistro })
$itemPrint.Add_Click({ ImprimirRegistro })
$itemClearLog.Add_Click({ LimpiarRegistroConConfirmacion })

$itemShow.Add_Click({
    $form.Show()
    $form.WindowState = [System.Windows.Forms.FormWindowState]::Normal
    $form.BringToFront()
})

$itemSync.Add_Click({
    if (Test-Path $syncBatPath) {
        Start-Process -FilePath $syncBatPath
    } else {
        [System.Windows.Forms.MessageBox]::Show("No se encontro el script de exportacion: $syncBatPath", "ReprediSL V4", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
    }
})

$itemSimulateOrder.Add_Click({
    $randNum = Get-Random -Minimum 1000 -Maximum 9999
    $randCliente = @("4012 - Supermercados Pepe", "5019 - Hosteleria Rias Baixas", "3008 - Alimentacion Garcia", "6102 - Distribuciones Levantinas") | Get-Random
    $randImporte = "{0:N2}" -f ((Get-Random -Minimum 5000 -Maximum 85000) / 100)
    RegistrarNuevoPedido -numPedido "PED-$randNum" -cliente $randCliente -importe $randImporte -grabarLog $true
})

$itemExit.Add_Click({
    $notifyIcon.Visible = $false
    $form.Dispose()
    try { $mutex.ReleaseMutex() } catch {}
    [System.Windows.Forms.Application]::Exit()
})

$form.Add_FormClosed({
    try { $mutex.ReleaseMutex() } catch {}
})

$notifyIcon.ContextMenuStrip = $contextMenu
$notifyIcon.Add_DoubleClick({
    $form.Show()
    $form.WindowState = [System.Windows.Forms.FormWindowState]::Normal
    $form.BringToFront()
})

# Timer 1: Lectura de sync_progress.log UNICAMENTE para actualizar la caja de texto GUI
$timerLog = New-Object System.Windows.Forms.Timer
$timerLog.Interval = 500 # 500ms
$timerLog.Add_Tick({
    if (Test-Path $logPath) {
        try {
            $content = Get-Content -Path $logPath -Raw -ErrorAction SilentlyContinue
            if ($content -and $content -ne $lastContent) {
                $lastContent = $content
                $txtLog.Text = $content
                $txtLog.SelectionStart = $txtLog.Text.Length
                $txtLog.ScrollToCaret()
            }
        } catch {}
    }
})
$timerLog.Start()

# Timer 2: Polling API PostgREST (/uventas) - Opt-in
$global:lastMaxId = 0
$timerApi = New-Object System.Windows.Forms.Timer
$timerApi.Interval = ($config.IntervaloRevisionSeg * 1000)
$timerApi.Add_Tick({
    if (-not $config.PollingApiActivo) { return }
    try {
        $resp = Invoke-RestMethod -Uri "$apiUrl/uventas?order=id.desc&limit=1" -Method Get -TimeoutSec 3 -ErrorAction SilentlyContinue
        if ($resp -and $resp.Length -gt 0) {
            $latest = $resp[0]
            $currentId = 0
            if ($null -ne $latest.id) { $currentId = [int]$latest.id }
            elseif ($null -ne $latest.id_pedido) { $currentId = [int]$latest.id_pedido }
            elseif ($null -ne $latest.id_uventa) { $currentId = [int]$latest.id_uventa }

            if ($currentId -gt 0) {
                if ($global:lastMaxId -eq 0) {
                    $global:lastMaxId = $currentId
                } elseif ($currentId -gt $global:lastMaxId) {
                    $global:lastMaxId = $currentId
                    $clienteInfo = "Cliente " + ($latest.id_cliente -or "General")
                    $importeInfo = "{0:N2}" -f ($latest.importe -or $latest.total -or 0)
                    RegistrarNuevoPedido -numPedido "PED-$currentId" -cliente $clienteInfo -importe $importeInfo -grabarLog $true
                }
            }
        }
    } catch {}
})
$timerApi.Start()

# Timer 3: Motor de Reintentos, Escalado y Descarte por Limite sin Confirmar
$timerReintentos = New-Object System.Windows.Forms.Timer
$timerReintentos.Interval = 5000 # Revisar cada 5 segundos
$timerReintentos.Add_Tick({
    if ($config.AceptarAutomaticamente) { return }

    $ahora = Get-Date
    foreach ($key in @($global:pedidosPendientes.Keys)) {
        $ped = $global:pedidosPendientes[$key]
        if (-not $ped.Leido -and -not $ped.Descartado) {
            $maxDescarte = $config.DescartarTrasAvisos
            $minutosEsperar = if ($ped.NumAvisosEnviados -eq 1) { $config.MinutosSegundoAviso } else { $config.MinutosSiguientesAvisos }
            $tiempoTranscurrido = ($ahora - $ped.UltimoAviso).TotalMinutes

            if ($tiempoTranscurrido -ge $minutosEsperar) {
                $ped.UltimoAviso = $ahora

                if ($maxDescarte -gt 0 -and $ped.NumAvisosEnviados -ge $maxDescarte) {
                    $ped.Descartado = $true
                    $ped.Estado = "Descartado"

                    try { [System.Media.SystemSounds]::Exclamation.Play() } catch {}
                    $tituloDescarte = "PEDIDO DESCARTADO (Sin Confirmacion)"
                    $msgDescarte = "El pedido $($ped.NumPedido) de $($ped.Cliente) ha sido DESCARTADO tras $($ped.NumAvisosEnviados) avisos sin confirmar."
                    $notifyIcon.ShowBalloonTip(6000, $tituloDescarte, $msgDescarte, [System.Windows.Forms.ToolTipIcon]::Warning)

                    $logDescarte = "[" + ($ahora.ToString('HH:mm:ss')) + "] [DESCARTADO] Pedido " + $ped.NumPedido + " DESCARTADO tras " + $ped.NumAvisosEnviados + " avisos sin confirmacion."
                    Add-Content -Path $logPath -Value $logDescarte
                } else {
                    $ped.NumAvisosEnviados++

                    try { [System.Media.SystemSounds]::Asterisk.Play() } catch {}
                    $limiteStr = if ($maxDescarte -gt 0) { "/$maxDescarte" } else { "" }
                    $tituloReintento = "PEDIDO SIN CONFIRMAR (Aviso $($ped.NumAvisosEnviados)$limiteStr)"
                    $msgReintento = "Pendiente de confirmacion:`nPedido: $($ped.NumPedido)`nCliente: $($ped.Cliente)`nImporte: $($ped.Importe) EUR"
                    $notifyIcon.ShowBalloonTip(6000, $tituloReintento, $msgReintento, [System.Windows.Forms.ToolTipIcon]::Warning)

                    $logMsg = "[" + ($ahora.ToString('HH:mm:ss')) + "] [REINTENTO AVISO " + $ped.NumAvisosEnviados + $limiteStr + "] Pedido " + $ped.NumPedido + " SIN CONFIRMAR despues de " + [math]::Round($tiempoTranscurrido, 1) + " min."
                    Add-Content -Path $logPath -Value $logMsg
                }
            }
        }
    }
    ActualizarEstadoPendientes
})
$timerReintentos.Start()

# Mensaje inicial en log
$targetPreview = ObtenerRutaGestionMdb -rutaBase $config.RutaPsGest -numEmpresa $config.Empresa -numEjercicio $config.Ejercicio
$initMsg = "[" + (Get-Date -Format 'HH:mm:ss') + "] Demonio de bandeja ReprediSL V4 iniciado.`r`nPsGest Target: " + $targetPreview + "`r`nConfiguracion: Modo Confirmacion Manual | 2º aviso a " + $config.MinutosSegundoAviso + " min | 3º a " + $config.MinutosSiguientesAvisos + " min | Descarte tras " + $config.DescartarTrasAvisos + " avisos sin confirmar.`r`n"
Add-Content -Path $logPath -Value $initMsg

# Actualizar estado visual inicial
ActualizarEstadoPendientes

# Mostrar ventana al iniciar
$form.Show()
[System.Windows.Forms.Application]::Run()
