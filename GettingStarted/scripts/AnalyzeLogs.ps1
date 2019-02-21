Param([string]$path=".")

$basePath = Split-Path -Parent $PSCommandPath
$DesktopPath = "$basePath\..\"

if (!(Test-Path -Path out)) {
    New-Item -ItemType directory -Path out | Out-Null
}

if (!(Test-Path -Path looping)) {
    New-Item -ItemType directory -Path looping | Out-Null
}

$files = Get-ChildItem $path\* -Include *.log
foreach($file in $files) {
    $baseName = $file.BaseName
    Start-Process -FilePath "$DesktopPath\tools\axiom-profiler\bin\Release\AxiomProfiler.exe" -ArgumentList "/loops:40 /showNumChecks /showQuantStatistics /autoQuit /outPrefix:out\$baseName /l:$path\$baseName.log" -Wait
    
    if (Test-Path -Path "out\$baseName.loops") {
        $matches = Import-Csv -Path "out\$baseName.loops" -Header reps,pat | Where-Object {[int]$_.reps -ge 10}
        if ($matches.Count -ge 1) {
            Copy-Item -Path $file -Destination "looping\$baseName.log" | Out-Null
        }
    }
}