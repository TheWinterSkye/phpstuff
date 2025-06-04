<?php
// players.php
// Return a JSON array of player‐names stored in data/players.json

header('Content-Type: application/json');
$playersFile = __DIR__ . '/data/players.json';

if (!file_exists($playersFile)) {
    // If it doesn't exist yet, return an empty array
    echo json_encode([]);
    exit;
}

// Read & echo the JSON array
echo file_get_contents($playersFile);
