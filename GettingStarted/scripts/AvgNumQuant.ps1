Param([string]$path=".\out\")

$files = Get-ChildItem $path\* -Include *.basic
[double]$sum = 0
foreach ($file in $files) {
    $content = Import-Csv -Path $file -Header first,second
    $sum += $content[1].second
}
$avg = $sum / $files.Count
Write-Host $avg