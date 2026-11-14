$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testOutput = Join-Path $env:TEMP ('RestaurantBoostTests_' + [Guid]::NewGuid().ToString('N') + '.exe')
& $compiler /nologo ('/out:' + $testOutput) (Join-Path $root 'RestaurantBoost\Source\Rules.cs') (Join-Path $PSScriptRoot '规则验证.cs')
if ($LASTEXITCODE -ne 0) { throw '验证程序编译失败。' }
& $testOutput
if ($LASTEXITCODE -ne 0) { throw '规则验证失败。' }
