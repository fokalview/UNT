param([string]$InputPath, [string]$OutputPath, [ValidateRange(-2,2)][int]$Rate=0)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $speaker.Rate = $Rate
    $format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
    $speaker.SetOutputToWaveFile($OutputPath, $format)
    $speaker.Speak([IO.File]::ReadAllText($InputPath))
} finally { $speaker.Dispose() }
