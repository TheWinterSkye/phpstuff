<?php
session_start();
$chatFile = 'data/chat.json';
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    // Receive JSON payload
    $input = json_decode(file_get_contents('php://input'), true);
    $text = trim($input['text'] ?? '');
    if ($text !== '') {
        $chatLog = file_exists($chatFile) ? json_decode(file_get_contents($chatFile), true) : [];
        $chatLog = is_array($chatLog) ? $chatLog : [];
        $chatLog[] = [
            'time' => date('H:i:s'),
            'user' => $_SESSION['username'],
            'text' => htmlspecialchars($text)
        ];
        file_put_contents($chatFile, json_encode($chatLog));
    }
    echo json_encode(['status'=>'ok']);
} else {
    // Return chat log as JSON
    header('Content-Type: application/json');
    if (file_exists($chatFile)) {
        echo file_get_contents($chatFile);
    } else {
        echo json_encode([]);
    }
}
?>
