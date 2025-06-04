<?php
header('Content-Type: application/json');
$sceneFile = 'scene.json';
if (!file_exists($sceneFile)) {
    echo json_encode(['image'=>'','aspects'=>[],'countdowns'=>[],'zones'=>[],'npcs'=>[]]);
    exit;
}
$data = json_decode(file_get_contents($sceneFile), true);
echo json_encode($data);
