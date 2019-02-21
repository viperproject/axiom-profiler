Param([string]$path=".")

$basePath = Split-Path -Parent $PSCommandPath
$DesktopPath = "$basePath\..\"

if (!(Test-Path -Path logs)) {
    New-Item -ItemType directory -Path logs | Out-Null
}

$files = Get-ChildItem $path\* -Include *.smt2
foreach($file in $files) {
    $baseName = $file.BaseName
    Start-Process -FilePath "$DesktopPath\tools\z3\build\z3.exe" -ArgumentList "TRACE=true PROOF=true TRACE_FILE_NAME=logs\$baseName.log -T:60 $file" -Wait
}