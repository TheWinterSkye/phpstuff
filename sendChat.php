<?php
header('Content-Type: application/json');
$data = json_decode(file_get_contents('php://input'), true);
if (empty($data['user']) || empty($data['text'])) {
    echo json_encode(['success'=>false]);
    exit;
}
$user = preg_replace('/[^A-Za-z0-9_ \\-]/', '', $data['user']);
$text = trim($data['text']);
$chatFile = 'chat.json';
if (!file_exists($chatFile)) {
    file_put_contents($chatFile, json_encode(['messages'=>[]], JSON_PRETTY_PRINT));
}
$chat = json_decode(file_get_contents($chatFile), true);
$chat['messages'][] = ['user'=>$user, 'text'=>$text, 'time'=>time()];
file_put_contents($chatFile, json_encode($chat, JSON_PRETTY_PRINT));
echo json_encode(['success'=>true]);
