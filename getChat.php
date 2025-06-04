<?php
header('Content-Type: application/json');
$chatFile = 'chat.json';
if (!file_exists($chatFile)) {
    echo json_encode(['messages'=>[]]);
    exit;
}
$chatData = json_decode(file_get_contents($chatFile), true);
echo json_encode($chatData);
