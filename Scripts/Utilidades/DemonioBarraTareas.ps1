# Script de Demonio de Bandeja de Sistema (System Tray Daemon) - ReprediSL V4
# Monitoreo visual de errores en ROJO, log especial sync_errors.log y alertas por incremento elevado de incidencias.

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# =========================================================
# PROTECCION DE INSTANCIA UNICA (MUTEX LOCAL DE USUARIO)
# =========================================================
$createdNew = $false
$mutex = New-Object System.Threading.Mutex($true, "Local\ReprediSL_V4_Daemon_Mutex", [ref]$createdNew)
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
$errorLogPath = Join-Path $ProjectRoot "src/Access/sync_errors.log"
$syncBatPath = Join-Path $ProjectRoot "Scripts/Desarrollo/EJECUTAR_EXPORTACION_POSTGRES.bat"
$apiUrl = "http://127.0.0.1:3000"

# =========================================================
# CONFIGURACION DE PSGEST, RUTA MDB, ALERTAS Y ERRORES
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
    MaxErroresAlerta          = 3         # Umbral de incidencias acumuladas tras el cual se lanza ALERTA
    TamanoFuente              = "Grande"  # Pequeno, Mediano, Grande
}

# Persistencia de configuracion UI
$settingsFilePath = Join-Path $ProjectRoot "src\Access\daemon_ui_settings.json"

function CargarConfiguracionUI {
    if (Test-Path $settingsFilePath) {
        try {
            $raw = Get-Content -Path $settingsFilePath -Raw -ErrorAction SilentlyContinue
            if ($raw) {
                $parsed = $raw | ConvertFrom-Json
                if ($parsed.TamanoFuente) { return $parsed.TamanoFuente }
            }
        } catch {}
    }
    return "Grande"
}

function GuardarConfiguracionUI {
    param([string]$tamano)
    try {
        $jsonObj = @{ TamanoFuente = $tamano } | ConvertTo-Json
        Set-Content -Path $settingsFilePath -Value $jsonObj -Force
    } catch {}
}

$config.TamanoFuente = CargarConfiguracionUI

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

# Variables globales de control de errores y pedidos
$global:pedidosPendientes = [ordered]@{}
$global:countErrores = 0
$global:countWarnings = 0
$global:lastMaxId = 0
$global:lastLineCount = 0
$lastContent = ""
$global:recentIncidenciasTimes = [System.Collections.Generic.List[datetime]]::new()

# Asegurar existencia de archivos de log
if (-not (Test-Path $logPath)) {
    New-Item -Path $logPath -ItemType File -Force | Out-Null
}
if (-not (Test-Path $errorLogPath)) {
    New-Item -Path $errorLogPath -ItemType File -Force | Out-Null
}

# Icono de bandeja
$notifyIcon = New-Object System.Windows.Forms.NotifyIcon
$notifyIcon.Icon = [System.Drawing.SystemIcons]::Information
$notifyIcon.Text = "ReprediSL V4 - Demonio de Sincronizacion y Pedidos"
$notifyIcon.Visible = $true

# Ventana Principal de Registro
$form = New-Object System.Windows.Forms.Form
$form.Text = "ReprediSL V4 - Demonio de Pedidos (Alertas de Error en ROJO & Log de Incidencias)"
$form.Size = New-Object System.Drawing.Size(1150, 720)
$form.StartPosition = "CenterScreen"
$form.BackColor = [System.Drawing.Color]::FromArgb(18, 19, 22)

# Panel Superior de Botones y Estado
$topPanel = New-Object System.Windows.Forms.Panel
$topPanel.Dock = "Top"
$topPanel.Height = 115
$topPanel.BackColor = [System.Drawing.Color]::FromArgb(28, 30, 36)

# Boton Confirmar Pedidos
$btnAck = New-Object System.Windows.Forms.Button
$btnAck.Text = " CONFIRMAR PEDIDOS"
$btnAck.Size = New-Object System.Drawing.Size(205, 40)
$btnAck.Location = New-Object System.Drawing.Point(12, 10)
$btnAck.FlatStyle = "Flat"
$btnAck.BackColor = [System.Drawing.Color]::FromArgb(38, 140, 75)
$btnAck.ForeColor = [System.Drawing.Color]::White
$btnAck.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($btnAck)

# Boton Conmutar Auto-Aceptar
$btnAutoToggle = New-Object System.Windows.Forms.Button
$btnAutoToggle.Text = "Modo: CONFIRMACION MANUAL"
$btnAutoToggle.Size = New-Object System.Drawing.Size(240, 40)
$btnAutoToggle.Location = New-Object System.Drawing.Point(227, 10)
$btnAutoToggle.FlatStyle = "Flat"
$btnAutoToggle.BackColor = [System.Drawing.Color]::FromArgb(50, 60, 80)
$btnAutoToggle.ForeColor = [System.Drawing.Color]::White
$btnAutoToggle.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($btnAutoToggle)

# Boton Ver Log de Errores (EN ROJO)
$btnViewErrors = New-Object System.Windows.Forms.Button
$btnViewErrors.Text = " VER ERRORES (0)"
$btnViewErrors.Size = New-Object System.Drawing.Size(195, 40)
$btnViewErrors.Location = New-Object System.Drawing.Point(477, 10)
$btnViewErrors.FlatStyle = "Flat"
$btnViewErrors.BackColor = [System.Drawing.Color]::FromArgb(70, 70, 80)
$btnViewErrors.ForeColor = [System.Drawing.Color]::White
$btnViewErrors.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($btnViewErrors)

# Boton Exportar Log
$btnExport = New-Object System.Windows.Forms.Button
$btnExport.Text = " Exportar Log"
$btnExport.Size = New-Object System.Drawing.Size(130, 40)
$btnExport.Location = New-Object System.Drawing.Point(682, 10)
$btnExport.FlatStyle = "Flat"
$btnExport.BackColor = [System.Drawing.Color]::FromArgb(60, 90, 140)
$btnExport.ForeColor = [System.Drawing.Color]::White
$btnExport.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($btnExport)

# Boton Imprimir Log
$btnPrint = New-Object System.Windows.Forms.Button
$btnPrint.Text = " Imprimir"
$btnPrint.Size = New-Object System.Drawing.Size(115, 40)
$btnPrint.Location = New-Object System.Drawing.Point(822, 10)
$btnPrint.FlatStyle = "Flat"
$btnPrint.BackColor = [System.Drawing.Color]::FromArgb(100, 70, 130)
$btnPrint.ForeColor = [System.Drawing.Color]::White
$btnPrint.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($btnPrint)

# Boton Limpiar Log (con confirmacion)
$btnClear = New-Object System.Windows.Forms.Button
$btnClear.Text = " Limpiar"
$btnClear.Size = New-Object System.Drawing.Size(115, 40)
$btnClear.Location = New-Object System.Drawing.Point(947, 10)
$btnClear.FlatStyle = "Flat"
$btnClear.BackColor = [System.Drawing.Color]::FromArgb(140, 50, 50)
$btnClear.ForeColor = [System.Drawing.Color]::White
$btnClear.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($btnClear)

# Boton Selector de Tamaño de Fuente
$btnSizeToggle = New-Object System.Windows.Forms.Button
$btnSizeToggle.Text = "Fuente: GRANDE"
$btnSizeToggle.Size = New-Object System.Drawing.Size(160, 42)
$btnSizeToggle.Location = New-Object System.Drawing.Point(1070, 10)
$btnSizeToggle.FlatStyle = "Flat"
$btnSizeToggle.BackColor = [System.Drawing.Color]::FromArgb(70, 90, 110)
$btnSizeToggle.ForeColor = [System.Drawing.Color]::White
$btnSizeToggle.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($btnSizeToggle)

# Etiqueta Estado Pedidos Pendientes
$lblPendientes = New-Object System.Windows.Forms.Label
$lblPendientes.Text = "Sin pedidos pendientes"
$lblPendientes.Location = New-Object System.Drawing.Point(12, 58)
$lblPendientes.Size = New-Object System.Drawing.Size(450, 24)
$lblPendientes.ForeColor = [System.Drawing.Color]::FromArgb(220, 220, 220)
$lblPendientes.Font = New-Object System.Drawing.Font("Segoe UI", 10.5, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($lblPendientes)

# Sub-etiqueta 1: Estado de Errores e Incidencias en ROJO
$lblErrorStatus = New-Object System.Windows.Forms.Label
$lblErrorStatus.Text = "Incidencias: 0 errores | 0 advertencias (Sin errores)"
$lblErrorStatus.Location = New-Object System.Drawing.Point(477, 58)
$lblErrorStatus.Size = New-Object System.Drawing.Size(580, 24)
$lblErrorStatus.ForeColor = [System.Drawing.Color]::FromArgb(53, 189, 105)
$lblErrorStatus.Font = New-Object System.Drawing.Font("Segoe UI", 10.5, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($lblErrorStatus)

# Sub-etiqueta 2: Ruta Destino gestion.mdb
$lblRutaTarget = New-Object System.Windows.Forms.Label
$targetPathPreview = ObtenerRutaGestionMdb -rutaBase $config.RutaPsGest -numEmpresa $config.Empresa -numEjercicio $config.Ejercicio
$lblRutaTarget.Text = "Destino ERP PsGest: " + $targetPathPreview
$lblRutaTarget.Location = New-Object System.Drawing.Point(12, 86)
$lblRutaTarget.Size = New-Object System.Drawing.Size(1100, 30)
$lblRutaTarget.ForeColor = [System.Drawing.Color]::FromArgb(100, 180, 240)
$lblRutaTarget.Font = New-Object System.Drawing.Font("Segoe UI", 13, [System.Drawing.FontStyle]::Bold)
$topPanel.Controls.Add($lblRutaTarget)

$form.Controls.Add($topPanel)

# RichTextBox de Log en tiempo real con resaltado en ROJO para errores
$txtLog = New-Object System.Windows.Forms.RichTextBox
$txtLog.Dock = "Fill"
$txtLog.ReadOnly = $true
$txtLog.BackColor = [System.Drawing.Color]::FromArgb(12, 13, 15)
$txtLog.ForeColor = [System.Drawing.Color]::FromArgb(53, 189, 105)
$txtLog.Font = New-Object System.Drawing.Font("Consolas", 12, [System.Drawing.FontStyle]::Bold)

$form.Controls.Add($txtLog)

# Función para agregar texto formateado con color al RichTextBox
function AppendColoredText {
    param(
        [System.Windows.Forms.RichTextBox]$box,
        [string]$text,
        [System.Drawing.Color]$color
    )
    if ([string]::IsNullOrEmpty($text)) { return }
    $box.SelectionStart = $box.TextLength
    $box.SelectionLength = 0
    $box.SelectionColor = $color
    $box.AppendText($text)
    $box.SelectionColor = $box.ForeColor
}

# =========================================================
# FUNCION PARA REGISTRAR Y ALERTAR INCIDENCIAS EN ROJO
# Y DETECTAR SI AUMENTAN MUCHO (SURGE / INCIDENCIAS RAPIDAS)
# =========================================================

function RegistrarIncidenciaSync {
    param(
        [string]$tipo = "ERROR", # ERROR o WARNING
        [string]$mensaje
    )

    $ahora = Get-Date
    $ahoraStr = $ahora.ToString('HH:mm:ss')
    $lineaError = "[$ahoraStr] [$tipo] $mensaje"

    # 1. Guardar en log especial sync_errors.log (Errores y Warnings)
    Add-Content -Path $errorLogPath -Value $lineaError -Encoding UTF8

    # 2. Incrementar contadores globales
    if ($tipo -eq "ERROR") { $global:countErrores++ } else { $global:countWarnings++ }
    $totalIncidencias = $global:countErrores + $global:countWarnings

    # 3. Registrar timestamp para detectar aumentos rapidos (en los ultimos 60 segundos)
    $global:recentIncidenciasTimes.Add($ahora)
    $haceUnMinuto = $ahora.AddSeconds(-60)
    $recientesList = @($global:recentIncidenciasTimes | Where-Object { $_ -ge $haceUnMinuto })
    $global:recentIncidenciasTimes = [System.Collections.Generic.List[datetime]]::new($recientesList)
    $cantReciente = $recientesList.Count

    # 4. Reproducir sonido de aviso de error de Windows
    try { [System.Media.SystemSounds]::Hand.Play() } catch {}

    # 5. Notificacion flotante (Balloon Tip) EN ROJO en la bandeja de sistema
    $tituloBalloon = if ($tipo -eq "ERROR") { "[ERROR] ALERTA DE ERROR EN SINCRONIZACION" } else { "[AVISO] ADVERTENCIA DE SINCRONIZACION" }
    $notifyIcon.ShowBalloonTip(6000, $tituloBalloon, $mensaje, [System.Windows.Forms.ToolTipIcon]::Error)

    # 6. AVISAR SI AUMENTAN MUCHO (DETECCION DE RAFAGA / RITMO ELEVADO)
    if ($cantReciente -ge 3) {
        # Si se producen 3 o más incidencias en un lapso de 60 segundos -> ALERTA CRITICA DE AUMENTO
        try { [System.Media.SystemSounds]::Exclamation.Play() } catch {}
        $msgAumento = "[ALERTA] ALERTA CRITICA DE ERRORES: Incremento rapido de incidencias.`n$cantReciente errores/advertencias en los ultimos 60 segundos.`n`nTotal acumulado: $totalIncidencias ($global:countErrores errores, $global:countWarnings advertencias). Haz clic para revisar sync_errors.log."
        $notifyIcon.ShowBalloonTip(10000, "[ALERTA] AUMENTO ELEVADO DE ERRORES", $msgAumento, [System.Windows.Forms.ToolTipIcon]::Error)
    } elseif ($totalIncidencias -ge $config.MaxErroresAlerta) {
        $msgExceso = "[ALERTA] Acumulado de $totalIncidencias incidencias de sincronizacion ($global:countErrores errores, $global:countWarnings advertencias). Revisa el log especial de errores."
        $notifyIcon.ShowBalloonTip(8000, "[ALERTA] UMBRAL DE ERRORES ALCANZADO", $msgExceso, [System.Windows.Forms.ToolTipIcon]::Error)
    }

    ActualizarEstadoPendientes
}

# Ver ventana de Log de Errores Especial (sync_errors.log) con resaltado y opciones
function MostrarVentanaErrores {
    $errForm = New-Object System.Windows.Forms.Form
    $errForm.Text = "ReprediSL V4 - Log Especial de Errores e Incidencias (sync_errors.log)"
    $errForm.StartPosition = "CenterParent"
    $errForm.BackColor = [System.Drawing.Color]::FromArgb(28, 15, 15)

    $errTop = New-Object System.Windows.Forms.Panel
    $errTop.Dock = "Top"
    $errTop.BackColor = [System.Drawing.Color]::FromArgb(45, 20, 20)

    $lblErrSummary = New-Object System.Windows.Forms.Label
    $lblErrSummary.Text = "Resumen: $global:countErrores Errores | $global:countWarnings Advertencias"
    $lblErrSummary.Location = New-Object System.Drawing.Point(14, 14)
    $lblErrSummary.ForeColor = [System.Drawing.Color]::FromArgb(255, 120, 120)

    $btnClearErrLog = New-Object System.Windows.Forms.Button
    $btnClearErrLog.Text = "Limpiar Log de Errores"
    $btnClearErrLog.FlatStyle = "Flat"
    $btnClearErrLog.BackColor = [System.Drawing.Color]::FromArgb(160, 40, 40)
    $btnClearErrLog.ForeColor = [System.Drawing.Color]::White

    switch ($config.TamanoFuente) {
        "Pequeno" {
            $errForm.Size = New-Object System.Drawing.Size(840, 520)
            $errTop.Height = 46
            $lblErrSummary.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
            $lblErrSummary.Size = New-Object System.Drawing.Size(420, 22)
            $btnClearErrLog.Size = New-Object System.Drawing.Size(160, 30)
            $btnClearErrLog.Location = New-Object System.Drawing.Point(640, 8)
            $btnClearErrLog.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)
        }
        "Mediano" {
            $errForm.Size = New-Object System.Drawing.Size(950, 600)
            $errTop.Height = 54
            $lblErrSummary.Font = New-Object System.Drawing.Font("Segoe UI", 11, [System.Drawing.FontStyle]::Bold)
            $lblErrSummary.Size = New-Object System.Drawing.Size(480, 26)
            $btnClearErrLog.Size = New-Object System.Drawing.Size(185, 34)
            $btnClearErrLog.Location = New-Object System.Drawing.Point(720, 10)
            $btnClearErrLog.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
        }
        default {
            $errForm.Size = New-Object System.Drawing.Size(1100, 680)
            $errTop.Height = 62
            $lblErrSummary.Font = New-Object System.Drawing.Font("Segoe UI", 12.5, [System.Drawing.FontStyle]::Bold)
            $lblErrSummary.Size = New-Object System.Drawing.Size(550, 30)
            $btnClearErrLog.Size = New-Object System.Drawing.Size(210, 38)
            $btnClearErrLog.Location = New-Object System.Drawing.Point(850, 10)
            $btnClearErrLog.Font = New-Object System.Drawing.Font("Segoe UI", 10.5, [System.Drawing.FontStyle]::Bold)
        }
    }

    $errTop.Controls.Add($lblErrSummary)
    $errTop.Controls.Add($btnClearErrLog)
    $errForm.Controls.Add($errTop)

    $txtErrLog = New-Object System.Windows.Forms.RichTextBox
    $txtErrLog.Dock = "Fill"
    $txtErrLog.ReadOnly = $true
    $txtErrLog.BackColor = [System.Drawing.Color]::FromArgb(18, 10, 10)

    switch ($config.TamanoFuente) {
        "Pequeno" { $txtErrLog.Font = New-Object System.Drawing.Font("Consolas", 9.5, [System.Drawing.FontStyle]::Bold) }
        "Mediano" { $txtErrLog.Font = New-Object System.Drawing.Font("Consolas", 11, [System.Drawing.FontStyle]::Bold) }
        default   { $txtErrLog.Font = New-Object System.Drawing.Font("Consolas", 13, [System.Drawing.FontStyle]::Bold) }
    }

    if (Test-Path $errorLogPath) {
        $errContent = Get-Content -Path $errorLogPath -Encoding UTF8 -ErrorAction SilentlyContinue
        if ($errContent) {
            foreach ($line in $errContent) {
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $c = if ($line -match "\[ERROR\]|Error|Fallo") { [System.Drawing.Color]::FromArgb(255, 90, 90) } else { [System.Drawing.Color]::FromArgb(255, 200, 80) }
                AppendColoredText -box $txtErrLog -text ($line + "`n") -color $c
            }
        } else {
            AppendColoredText -box $txtErrLog -text "Sin errores ni advertencias registradas.`n" -color ([System.Drawing.Color]::FromArgb(100, 200, 100))
        }
    }

    $btnClearErrLog.Add_Click({
        Set-Content -Path $errorLogPath -Value "" -Encoding UTF8
        $txtErrLog.Clear()
        AppendColoredText -box $txtErrLog -text "Log especial de errores limpiado.`n" -color ([System.Drawing.Color]::FromArgb(100, 200, 100))
        $global:countErrores = 0
        $global:countWarnings = 0
        ActualizarEstadoPendientes
    })

    $errForm.Controls.Add($txtErrLog)
    $errForm.ShowDialog()
}

$btnViewErrors.Add_Click({ MostrarVentanaErrores })

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
        if (-not $exported) { return }
    }

    # Vaciar archivo de log y pantalla
    Set-Content -Path $logPath -Value ""
    $txtLog.Clear()
    $script:lastContent = ""
    $global:lastLineCount = 0
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

        $logOk = "[" + (Get-Date -Format 'HH:mm:ss') + "] [OK] [GESTION.MDB] Pedido " + $numPedido + " CREADO exitosamente en: " + $targetMdb
        Add-Content -Path $logPath -Value $logOk -Encoding UTF8
    } catch {
        $logNote = "[" + (Get-Date -Format 'HH:mm:ss') + "] [OK] [GESTION.MDB] Pedido " + $pedido.NumPedido + " CONFIRMADO -> Destino preparado: " + ${targetMdb}
        Add-Content -Path $logPath -Value $logNote -Encoding UTF8
        RegistrarIncidenciaSync -tipo "WARNING" -mensaje "No se pudo escribir directamente en ${targetMdb}: $_"
    }
}

# Actualizar contador visual de estado (Incluyendo Estado de Errores en ROJO)
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

    # Actualizar estado de errores en la barra superior en ROJO
    $totalIncidenciasCount = $global:countErrores + $global:countWarnings
    $btnViewErrors.Text = " VER ERRORES (" + $totalIncidenciasCount + ")"
    if ($global:countErrores -gt 0 -or $global:countWarnings -gt 0) {
        $lblErrorStatus.Text = "INCIDENCIAS: " + $global:countErrores + " errores | " + $global:countWarnings + " advertencias"
        $lblErrorStatus.ForeColor = [System.Drawing.Color]::FromArgb(255, 80, 80)
        $btnViewErrors.BackColor = [System.Drawing.Color]::FromArgb(210, 40, 40)
    } else {
        $lblErrorStatus.Text = "Incidencias: 0 errores | 0 advertencias (Sin errores)"
        $lblErrorStatus.ForeColor = [System.Drawing.Color]::FromArgb(53, 189, 105)
        $btnViewErrors.BackColor = [System.Drawing.Color]::FromArgb(70, 70, 80)
    }

    if ($config.AceptarAutomaticamente) {
        $btnAutoToggle.Text = "Modo: AUTO-ACEPTAR ACTIVO"
        $btnAutoToggle.BackColor = [System.Drawing.Color]::FromArgb(30, 120, 180)
    } else {
        $btnAutoToggle.Text = "Modo: CONFIRMACION MANUAL"
        $btnAutoToggle.BackColor = [System.Drawing.Color]::FromArgb(50, 60, 80)
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
    Add-Content -Path $logPath -Value $msg -Encoding UTF8
    ActualizarEstadoPendientes
})

# Reconstruir estado de pedidos desde sync_progress.log al iniciar
function ReconstruirPedidosDesdeLog {
    if (Test-Path $logPath) {
        $lines = Get-Content -Path $logPath -Encoding UTF8 -ErrorAction SilentlyContinue
        if ($lines) {
            foreach ($line in $lines) {
                if ([string]::IsNullOrWhiteSpace($line)) { continue }

                # 1. Capturar Nuevo Pedido
                if ($line -match "\[NUEVO PEDIDO\]\s+Recibido pedido N\.\s+([^\s]+)\s+\(([^)]+)\)\s+\|\s+Cliente:\s+([^|]+)\|\s+Importe:\s+([^\s]+)") {
                    $numPed = $matches[1]
                    $clienteStr = $matches[3].Trim()
                    $impStr = $matches[4].Trim()

                    if (-not $global:pedidosPendientes.Contains($numPed)) {
                        $pedObj = [PSCustomObject]@{
                            NumPedido         = $numPed
                            Cliente           = $clienteStr
                            Importe           = $impStr
                            FechaLlegada      = Get-Date
                            UltimoAviso       = Get-Date
                            NumAvisosEnviados = 1
                            Leido             = $false
                            Descartado        = $false
                            Estado            = "Pendiente"
                        }
                        $global:pedidosPendientes[$numPed] = $pedObj
                    }
                }
                # 2. Capturar Confirmacion de Pedido
                elseif ($line -match "\[CONFIRMADO\]|\[GESTION\.MDB\]") {
                    if ($line -match "(PED-\d+)") {
                        $pKey = $matches[1]
                        if ($global:pedidosPendientes.Contains($pKey)) {
                            $global:pedidosPendientes[$pKey].Leido = $true
                            $global:pedidosPendientes[$pKey].Estado = "Confirmado"
                        }
                    }
                }
                # 3. Capturar Descarte de Pedido
                elseif ($line -match "\[DESCARTADO\]") {
                    if ($line -match "(PED-\d+)") {
                        $pKey = $matches[1]
                        if ($global:pedidosPendientes.Contains($pKey)) {
                            $global:pedidosPendientes[$pKey].Descartado = $true
                            $global:pedidosPendientes[$pKey].Estado = "Descartado"
                        }
                    }
                }
            }
        }
    }
}
function ConfirmarPedidoEspecifico {
    param([string]$numPedido)
    if ($global:pedidosPendientes.Contains($numPedido)) {
        $p = $global:pedidosPendientes[$numPedido]
        if (-not $p.Leido -and -not $p.Descartado) {
            $p.Leido = $true
            $p.Estado = "Confirmado"
            InsertarPedidoEnGestionMdb -pedido $p
            $msg = "[" + (Get-Date -Format 'HH:mm:ss') + "] [CONFIRMADO] El usuario ha ACEPTADO el pedido " + $numPedido + ". Insertado en gestion.mdb."
            Add-Content -Path $logPath -Value $msg -Encoding UTF8
            ActualizarEstadoPendientes
            return $true
        }
    }
    return $false
}

# Descartar un pedido especifico
function DescartarPedidoEspecifico {
    param([string]$numPedido)
    if ($global:pedidosPendientes.Contains($numPedido)) {
        $p = $global:pedidosPendientes[$numPedido]
        if (-not $p.Leido -and -not $p.Descartado) {
            $p.Descartado = $true
            $p.Estado = "Descartado"
            $msg = "[" + (Get-Date -Format 'HH:mm:ss') + "] [DESCARTADO] El usuario ha DESCARTADO el pedido " + $numPedido + "."
            Add-Content -Path $logPath -Value $msg -Encoding UTF8
            ActualizarEstadoPendientes
            return $true
        }
    }
    return $false
}

function ConfirmarTodosLosPedidos {
    $confirmadosCount = 0
    foreach ($key in @($global:pedidosPendientes.Keys)) {
        $p = $global:pedidosPendientes[$key]
        if (-not $p.Leido -and -not $p.Descartado) {
            $p.Leido = $true
            $p.Estado = "Confirmado"
            InsertarPedidoEnGestionMdb -pedido $p
            $confirmadosCount++
        }
    }
    if ($confirmadosCount -gt 0) {
        $msg = "[" + (Get-Date -Format 'HH:mm:ss') + "] [CONFIRMADO] El usuario ha ACEPTADO TODOS los pedidos pendientes ($confirmadosCount pedidos). Insertados en gestion.mdb."
        Add-Content -Path $logPath -Value $msg -Encoding UTF8
        ActualizarEstadoPendientes
    }
}

# Ventana Interactiva para Gestionar y Aceptar Pedidos Pendientes
function MostrarVentanaPedidosPendientes {
    $pForm = New-Object System.Windows.Forms.Form
    $pForm.Text = "ReprediSL V4 - Gestion y Confirmacion de Pedidos Pendientes"
    $pForm.Size = New-Object System.Drawing.Size(980, 580)
    $pForm.StartPosition = "CenterScreen"
    $pForm.TopMost = $true
    $pForm.BackColor = [System.Drawing.Color]::FromArgb(20, 24, 30)

    $fSizeHeader = 10.5
    $fSizeGrid = 10
    if ($config.TamanoFuente -eq "Pequeno") {
        $fSizeHeader = 9.5; $fSizeGrid = 9
    } elseif ($config.TamanoFuente -eq "Grande") {
        $fSizeHeader = 12; $fSizeGrid = 11.5
    }

    $pTop = New-Object System.Windows.Forms.Panel
    $pTop.Dock = "Top"
    $pTop.Height = 48
    $pTop.BackColor = [System.Drawing.Color]::FromArgb(30, 36, 45)

    $lblTitle = New-Object System.Windows.Forms.Label
    $lblTitle.Text = "PEDIDOS ENTRANTES Y PENDIENTES DE CONFIRMACION"
    $lblTitle.Location = New-Object System.Drawing.Point(14, 12)
    $lblTitle.Size = New-Object System.Drawing.Size(650, 26)
    $lblTitle.ForeColor = [System.Drawing.Color]::White
    $lblTitle.Font = New-Object System.Drawing.Font("Segoe UI", $fSizeHeader, [System.Drawing.FontStyle]::Bold)
    $pTop.Controls.Add($lblTitle)

    $grid = New-Object System.Windows.Forms.DataGridView
    $grid.Dock = "Fill"
    $grid.SelectionMode = "FullRowSelect"
    $grid.MultiSelect = $false
    $grid.AllowUserToAddRows = $false
    $grid.AllowUserToDeleteRows = $false
    $grid.ReadOnly = $true
    $grid.AutoSizeColumnsMode = "Fill"
    $grid.BackgroundColor = [System.Drawing.Color]::FromArgb(15, 18, 22)
    $grid.ForeColor = [System.Drawing.Color]::White
    $grid.DefaultCellStyle.BackColor = [System.Drawing.Color]::FromArgb(25, 28, 35)
    $grid.DefaultCellStyle.ForeColor = [System.Drawing.Color]::White
    $grid.DefaultCellStyle.SelectionBackColor = [System.Drawing.Color]::FromArgb(40, 110, 180)
    $grid.DefaultCellStyle.Font = New-Object System.Drawing.Font("Segoe UI", $fSizeGrid, [System.Drawing.FontStyle]::Regular)
    
    # Ajuste explicito de altura y diseño de la barra de cabecera para bajar el grid y mostrar titulos de columna
    $grid.ColumnHeadersHeightSizeMode = [System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode]::DisableResizing
    $grid.ColumnHeadersHeight = 38
    $grid.RowTemplate.Height = 32
    $grid.ColumnHeadersDefaultCellStyle.Font = New-Object System.Drawing.Font("Segoe UI", ($fSizeGrid + 0.5), [System.Drawing.FontStyle]::Bold)
    $grid.ColumnHeadersDefaultCellStyle.BackColor = [System.Drawing.Color]::FromArgb(45, 52, 65)
    $grid.ColumnHeadersDefaultCellStyle.ForeColor = [System.Drawing.Color]::White
    $grid.EnableHeadersVisualStyles = $false

    $grid.Columns.Add("NumPedido", "N. Pedido") | Out-Null
    $grid.Columns.Add("Cliente", "Cliente") | Out-Null
    $grid.Columns.Add("Importe", "Importe (EUR)") | Out-Null
    $grid.Columns.Add("Hora", "Hora Llegada") | Out-Null
    $grid.Columns.Add("Avisos", "Avisos") | Out-Null
    $grid.Columns.Add("Estado", "Estado") | Out-Null

    function CargarDatosGrid {
        $grid.Rows.Clear()
        foreach ($key in $global:pedidosPendientes.Keys) {
            $p = $global:pedidosPendientes[$key]
            $hStr = $p.FechaLlegada.ToString("HH:mm:ss")
            $idx = $grid.Rows.Add($p.NumPedido, $p.Cliente, ($p.Importe + " EUR"), $hStr, $p.NumAvisosEnviados, $p.Estado)
            $row = $grid.Rows[$idx]

            if ($p.Estado -eq "Confirmado" -or $p.Estado -eq "Auto-Aceptado") {
                $row.DefaultCellStyle.ForeColor = [System.Drawing.Color]::FromArgb(80, 220, 255)
            } elseif ($p.Estado -eq "Descartado") {
                $row.DefaultCellStyle.ForeColor = [System.Drawing.Color]::FromArgb(180, 180, 180)
            } else {
                $row.DefaultCellStyle.ForeColor = [System.Drawing.Color]::FromArgb(255, 230, 80)
            }
        }
    }

    $pBottom = New-Object System.Windows.Forms.Panel
    $pBottom.Dock = "Bottom"
    $pBottom.Height = 65
    $pBottom.BackColor = [System.Drawing.Color]::FromArgb(30, 36, 45)

    $btnAckSel = New-Object System.Windows.Forms.Button
    $btnAckSel.Text = " ACEPTAR SELECCIONADO"
    $btnAckSel.Size = New-Object System.Drawing.Size(220, 42)
    $btnAckSel.Location = New-Object System.Drawing.Point(14, 12)
    $btnAckSel.FlatStyle = "Flat"
    $btnAckSel.BackColor = [System.Drawing.Color]::FromArgb(38, 140, 75)
    $btnAckSel.ForeColor = [System.Drawing.Color]::White
    $btnAckSel.Font = New-Object System.Drawing.Font("Segoe UI", $fSizeGrid, [System.Drawing.FontStyle]::Bold)
    $pBottom.Controls.Add($btnAckSel)

    $btnAckAll = New-Object System.Windows.Forms.Button
    $btnAckAll.Text = " ACEPTAR TODOS LOS PENDIENTES"
    $btnAckAll.Size = New-Object System.Drawing.Size(270, 42)
    $btnAckAll.Location = New-Object System.Drawing.Point(245, 12)
    $btnAckAll.FlatStyle = "Flat"
    $btnAckAll.BackColor = [System.Drawing.Color]::FromArgb(45, 110, 160)
    $btnAckAll.ForeColor = [System.Drawing.Color]::White
    $btnAckAll.Font = New-Object System.Drawing.Font("Segoe UI", $fSizeGrid, [System.Drawing.FontStyle]::Bold)
    $pBottom.Controls.Add($btnAckAll)

    $btnDiscSel = New-Object System.Windows.Forms.Button
    $btnDiscSel.Text = " DESCARTAR SELECCIONADO"
    $btnDiscSel.Size = New-Object System.Drawing.Size(220, 42)
    $btnDiscSel.Location = New-Object System.Drawing.Point(525, 12)
    $btnDiscSel.FlatStyle = "Flat"
    $btnDiscSel.BackColor = [System.Drawing.Color]::FromArgb(160, 50, 50)
    $btnDiscSel.ForeColor = [System.Drawing.Color]::White
    $btnDiscSel.Font = New-Object System.Drawing.Font("Segoe UI", $fSizeGrid, [System.Drawing.FontStyle]::Bold)
    $pBottom.Controls.Add($btnDiscSel)

    function ActualizarEstadoBotones {
        $hayPendientes = $false
        foreach ($k in $global:pedidosPendientes.Keys) {
            $pObj = $global:pedidosPendientes[$k]
            if (-not $pObj.Leido -and -not $pObj.Descartado) {
                $hayPendientes = $true
                break
            }
        }

        if ($hayPendientes) {
            $btnAckAll.Enabled = $true
            $btnAckAll.BackColor = [System.Drawing.Color]::FromArgb(45, 110, 160)
            $btnAckAll.ForeColor = [System.Drawing.Color]::White
        } else {
            $btnAckAll.Enabled = $false
            $btnAckAll.BackColor = [System.Drawing.Color]::FromArgb(50, 55, 65)
            $btnAckAll.ForeColor = [System.Drawing.Color]::FromArgb(120, 125, 135)
        }

        if ($grid.SelectedRows.Count -gt 0) {
            $numPed = $grid.SelectedRows[0].Cells["NumPedido"].Value
            if ($global:pedidosPendientes.Contains($numPed)) {
                $p = $global:pedidosPendientes[$numPed]
                if (-not $p.Leido -and -not $p.Descartado) {
                    $btnAckSel.Enabled = $true
                    $btnAckSel.BackColor = [System.Drawing.Color]::FromArgb(38, 140, 75)
                    $btnAckSel.ForeColor = [System.Drawing.Color]::White

                    $btnDiscSel.Enabled = $true
                    $btnDiscSel.BackColor = [System.Drawing.Color]::FromArgb(160, 50, 50)
                    $btnDiscSel.ForeColor = [System.Drawing.Color]::White
                } else {
                    $btnAckSel.Enabled = $false
                    $btnAckSel.BackColor = [System.Drawing.Color]::FromArgb(50, 55, 65)
                    $btnAckSel.ForeColor = [System.Drawing.Color]::FromArgb(120, 125, 135)

                    $btnDiscSel.Enabled = $false
                    $btnDiscSel.BackColor = [System.Drawing.Color]::FromArgb(50, 55, 65)
                    $btnDiscSel.ForeColor = [System.Drawing.Color]::FromArgb(120, 125, 135)
                }
            } else {
                $btnAckSel.Enabled = $false
                $btnAckSel.BackColor = [System.Drawing.Color]::FromArgb(50, 55, 65)
                $btnAckSel.ForeColor = [System.Drawing.Color]::FromArgb(120, 125, 135)

                $btnDiscSel.Enabled = $false
                $btnDiscSel.BackColor = [System.Drawing.Color]::FromArgb(50, 55, 65)
                $btnDiscSel.ForeColor = [System.Drawing.Color]::FromArgb(120, 125, 135)
            }
        } else {
            $btnAckSel.Enabled = $false
            $btnAckSel.BackColor = [System.Drawing.Color]::FromArgb(50, 55, 65)
            $btnAckSel.ForeColor = [System.Drawing.Color]::FromArgb(120, 125, 135)

            $btnDiscSel.Enabled = $false
            $btnDiscSel.BackColor = [System.Drawing.Color]::FromArgb(50, 55, 65)
            $btnDiscSel.ForeColor = [System.Drawing.Color]::FromArgb(120, 125, 135)
        }
    }

    CargarDatosGrid
    ActualizarEstadoBotones

    $grid.Add_SelectionChanged({ ActualizarEstadoBotones })

    $btnAckSel.Add_Click({
        if ($grid.SelectedRows.Count -gt 0) {
            $numPed = $grid.SelectedRows[0].Cells["NumPedido"].Value
            $ok = ConfirmarPedidoEspecifico -numPedido $numPed
            if ($ok) {
                CargarDatosGrid
                ActualizarEstadoBotones
                RecargarYRefrescarLog
            }
        }
    })

    $btnAckAll.Add_Click({
        ConfirmarTodosLosPedidos
        CargarDatosGrid
        ActualizarEstadoBotones
        RecargarYRefrescarLog
    })

    $btnDiscSel.Add_Click({
        if ($grid.SelectedRows.Count -gt 0) {
            $numPed = $grid.SelectedRows[0].Cells["NumPedido"].Value
            $ok = DescartarPedidoEspecifico -numPedido $numPed
            if ($ok) {
                CargarDatosGrid
                ActualizarEstadoBotones
                RecargarYRefrescarLog
            }
        }
    })

    $pForm.Controls.Add($pTop)
    $pForm.Controls.Add($pBottom)
    $pForm.Controls.Add($grid)
    $pForm.ShowDialog()
}

$btnAck.Add_Click({ MostrarVentanaPedidosPendientes })

# Prevenir cierre al minimizar o cerrar ventana principal (mantener en bandeja)
$form.Add_FormClosing({
    param($sender, $e)
    if ($e.CloseReason -eq [System.Windows.Forms.CloseReason]::UserClosing) {
        $e.Cancel = $true
        $form.Hide()
        $notifyIcon.ShowBalloonTip(2000, "ReprediSL V4", "El demonio sigue activo en la barra de tareas.", [System.Windows.Forms.ToolTipIcon]::Info)
    }
})

# Cargar texto inicial del log en la caja con formato de colores
if (Test-Path $logPath) {
    $existingRaw = Get-Content -Path $logPath -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
    if ($existingRaw) {
        $lastContent = $existingRaw
        $linesArr = $existingRaw.Split("`n")
        $txtLog.Clear()
        foreach ($lRaw in $linesArr) {
            $line = $lRaw.TrimEnd("`r")
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $lineColor = [System.Drawing.Color]::FromArgb(53, 189, 105)
            if ($line -match "\[ERROR\]|Fallo|Excepcion") {
                $lineColor = [System.Drawing.Color]::FromArgb(255, 75, 75)
            } elseif ($line -match "\[AVISO\]|\[WARNING\]|\[WARN\]") {
                $lineColor = [System.Drawing.Color]::FromArgb(255, 190, 60)
            } elseif ($line -match "\[NUEVO PEDIDO\]") {
                $lineColor = [System.Drawing.Color]::FromArgb(255, 230, 80) # AMARILLO VIVO
            } elseif ($line -match "\[CONFIRMADO\]|\[AUTO-ACEPTADO\]|\[GESTION\.MDB\]") {
                $lineColor = [System.Drawing.Color]::FromArgb(80, 220, 255) # CIAN / TURQUESA
            } elseif ($line -match "\[CONFIG\]|PsGest|Destino|Configuracion|iniciado") {
                $lineColor = [System.Drawing.Color]::FromArgb(100, 180, 240) # AZUL
            }
            AppendColoredText -box $txtLog -text ($line + "`n") -color $lineColor
        }
        $global:lastLineCount = $linesArr.Length
        $txtLog.SelectionStart = $txtLog.TextLength
        $txtLog.ScrollToCaret()
    }
}

# Al hacer clic en el globo flotante de notificacion -> Abrir gestor de pedidos
$notifyIcon.Add_BalloonTipClicked({
    MostrarVentanaPedidosPendientes
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
            Add-Content -Path $logPath -Value $lineaLog -Encoding UTF8
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
            Add-Content -Path $logPath -Value $lineaLog -Encoding UTF8
        }
    }
}

# Menu Contextual de la Bandeja de Sistema
$contextMenu = New-Object System.Windows.Forms.ContextMenuStrip
$contextMenu.Font = New-Object System.Drawing.Font("Segoe UI", 11, [System.Drawing.FontStyle]::Regular)
$itemAck = $contextMenu.Items.Add("Gestionar / Aceptar Pedidos Pendientes")
$itemAutoToggle = $contextMenu.Items.Add("Alternar Modo Auto-Aceptar")
$itemErrors = $contextMenu.Items.Add("Ver Log Especial de Errores (sync_errors.log)")
$itemExport = $contextMenu.Items.Add("Exportar Registro (Guardar como...)")
$itemPrint = $contextMenu.Items.Add("Imprimir Registro")
$itemClearLog = $contextMenu.Items.Add("Limpiar Registro (Con Copia de Seg.)")

# Submenu de Tamaño de Interfaz
$itemSizeMenu = New-Object System.Windows.Forms.ToolStripMenuItem("Tamano de Letra / Interfaz")
$itemSizeSmall = New-Object System.Windows.Forms.ToolStripMenuItem("Pequeno (100%)")
$itemSizeMedium = New-Object System.Windows.Forms.ToolStripMenuItem("Mediano (125%)")
$itemSizeLarge = New-Object System.Windows.Forms.ToolStripMenuItem("Grande (150%)")
$itemSizeMenu.DropDownItems.Add($itemSizeSmall) | Out-Null
$itemSizeMenu.DropDownItems.Add($itemSizeMedium) | Out-Null
$itemSizeMenu.DropDownItems.Add($itemSizeLarge) | Out-Null
$contextMenu.Items.Add($itemSizeMenu) | Out-Null

$itemShow = $contextMenu.Items.Add("Ver Registro / Log en Vivo")
$itemSync = $contextMenu.Items.Add("Ejecutar Sincronizacion Ahora")
$itemSimulateOrder = $contextMenu.Items.Add("Simular Llegada de Pedido (Prueba)")
$itemSimulateError = $contextMenu.Items.Add("Simular Error de Sync (Prueba Alerta Rojo)")
$itemSimulateSurge = $contextMenu.Items.Add("Simular Rafaga de Errores (Prueba Aumento)")
$contextMenu.Items.Add("-") | Out-Null
$itemExit = $contextMenu.Items.Add("Salir")

$itemAck.Add_Click({ MostrarVentanaPedidosPendientes })

$itemAutoToggle.Add_Click({
    $config.AceptarAutomaticamente = -not $config.AceptarAutomaticamente
    $config.RequiereConfirmacion = -not $config.AceptarAutomaticamente
    ActualizarEstadoPendientes
})

$itemErrors.Add_Click({ MostrarVentanaErrores })
$itemExport.Add_Click({ ExportarRegistro })
$itemPrint.Add_Click({ ImprimirRegistro })
$itemSizeSmall.Add_Click({ AplicarTamanoFuente -nivel "Pequeno" })
$itemSizeMedium.Add_Click({ AplicarTamanoFuente -nivel "Mediano" })
$itemSizeLarge.Add_Click({ AplicarTamanoFuente -nivel "Grande" })

$btnSizeToggle.Add_Click({
    switch ($config.TamanoFuente) {
        "Pequeno" { AplicarTamanoFuente -nivel "Mediano" }
        "Mediano" { AplicarTamanoFuente -nivel "Grande" }
        default   { AplicarTamanoFuente -nivel "Pequeno" }
    }
})

function AplicarTamanoFuente {
    param(
        [string]$nivel = "Grande"
    )

    $config.TamanoFuente = $nivel
    GuardarConfiguracionUI -tamano $nivel

    if ($null -ne $itemSizeSmall) { $itemSizeSmall.Checked = ($nivel -eq "Pequeno") }
    if ($null -ne $itemSizeMedium) { $itemSizeMedium.Checked = ($nivel -eq "Mediano") }
    if ($null -ne $itemSizeLarge) { $itemSizeLarge.Checked = ($nivel -eq "Grande") }

    switch ($nivel) {
        "Pequeno" {
            $form.Size = New-Object System.Drawing.Size(980, 630)
            $topPanel.Height = 92

            $btnAck.Size = New-Object System.Drawing.Size(165, 32)
            $btnAck.Location = New-Object System.Drawing.Point(10, 8)
            $btnAck.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)

            $btnAutoToggle.Size = New-Object System.Drawing.Size(195, 32)
            $btnAutoToggle.Location = New-Object System.Drawing.Point(180, 8)
            $btnAutoToggle.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)

            $btnViewErrors.Size = New-Object System.Drawing.Size(145, 32)
            $btnViewErrors.Location = New-Object System.Drawing.Point(380, 8)
            $btnViewErrors.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)

            $btnExport.Size = New-Object System.Drawing.Size(100, 32)
            $btnExport.Location = New-Object System.Drawing.Point(530, 8)
            $btnExport.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)

            $btnPrint.Size = New-Object System.Drawing.Size(85, 32)
            $btnPrint.Location = New-Object System.Drawing.Point(635, 8)
            $btnPrint.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)

            $btnClear.Size = New-Object System.Drawing.Size(85, 32)
            $btnClear.Location = New-Object System.Drawing.Point(725, 8)
            $btnClear.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)

            $btnSizeToggle.Size = New-Object System.Drawing.Size(135, 32)
            $btnSizeToggle.Location = New-Object System.Drawing.Point(815, 8)
            $btnSizeToggle.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)

            $lblPendientes.Location = New-Object System.Drawing.Point(10, 44)
            $lblPendientes.Size = New-Object System.Drawing.Size(360, 18)
            $lblPendientes.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)

            $lblErrorStatus.Location = New-Object System.Drawing.Point(380, 44)
            $lblErrorStatus.Size = New-Object System.Drawing.Size(450, 18)
            $lblErrorStatus.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)

            $lblRutaTarget.Location = New-Object System.Drawing.Point(10, 64)
            $lblRutaTarget.Size = New-Object System.Drawing.Size(950, 22)
            $lblRutaTarget.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)

            $txtLog.Font = New-Object System.Drawing.Font("Consolas", 9.5, [System.Drawing.FontStyle]::Bold)
            $contextMenu.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Regular)
            $btnSizeToggle.Text = "Fuente: PEQUENO"
        }
        "Mediano" {
            $form.Size = New-Object System.Drawing.Size(1120, 690)
            $topPanel.Height = 110

            $btnAck.Size = New-Object System.Drawing.Size(190, 36)
            $btnAck.Location = New-Object System.Drawing.Point(10, 9)
            $btnAck.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)

            $btnAutoToggle.Size = New-Object System.Drawing.Size(220, 36)
            $btnAutoToggle.Location = New-Object System.Drawing.Point(205, 9)
            $btnAutoToggle.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)

            $btnViewErrors.Size = New-Object System.Drawing.Size(170, 36)
            $btnViewErrors.Location = New-Object System.Drawing.Point(430, 9)
            $btnViewErrors.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)

            $btnExport.Size = New-Object System.Drawing.Size(115, 36)
            $btnExport.Location = New-Object System.Drawing.Point(605, 9)
            $btnExport.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)

            $btnPrint.Size = New-Object System.Drawing.Size(100, 36)
            $btnPrint.Location = New-Object System.Drawing.Point(725, 9)
            $btnPrint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)

            $btnClear.Size = New-Object System.Drawing.Size(100, 36)
            $btnClear.Location = New-Object System.Drawing.Point(830, 9)
            $btnClear.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)

            $btnSizeToggle.Size = New-Object System.Drawing.Size(145, 36)
            $btnSizeToggle.Location = New-Object System.Drawing.Point(935, 9)
            $btnSizeToggle.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)

            $lblPendientes.Location = New-Object System.Drawing.Point(10, 50)
            $lblPendientes.Size = New-Object System.Drawing.Size(410, 20)
            $lblPendientes.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)

            $lblErrorStatus.Location = New-Object System.Drawing.Point(430, 50)
            $lblErrorStatus.Size = New-Object System.Drawing.Size(520, 20)
            $lblErrorStatus.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)

            $lblRutaTarget.Location = New-Object System.Drawing.Point(10, 76)
            $lblRutaTarget.Size = New-Object System.Drawing.Size(1080, 26)
            $lblRutaTarget.Font = New-Object System.Drawing.Font("Segoe UI", 11.5, [System.Drawing.FontStyle]::Bold)

            $txtLog.Font = New-Object System.Drawing.Font("Consolas", 11, [System.Drawing.FontStyle]::Bold)
            $contextMenu.Font = New-Object System.Drawing.Font("Segoe UI", 10.5, [System.Drawing.FontStyle]::Regular)
            $btnSizeToggle.Text = "Fuente: MEDIANO"
        }
        "Grande" {
            $form.Size = New-Object System.Drawing.Size(1280, 770)
            $topPanel.Height = 125

            $btnAck.Size = New-Object System.Drawing.Size(210, 42)
            $btnAck.Location = New-Object System.Drawing.Point(12, 10)
            $btnAck.Font = New-Object System.Drawing.Font("Segoe UI", 10.5, [System.Drawing.FontStyle]::Bold)

            $btnAutoToggle.Size = New-Object System.Drawing.Size(245, 42)
            $btnAutoToggle.Location = New-Object System.Drawing.Point(227, 10)
            $btnAutoToggle.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)

            $btnViewErrors.Size = New-Object System.Drawing.Size(195, 42)
            $btnViewErrors.Location = New-Object System.Drawing.Point(477, 10)
            $btnViewErrors.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)

            $btnExport.Size = New-Object System.Drawing.Size(130, 42)
            $btnExport.Location = New-Object System.Drawing.Point(677, 10)
            $btnExport.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)

            $btnPrint.Size = New-Object System.Drawing.Size(115, 42)
            $btnPrint.Location = New-Object System.Drawing.Point(812, 10)
            $btnPrint.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)

            $btnClear.Size = New-Object System.Drawing.Size(115, 42)
            $btnClear.Location = New-Object System.Drawing.Point(932, 10)
            $btnClear.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)

            $btnSizeToggle.Size = New-Object System.Drawing.Size(160, 42)
            $btnSizeToggle.Location = New-Object System.Drawing.Point(1052, 10)
            $btnSizeToggle.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)

            $lblPendientes.Location = New-Object System.Drawing.Point(12, 58)
            $lblPendientes.Size = New-Object System.Drawing.Size(450, 24)
            $lblPendientes.Font = New-Object System.Drawing.Font("Segoe UI", 11, [System.Drawing.FontStyle]::Bold)

            $lblErrorStatus.Location = New-Object System.Drawing.Point(477, 58)
            $lblErrorStatus.Size = New-Object System.Drawing.Size(580, 24)
            $lblErrorStatus.Font = New-Object System.Drawing.Font("Segoe UI", 10.5, [System.Drawing.FontStyle]::Bold)

            $lblRutaTarget.Location = New-Object System.Drawing.Point(12, 88)
            $lblRutaTarget.Size = New-Object System.Drawing.Size(1200, 30)
            $lblRutaTarget.Font = New-Object System.Drawing.Font("Segoe UI", 13, [System.Drawing.FontStyle]::Bold)

            $txtLog.Font = New-Object System.Drawing.Font("Consolas", 13, [System.Drawing.FontStyle]::Bold)
            $contextMenu.Font = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Regular)
            $btnSizeToggle.Text = "Fuente: GRANDE"
        }
    }

    RecargarYRefrescarLog
}

function RecargarYRefrescarLog {
    if (Test-Path $logPath) {
        $existingRaw = Get-Content -Path $logPath -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
        if ($existingRaw) {
            $script:lastContent = $existingRaw
            $linesArr = $existingRaw.Split("`n")
            $txtLog.Clear()
            foreach ($lRaw in $linesArr) {
                $line = $lRaw.TrimEnd("`r")
                if ([string]::IsNullOrWhiteSpace($line)) { continue }

                $lineColor = [System.Drawing.Color]::FromArgb(53, 189, 105)
                if ($line -match "\[ERROR\]|Fallo|Excepcion") {
                    $lineColor = [System.Drawing.Color]::FromArgb(255, 75, 75)
                } elseif ($line -match "\[AVISO\]|\[WARNING\]|\[WARN\]") {
                    $lineColor = [System.Drawing.Color]::FromArgb(255, 190, 60)
                } elseif ($line -match "\[NUEVO PEDIDO\]") {
                    $lineColor = [System.Drawing.Color]::FromArgb(255, 230, 80)
                } elseif ($line -match "\[CONFIRMADO\]|\[AUTO-ACEPTADO\]|\[GESTION\.MDB\]") {
                    $lineColor = [System.Drawing.Color]::FromArgb(80, 220, 255)
                } elseif ($line -match "\[CONFIG\]|PsGest|Destino|Configuracion|iniciado") {
                    $lineColor = [System.Drawing.Color]::FromArgb(100, 180, 240)
                }
                AppendColoredText -box $txtLog -text ($line + "`n") -color $lineColor
            }
            $global:lastLineCount = $linesArr.Length
            $txtLog.SelectionStart = $txtLog.TextLength
            $txtLog.ScrollToCaret()
        }
    }
}

$itemShow.Add_Click({
    RecargarYRefrescarLog
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

$itemSimulateError.Add_Click({
    $ahora = Get-Date -Format 'HH:mm:ss'
    $msgErr = "[$ahora] [ERROR] Fallo de conexion ODBC/PostgreSQL: Timeout alcanzado en la consulta de exportacion."
    Add-Content -Path $logPath -Value $msgErr
})

$itemSimulateSurge.Add_Click({
    $ahora = Get-Date -Format 'HH:mm:ss'
    Add-Content -Path $logPath -Value "[$ahora] [ERROR] Fallo en la tabla clientes (Error de clave duplicada)."
    Add-Content -Path $logPath -Value "[$ahora] [WARNING] Tiempo de respuesta elevado en servidor PostgreSQL (>3000ms)."
    Add-Content -Path $logPath -Value "[$ahora] [ERROR] Desconexion inesperada del controlador Jet.OLEDB."
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
    RecargarYRefrescarLog
    $form.Show()
    $form.WindowState = [System.Windows.Forms.FormWindowState]::Normal
    $form.BringToFront()
})

# Timer 1: Lectura de sync_progress.log y DETECCION DE ERRORES/WARNINGS PARA AVISO EN ROJO Y LOG ESPECIAL
$timerLog = New-Object System.Windows.Forms.Timer
$timerLog.Interval = 500 # 500ms
$timerLog.Add_Tick({
    if (Test-Path $logPath) {
        try {
            $content = Get-Content -Path $logPath -Raw -ErrorAction SilentlyContinue
            if ($content -and $content -ne $lastContent) {
                $lastContent = $content

                # Inspeccion de nuevas lineas para capturar Errores, Advertencias y Pedidos
                $lines = $content.Split("`n")

                # Si el archivo se limpio o reinicio, resetear contador de lineas
                if ($lines.Length -lt $global:lastLineCount) {
                    $global:lastLineCount = 0
                    $txtLog.Clear()
                }

                if ($lines.Length -gt $global:lastLineCount) {
                    for ($i = $global:lastLineCount; $i -lt $lines.Length; $i++) {
                        $line = $lines[$i].TrimEnd("`r")
                        if ([string]::IsNullOrWhiteSpace($line)) { continue }

                        $lineColor = [System.Drawing.Color]::FromArgb(53, 189, 105) # Verde por defecto
                        if ($line -match "\[ERROR\]") {
                            $lineColor = [System.Drawing.Color]::FromArgb(255, 75, 75) # ROJO
                            RegistrarIncidenciaSync -tipo "ERROR" -mensaje $line
                        } elseif ($line -match "\[AVISO\]|\[WARNING\]|\[WARN\]") {
                            $lineColor = [System.Drawing.Color]::FromArgb(255, 190, 60) # NARANJA
                            RegistrarIncidenciaSync -tipo "WARNING" -mensaje $line
                        } elseif ($line -match "\[NUEVO PEDIDO\]") {
                            $lineColor = [System.Drawing.Color]::FromArgb(255, 230, 80) # AMARILLO VIVO
                        } elseif ($line -match "\[CONFIRMADO\]|\[AUTO-ACEPTADO\]|\[GESTION\.MDB\]") {
                            $lineColor = [System.Drawing.Color]::FromArgb(80, 220, 255) # CIAN / TURQUESA
                        } elseif ($line -match "\[CONFIG\]|PsGest|Destino|Configuracion|iniciado") {
                            $lineColor = [System.Drawing.Color]::FromArgb(100, 180, 240) # AZUL
                        }

                        AppendColoredText -box $txtLog -text ($line + "`n") -color $lineColor
                    }
                    $global:lastLineCount = $lines.Length
                    $txtLog.SelectionStart = $txtLog.TextLength
                    $txtLog.ScrollToCaret()
                }
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
                    Add-Content -Path $logPath -Value $logDescarte -Encoding UTF8
                } else {
                    $ped.NumAvisosEnviados++

                    try { [System.Media.SystemSounds]::Asterisk.Play() } catch {}
                    $limiteStr = if ($maxDescarte -gt 0) { "/$maxDescarte" } else { "" }
                    $tituloReintento = "PEDIDO SIN CONFIRMAR (Aviso $($ped.NumAvisosEnviados)$limiteStr)"
                    $msgReintento = "Pendiente de confirmacion:`nPedido: $($ped.NumPedido)`nCliente: $($ped.Cliente)`nImporte: $($ped.Importe) EUR"
                    $notifyIcon.ShowBalloonTip(6000, $tituloReintento, $msgReintento, [System.Windows.Forms.ToolTipIcon]::Warning)

                    $logMsg = "[" + ($ahora.ToString('HH:mm:ss')) + "] [REINTENTO AVISO " + $ped.NumAvisosEnviados + $limiteStr + "] Pedido " + $ped.NumPedido + " SIN CONFIRMAR despues de " + [math]::Round($tiempoTranscurrido, 1) + " min."
                    Add-Content -Path $logPath -Value $logMsg -Encoding UTF8
                }
            }
        }
    }
    ActualizarEstadoPendientes
})
$timerReintentos.Start()

# Mensaje inicial en log
$targetPreview = ObtenerRutaGestionMdb -rutaBase $config.RutaPsGest -numEmpresa $config.Empresa -numEjercicio $config.Ejercicio
$initMsg = "[" + (Get-Date -Format 'HH:mm:ss') + "] Demonio de bandeja ReprediSL V4 iniciado.`r`nPsGest Target: " + $targetPreview + "`r`nConfiguracion: Modo Confirmacion Manual | 2o aviso a " + $config.MinutosSegundoAviso + " min | 3er aviso a " + $config.MinutosSiguientesAvisos + " min | Descarte tras " + $config.DescartarTrasAvisos + " avisos sin confirmar.`r`n"
Add-Content -Path $logPath -Value $initMsg -Encoding UTF8

# Reconstruir historial de pedidos desde el log
ReconstruirPedidosDesdeLog

# Actualizar estado visual inicial
ActualizarEstadoPendientes

# Aplicar tamaño de fuente guardado o por defecto
AplicarTamanoFuente -nivel $config.TamanoFuente

# Mostrar ventana al iniciar y traer al frente
$form.Show()
$form.WindowState = [System.Windows.Forms.FormWindowState]::Normal
$form.Activate()
$form.BringToFront()
[System.Windows.Forms.Application]::Run()
