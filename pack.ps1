param(
  [switch]$SkipSetup
)
$ErrorActionPreference = 'Stop'
$d        = 'C:\Users\Mo\WorkBuddy\2026-10-08-17-50-32'
$app      = 'C:\Users\Mo\Documents\PocketDeck'
$staging  = "$d\build\release-app"
$zip      = "$d\build\app.zip"
$csproj   = "$d\src\PocketDeck.Setup\PocketDeck.Setup.csproj"
$publish  = "$d\src\PocketDeck.Setup\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\PocketDeck.Setup.exe"
$release  = "$d\release\PocketDeck-0.2beta-setup.exe"

if (Get-Process PocketDeck -ErrorAction SilentlyContinue) { throw 'app is running; close it first' }

Write-Host '--- 1/4 stage payload from live app dir ---'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
robocopy $app $staging /E /XD layout artifacts /XF *.pdb /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
$staged = (Get-ChildItem $staging -Recurse -File).Count
Write-Host "staged files: $staged"

Write-Host '--- 2/4 build app.zip (7-Zip, deflate -mx=9) ---'
if (Test-Path $zip) { Remove-Item $zip -Force }
Push-Location $staging
try {
Write-Host '--- 1c/4 prune payload (design-time only / symbol reader / winrt projection) ---'
foreach ($name in @('Microsoft.Windows.SDK.NET.dll','System.Windows.Forms.Design.dll','Microsoft.DiaSymReader.Native.amd64.dll')) {
  Get-ChildItem -Path . -Recurse -File -Filter $name -ErrorAction SilentlyContinue | Remove-Item -Force
}
Get-ChildItem -Path . -Recurse -File -Include '*.pdb' -ErrorAction SilentlyContinue | Remove-Item -Force
  & 7z a -tzip -mx=9 -mm=Deflate -bso0 -bsp0 $zip '.\*' | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "7z failed ($LASTEXITCODE)" }
}
finally { Pop-Location }
Write-Host ("app.zip: {0:N0} bytes" -f (Get-Item $zip).Length)

if (-not $SkipSetup) {
  Write-Host '--- 3/4 publish installer (single file, self-contained) ---'
  dotnet publish $csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -v m --nologo
  if ($LASTEXITCODE -ne 0) { throw "setup publish failed ($LASTEXITCODE)" }
  Write-Host '--- 4/4 copy to release ---'
  Copy-Item $publish $release -Force
}

Write-Host '--- result ---'
Get-Item $zip, $release -ErrorAction SilentlyContinue | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 140
Get-FileHash $release -Algorithm MD5 -ErrorAction SilentlyContinue | Select-Object Hash | Format-Table -AutoSize | Out-String
