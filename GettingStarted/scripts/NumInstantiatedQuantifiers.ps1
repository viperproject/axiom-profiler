Param([string]$path)

$instantiated = Import-Csv -Path $path -Header quant,insts | Select-Object -Skip 3 | Where-Object {([int]$_.insts) -gt 0}

Write-Host $instantiated.Count