# Minimal client for the DevBridge TCP transport.
#   .\bridge-client.ps1 state                  - send one command, print the response
#   .\bridge-client.ps1 -Listen 10             - just stream push events for N seconds
#   .\bridge-client.ps1 interact -Listen 5     - send command, then keep streaming events
# Port is discovered from the game's UserData/DevBridge/port.txt.
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Command,
    [int]$Listen = 0,
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Disco Elysium"
)

$portFile = Join-Path $GamePath "UserData\DevBridge\port.txt"
if (-not (Test-Path $portFile)) { Write-Error "port.txt not found - is the game running with DevBridge?"; exit 1 }
$port = [int](Get-Content $portFile -TotalCount 1)

$client = New-Object System.Net.Sockets.TcpClient
$client.Connect("127.0.0.1", $port)
$stream = $client.GetStream()
$writer = New-Object System.IO.StreamWriter($stream, (New-Object System.Text.UTF8Encoding($false)))
$writer.AutoFlush = $true
$reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)

try {
    if ($Command -and $Command.Count -gt 0) {
        $writer.WriteLine(($Command -join " "))
        while ($true) {
            $line = $reader.ReadLine()
            if ($null -eq $line -or $line -eq "<<END>>") { break }
            $line
        }
    }
    if ($Listen -gt 0) {
        # Poll DataAvailable instead of using ReadTimeout: a timed-out ReadLine leaves
        # the StreamReader in a broken state and silently eats everything after it.
        $deadline = (Get-Date).AddSeconds($Listen)
        while ((Get-Date) -lt $deadline) {
            if ($stream.DataAvailable) {
                do {
                    $line = $reader.ReadLine()
                    if ($null -eq $line) { return }
                    $line
                } while ($stream.DataAvailable)
            } else {
                Start-Sleep -Milliseconds 100
            }
        }
    }
} finally {
    $client.Close()
}
