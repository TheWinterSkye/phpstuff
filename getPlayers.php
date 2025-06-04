<?php
header('Content-Type: application/json');
$playersFile = 'players.json';

if (!file_exists($playersFile)) {
    echo json_encode(['players'=>[]]);
    exit;
}

$playersData = json_decode(file_get_contents($playersFile), true);
$output = ['players'=>[]];

foreach ($playersData['players'] as $p) {
    $name = $p['name'];
    $safeName = preg_replace('/[^A-Za-z0-9]/', '', $name);
    $charFile = "char_$safeName.json";

    // Default character data structure elements we need for the player list
    $charSummary = [
        'image' => '',
        'fatePoints' => 0,
        'stressPhysical' => 0,
        'physicalStressBoxes' => 2, // Default
        'stressMental' => 0,
        'mentalStressBoxes' => 2 // Default
    ];

    if (file_exists($charFile)) {
        $cdata = json_decode(file_get_contents($charFile), true);
        if ($cdata) { // Ensure cdata is not null/false
            $charSummary['image'] = isset($cdata['image']) ? $cdata['image'] : '';
            $charSummary['fatePoints'] = isset($cdata['fatePoints']) ? $cdata['fatePoints'] : 0;
            $charSummary['stressPhysical'] = isset($cdata['stressPhysical']) ? $cdata['stressPhysical'] : 0;
            $charSummary['physicalStressBoxes'] = isset($cdata['physicalStressBoxes']) ? $cdata['physicalStressBoxes'] : 2;
            $charSummary['stressMental'] = isset($cdata['stressMental']) ? $cdata['stressMental'] : 0;
            $charSummary['mentalStressBoxes'] = isset($cdata['mentalStressBoxes']) ? $cdata['mentalStressBoxes'] : 2;
        }
    }

    $output['players'][] = [
        'name' => $name,
        'isGM' => !empty($p['isGM']),
        'image' => $charSummary['image'],
        'fatePoints' => $charSummary['fatePoints'],
        'stressPhysical' => $charSummary['stressPhysical'],
        'physicalStressBoxes' => $charSummary['physicalStressBoxes'],
        'stressMental' => $charSummary['stressMental'],
        'mentalStressBoxes' => $charSummary['mentalStressBoxes']
    ];
}

echo json_encode($output, JSON_UNESCAPED_SLASHES);
?>