$files = Get-ChildItem -Path "$PSScriptRoot\..\..\src\Daemon\ReprediTrayDaemon" -Filter "*.cs" -Recurse

$utf8Bom = New-Object System.Text.UTF8Encoding($true)

foreach ($file in $files) {
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    [System.IO.File]::WriteAllText($file.FullName, $content, $utf8Bom)
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $isBom = ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    Write-Output "$($file.Name): BOM Present = $isBom"
}
