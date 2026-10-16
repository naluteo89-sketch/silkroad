$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content "$root/comfyui/workflows/klein_00001_.json" -Raw | ConvertFrom-Json
$template = Get-Content "$root/comfyui/workflows/drawing_to_silkroad.api.json" -Raw
$presetText = Get-Content "$root/comfyui/presets/silkroad_style.json" -Raw
$preset = $presetText | ConvertFrom-Json
$approvedPrompt = (Get-Content "$root/comfyui/prompts/silkroad-base.txt" -Raw).TrimEnd()
if ($preset.positive -cne $approvedPrompt) { throw 'Preset differs from approved base prompt' }
if ($template -cne (Get-Content "$root/unity/Assets/StreamingAssets/ComfyUI/drawing_to_silkroad.api.json" -Raw)) { throw 'Unity workflow differs' }
if ($presetText -cne (Get-Content "$root/unity/Assets/StreamingAssets/ComfyUI/silkroad_style.json" -Raw)) { throw 'Unity preset differs' }
if ($preset.themes.Count -ne 0 -or $preset.defaultTheme) { throw 'Unexpected theme override' }
$resolved = $template.Replace('"{{POSITIVE}}"', (ConvertTo-Json -InputObject $preset.positive -Compress)).Replace('"{{NEGATIVE}}"', (ConvertTo-Json -InputObject $preset.negative -Compress))
foreach ($name in @('steps', 'cfg', 'seed')) {
    $resolved = $resolved.Replace(('{{' + $name.ToUpperInvariant() + '}}'), $preset.$name.ToString([Globalization.CultureInfo]::InvariantCulture))
}
$links = @{}
foreach ($edge in $source.links) { $links[[int]$edge[0]] = $edge }
foreach ($assetId in @('drawing-A', 'drawing-B')) {
    $body = '{"workflow":' + $resolved.Replace('"{{INPUT_IMAGE}}"', ('{"__type":"core/ASSET","info":{"id":"' + $assetId + '"}}')) + '}'
    $api = ($body | ConvertFrom-Json).workflow
    if (@($api.PSObject.Properties).Count -ne $source.nodes.Count) { throw 'Node count mismatch' }
    foreach ($node in $source.nodes) {
        $actual = $api.([string]$node.id)
        if ($actual.class_type -cne $node.type) { throw "Node type mismatch: $($node.id)" }
        $linkedNames = @($node.inputs | Where-Object { $null -ne $_.link } | ForEach-Object name)
        foreach ($socket in $node.inputs) {
            if ($null -eq $socket.link) { continue }
            $edge = $links[[int]$socket.link]
            $connection = $actual.inputs.($socket.name)
            if ($connection[0] -ne [string]$edge[1] -or $connection[1] -ne $edge[2]) { throw "Link mismatch: $($node.id)/$($socket.name)" }
        }
        foreach ($widget in $node.widgets_values_named.PSObject.Properties) {
            if ($widget.Name -in @('upload', 'control_after_generate') -or $widget.Name -in $linkedNames) { continue }
            if ($node.type -eq 'LoadImage' -and $widget.Name -eq 'image') {
                if ($actual.inputs.image.info.id -cne $assetId) { throw 'Drawing not bound' }
            } elseif ($node.id -eq 74 -and $widget.Name -eq 'text') {
                # User-approved prompt supersedes the original exported palace concept.
                if ($actual.inputs.text -cne $approvedPrompt) { throw 'Approved prompt mismatch' }
            } elseif ($actual.inputs.($widget.Name) -cne $widget.Value) { throw "Setting mismatch: $($node.id)/$($widget.Name)" }
        }
    }
}
$noise = $source.nodes | Where-Object id -eq 73
if ($preset.seedControl -cne $noise.widgets_values_named.control_after_generate) { throw 'Seed control mismatch' }
if ($api.($preset.outputNode).class_type -ne 'SaveImage') { throw 'Wrong output node' }
Write-Output "PASS: $($source.nodes.Count) nodes, $($source.links.Count) links, all widget values, seed control, two distinct drawing assets, and Unity file parity."
