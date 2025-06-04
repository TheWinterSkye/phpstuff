<?php
// scene.php
session_start();
$sceneFile = __DIR__ . '/data/scene.json';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    // 1) File‐upload form for scene image (name="sceneImg")
    if (!empty($_FILES['sceneImg']) && $_FILES['sceneImg']['error'] === UPLOAD_ERR_OK) {
        $ext = pathinfo($_FILES['sceneImg']['name'], PATHINFO_EXTENSION);
        $upDir = __DIR__ . '/uploads';
        if (!is_dir($upDir)) mkdir($upDir, 0777, true);
        $target = $upDir . '/scene_' . time() . '.' . $ext;
        move_uploaded_file($_FILES['sceneImg']['tmp_name'], $target);

        // Load or init scene JSON
        $scene = file_exists($sceneFile)
               ? json_decode(file_get_contents($sceneFile), true)
               : ['image'=>'','aspects'=>[],'countdowns'=>[],'zones'=>[],'npcs'=>[]];
        $scene['image'] = basename($target);
        file_put_contents($sceneFile, json_encode($scene, JSON_PRETTY_PRINT));
        echo json_encode(['status'=>'ok']);
        exit;
    }

    // 2) JSON POST for adding aspects, countdowns, zones, NPCs
    $input = json_decode(file_get_contents('php://input'), true);
    $scene = file_exists($sceneFile)
           ? json_decode(file_get_contents($sceneFile), true)
           : ['image'=>'','aspects'=>[],'countdowns'=>[],'zones'=>[],'npcs'=>[]];

    switch ($input['action'] ?? '') {
        case 'aspect':
            if (!empty($input['value'])) {
                $scene['aspects'][] = $input['value'];
            }
            break;
        case 'countdown':
            if (!empty($input['name']) && isset($input['value'])) {
                $scene['countdowns'][] = [
                  'name' => $input['name'],
                  'value' => intval($input['value'])
                ];
            }
            break;
        case 'zone':
            if (!empty($input['value'])) {
                $scene['zones'][] = $input['value'];
            }
            break;
        case 'npc':
            if (!empty($input['name']) && isset($input['aspects'])) {
                $scene['npcs'][] = [
                  'name'    => $input['name'],
                  'aspects' => $input['aspects']
                ];
            }
            break;
        // add more cases if you need remove or update actions
    }

    file_put_contents($sceneFile, json_encode($scene, JSON_PRETTY_PRINT));
    echo json_encode(['success'=>true]);
    exit;
}

// GET: return the full scene state
header('Content-Type: application/json');
if (file_exists($sceneFile)) {
    echo file_get_contents($sceneFile);
} else {
    echo json_encode([
      'image'=>'',
      'aspects'=>[],
      'countdowns'=>[],
      'zones'=>[],
      'npcs'=>[]
    ]);
}
