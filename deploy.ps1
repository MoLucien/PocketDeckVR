param(
  [string]$Target = 'C:\Users\Mo\Documents\PocketDeck'
)
$ErrorActionPreference = 'Stop'
$d      = 'C:\Users\Mo\WorkBuddy\2026-10-08-17-50-32'
$built  = "$d\src\VRPhoneScreenOverlay\bin\x64\Release\net10.0-windows10.0.19041.0"
$legacy = 'C:\Users\Mo\Documents\VRPhoneScreenOverlay\app'

if (Get-Process PocketDeck -ErrorAction SilentlyContinue) { throw 'PocketDeck is running; close it first' }
if (Get-Process VRPhoneScreenOverlay -ErrorAction SilentlyContinue) { throw 'legacy app is running; close it first' }

Write-Host "--- 1/4 seed payload from legacy app dir -> $Target ---"
New-Item -ItemType Directory -Path $Target -Force | Out-Null
robocopy $legacy $Target /E /XD layout artifacts /XF 'VRPhoneScreenOverlay*' /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }

Write-Host '--- 1b/4 publish + copy the rebuilt binding tool ---'
$toolProj = "$d\src\PocketDeck.SteamVR.BindingTool\PocketDeck.SteamVR.BindingTool.csproj"
dotnet publish $toolProj -c Release -r win-x64 --self-contained true -v q --nologo | Out-Null
$toolPub = "$d\src\PocketDeck.SteamVR.BindingTool\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
Get-ChildItem $Target -File | Where-Object { $_.Name -like '*BindingTool*' } | Remove-Item -Force
Get-ChildItem $toolPub -File | Where-Object { $_.Name -like 'PocketDeck.SteamVR.BindingTool*' -and $_.Extension -ne '.pdb' } | ForEach-Object {
  Copy-Item $_.FullName $Target -Force
}

Write-Host '--- 2/4 copy new PocketDeck binaries ---'
$files = Get-ChildItem $built -File | Where-Object { $_.Name -like 'PocketDeck*' -and $_.Extension -in '.dll', '.exe', '.json' }
foreach ($f in $files) { Copy-Item $f.FullName $Target -Force }
$copied = ($files | Measure-Object).Count
Write-Host "copied $copied files"

Write-Host '--- 3/4 patch SteamVR manifest identity ---'
$manifest = Join-Path $Target 'manifest.vrmanifest'
$json = Get-Content $manifest -Raw
$json = $json -replace 'local\.spacedraglite\.desktop\.v1', 'local.pocketdeck.desktop.v1'
$json = $json -replace 'VRPhoneScreen Overlay', 'PocketDeck VR'
$json = $json -replace '"binary_path_windows"\s*:\s*"[^"]*"', '"binary_path_windows": "PocketDeck.exe"'
Set-Content -Path $manifest -Value $json -Encoding UTF8

# 绑定文件的 app_key 必须与代码校验一致，否则会被判为「开发默认绑定无效」
Get-ChildItem $Target -Recurse -File -Include *.json, *.vrmanifest | ForEach-Object {
  $content = Get-Content $_.FullName -Raw
  if ($content -match 'spacedraglite') {
    Set-Content -Path $_.FullName -Value ($content -replace 'local\.spacedraglite\.desktop\.v1', 'local.pocketdeck.desktop.v1') -Encoding UTF8
  }
}
$json | Select-String -Pattern 'app_key|binary_path_windows' -AllMatches | ForEach-Object { $_.Line.Trim() }

Write-Host '--- 4/4 flat layout check ---'
$total = (Get-ChildItem $Target -Recurse -File | Measure-Object).Count
Write-Host "files in target: $total"
Get-ChildItem $Target -Filter 'PocketDeck*' | Select-Object Name, Length | Format-Table -AutoSize | Out-String -Width 120
