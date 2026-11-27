# Dot-sourced by the helper build scripts.
# Runs csc.exe with its arguments in a response file. Full paths to every source, facade and
# WinRT .winmd can exceed Windows' 32,767-character command-line limit when the source lives in a
# deep folder (e.g. %LOCALAPPDATA%\NordRice\source\<guid>\<repo>-<sha>), which fails with
# "The filename or extension is too long".
function Invoke-Csc([string]$Compiler, [string[]]$Arguments) {
  $rsp = Join-Path $env:TEMP ("rime-csc-" + [Guid]::NewGuid().ToString('N') + '.rsp')
  # One argument per line, each quoted whole (same as passing it quoted on the command line).
  $lines = $Arguments | Where-Object { $_ } | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
  [IO.File]::WriteAllLines($rsp, [string[]]$lines, (New-Object Text.UTF8Encoding($false)))
  try { & $Compiler "@$rsp" } finally { Remove-Item -LiteralPath $rsp -Force -ErrorAction SilentlyContinue }
}
