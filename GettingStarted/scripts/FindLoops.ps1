Param([int]$n)

$path=".\out\"

if (!(Test-Path -Path found-loops)) {
    New-Item -ItemType directory -Path found-loops | Out-Null
}

$files = Get-ChildItem $path\* -Include *.loops
$count = 0
foreach ($file in $files) {
    $matches = Import-Csv -Path $file -Header reps,pat | Where-Object {[int]$_.reps -ge $n}
    if ($matches.Count -ge 1) {
        $baseName = $file.BaseName
        Copy-Item "logs\$baseName.log" -Destination "found-loops\$baseName.log" | Out-Null
        $count++
    }
}
Write-Host $count