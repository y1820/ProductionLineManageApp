$ErrorActionPreference = "Stop"
$srcRoot = "C:\Users\y1820\Desktop\t\PlantSCADA"
$dstRoot = "C:\Users\y1820\Desktop\t\ProductionLineManageApp"
$utf8Bom = New-Object System.Text.UTF8Encoding $true

function Copy-Tree($from, $to) {
    if (Test-Path $to) {
        Get-ChildItem $to -Force | Where-Object { $_.Name -notin @("bin","obj") } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Force -Path $to | Out-Null
    robocopy $from $to /E /NFL /NDL /NJH /NJS /nc /ns /np /XD bin obj .vs /XF *.csproj *.user *.suo *.dll *.pdb *.cache | Out-Null
}

Copy-Tree "$srcRoot\01_Startup\PlantSCADA.Host" "$dstRoot\01_Startup\ProductionLineManage.Host"
Copy-Tree "$srcRoot\02_Infrastructure\PlantSCADA.Infrastructure" "$dstRoot\02_Infrastructure\ProductionLineManage.Infrastructure"
Copy-Tree "$srcRoot\02_Infrastructure\PlantSCADA.Shared" "$dstRoot\02_Infrastructure\ProductionLineManage.Shared"
Copy-Tree "$srcRoot\03_Core\PlantSCADA.Core" "$dstRoot\03_Core\ProductionLineManage.Core"
Copy-Tree "$srcRoot\04_Services\PlantSCADA.Services" "$dstRoot\04_Services\ProductionLineManage.Services"
Copy-Tree "$srcRoot\05_Modules\DeviceModule" "$dstRoot\05_Modules\DeviceModule"
Copy-Tree "$srcRoot\05_Modules\WorkmanshipModule" "$dstRoot\05_Modules\WorkmanshipModule"
Copy-Tree "$srcRoot\05_Modules\MaterialModule" "$dstRoot\05_Modules\MaterialModule"
Copy-Tree "$srcRoot\05_Modules\ProductionModule" "$dstRoot\05_Modules\ProductionModule"
Copy-Tree "$srcRoot\05_Modules\ReportModule" "$dstRoot\05_Modules\ReportModule"

# leftover old-style Host files
@(
    "$dstRoot\01_Startup\ProductionLineManage.Host\Properties",
    "$dstRoot\01_Startup\ProductionLineManage.Host\packages.config"
) | ForEach-Object { if (Test-Path $_) { Remove-Item $_ -Recurse -Force } }

Get-ChildItem $dstRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\(bin|obj|packages|\.vs)\\' -and
    $_.Extension -match '\.(cs|xaml|json|config)$'
} | ForEach-Object {
    $text = [System.IO.File]::ReadAllText($_.FullName)
    $new = $text.Replace("PlantSCADA", "ProductionLineManage")
    if ($new -ne $text) {
        [System.IO.File]::WriteAllText($_.FullName, $new, $utf8Bom)
    }
}

Write-Output "copy-and-replace done"
