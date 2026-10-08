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
$releaseDir = "$d\release"
$release  = ""

if (Get-Process PocketDeck -ErrorAction SilentlyContinue) { throw 'app is running; close it first' }

Write-Host '--- 2/4 stage framework-dependent payload ---'
$stage = "$d\build\payload"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null

# 程序本体：依赖框架发布（不再向负载里塞 124MB 运行时）
dotnet publish "$d\src\VRPhoneScreenOverlay\PocketDeck.csproj" -c Release -r win-x64 --self-contained false -o $stage -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "app publish (framework-dependent) failed ($LASTEXITCODE)" }

# 运行时与调试符号不进负载：它们由目标机的 .NET 10 桌面运行时提供
Get-ChildItem $stage -File -Recurse | Where-Object {
  $_.Name -like 'System.*' -or $_.Name -like 'Microsoft.*' -or
  $_.Name -in @('mscorlib.dll','netstandard.dll','WindowsBase.dll','coreclr.dll','clrjit.dll','hostfxr.dll','hostpolicy.dll','createdump.exe') -or
  $_.Extension -eq '.pdb'
} | Remove-Item -Force

# 厂商资源（publish 不产出，从当前部署目录补齐）
foreach ($item in @('resources','licenses','bindings','action_manifest.json','manifest.vrmanifest','openvr_api.dll','SharpGen.Runtime.COM.dll','app.ico')) {
  $source = Join-Path $app $item
  if (Test-Path $source) { Copy-Item $source $stage -Recurse -Force }
}

# 绑定准备工具：同样依赖框架，与主程序共享运行时
dotnet publish "$d\src\PocketDeck.SteamVR.BindingTool\PocketDeck.SteamVR.BindingTool.csproj" -c Release -r win-x64 --self-contained false -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "binding tool publish failed ($LASTEXITCODE)" }
Get-ChildItem "$d\src\PocketDeck.SteamVR.BindingTool\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish" -File |
  Where-Object { $_.Name -like 'PocketDeck.SteamVR.BindingTool*' -and $_.Extension -ne '.pdb' } |
  ForEach-Object { Copy-Item $_.FullName $stage -Force }

$payloadMB = [math]::Round(((Get-ChildItem $stage -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
$displayVersion = ((Get-Item "$stage\PocketDeck.exe").VersionInfo.ProductVersion -split '\+')[0]
$release = Join-Path $releaseDir ("PocketDeck-$displayVersion-setup.exe")
Write-Host "    payload = $payloadMB MB, version = $displayVersion"

Write-Host '--- 2b/4 build app.zip (installer payload) + app update package ---'
$appPackage = Join-Path (Split-Path $release -Parent) ("PocketDeck-$displayVersion-app.zip")
foreach ($target in @($zip, $appPackage)) {
  if (Test-Path $target) { Remove-Item $target -Force }
  Push-Location $stage
  & 7z a -tzip -mx=9 -mm=Deflate -bso0 -bsp0 $target '.\*' | Out-Null
  $code = $LASTEXITCODE
  Pop-Location
  if ($code -ne 0) { throw "7z failed for $target ($code)" }
  Write-Host ("    {0} = {1} MB" -f (Split-Path $target -Leaf), [math]::Round((Get-Item $target).Length / 1MB, 2))
}

Write-Host '--- 3/4 publish installer (single file, self-contained) ---'
  dotnet publish $csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -v m --nologo
  if ($LASTEXITCODE -ne 0) { throw "setup publish failed ($LASTEXITCODE)" }
Write-Host '--- 4/4 copy installer to release ---'
Copy-Item $publish $release -Force

Write-Host '--- result ---'
Get-Item $zip, $release, $appPackage -ErrorAction SilentlyContinue | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 140
Get-FileHash $release -Algorithm MD5 -ErrorAction SilentlyContinue | Select-Object Hash | Format-Table -AutoSize | Out-String
