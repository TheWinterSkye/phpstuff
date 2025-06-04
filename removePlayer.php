<?php
// removePlayer.php
session_start();
header('Content-Type: application/json');

// Basic security: Only GM can remove players
$isCurrentUserGM = false;
if (isset($_SESSION['name'])) {
    $playersFileCheck = 'players.json';
    if (file_exists($playersFileCheck)) {
        $allPlayers = json_decode(file_get_contents($playersFileCheck), true);
        if (isset($allPlayers['players'])) {
            foreach ($allPlayers['players'] as $p) {
                if ($p['name'] === $_SESSION['name'] && !empty($p['isGM'])) {
                    $isCurrentUserGM = true;
                    break;
                }
            }
        }
    }
}

if (!$isCurrentUserGM) {
    echo json_encode(['success' => false, 'error' => 'Unauthorized: Only GMs can remove players.']);
    exit;
}

$input = json_decode(file_get_contents('php://input'), true);

if (!$input || empty($input['name'])) {
    echo json_encode(['success' => false, 'error' => 'Player name not provided.']);
    exit;
}

$playerNameToRemove = $input['name'];
$playersFile = 'players.json';

if (!file_exists($playersFile)) {
    echo json_encode(['success' => false, 'error' => 'Players file not found.']);
    exit;
}

$playersData = json_decode(file_get_contents($playersFile), true);
if (!$playersData || !isset($playersData['players'])) {
    echo json_encode(['success' => false, 'error' => 'Invalid players file format.']);
    exit;
}

$updatedPlayers = [];
$found = false;
$wasGM = false;

foreach ($playersData['players'] as $player) {
    if ($player['name'] === $playerNameToRemove) {
        $found = true;
        if (!empty($player['isGM'])) {
            // Generally, GMs shouldn't be removed this way or a GM should not remove themselves.
            // Add a check to prevent a GM from removing another GM or themselves via this simple method.
            if ($player['name'] === $_SESSION['name']){
                 echo json_encode(['success' => false, 'error' => 'GM cannot remove themselves this way.']);
                 exit;
            }
            // If you had multiple GMs and wanted to allow removal of other GMs:
            // $wasGM = true; 
            // For now, assume only non-GMs are removed via this button.
            // If logic changes, ensure GM status is handled (e.g., reassigning GM if last one is removed).
        }
        // Skip this player to remove them
    } else {
        $updatedPlayers[] = $player;
    }
}

if (!$found) {
    echo json_encode(['success' => false, 'error' => 'Player not found in list.']);
    exit;
}

$playersData['players'] = $updatedPlayers;

if (file_put_contents($playersFile, json_encode($playersData, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES))) {
    // Note: Character file (char_PlayerName.json) is NOT deleted.
    echo json_encode(['success' => true, 'message' => "Player '$playerNameToRemove' removed."]);
} else {
    error_log("removePlayer.php: Failed to write updated $playersFile");
    echo json_encode(['success' => false, 'error' => 'Failed to update players file.']);
}
?>