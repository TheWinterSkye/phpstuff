<?php
// char.php
session_start();
header('Content-Type: application/json');
$dataDir = __DIR__ . '/data';
if (!is_dir($dataDir)) mkdir($dataDir, 0777, true);

if ($_SERVER['REQUEST_METHOD'] === 'GET' && isset($_GET['name'])) {
    $name = preg_replace('/[^A-Za-z0-9]/','', $_GET['name']);
    $file = "$dataDir/char_{$name}.json";
    if (file_exists($file)) {
        echo file_get_contents($file);
    } else {
        echo json_encode(null);
    }
    exit;
}

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $input = json_decode(file_get_contents('php://input'), true);
    $name  = preg_replace('/[^A-Za-z0-9]/','', $input['name'] ?? '');
    $data  = $input['data']  ?? null;
    if (!$name || !is_array($data)) {
        echo json_encode(['success'=>false]);
        exit;
    }
    $file = "$dataDir/char_{$name}.json";
    file_put_contents($file, json_encode($data, JSON_PRETTY_PRINT));
    echo json_encode(['success'=>true]);
    exit;
}

// If we get here, it was neither a valid GET nor POST
echo json_encode(['success'=>false]);
