<?php
header('Content-Type: application/json');
$input = json_decode(file_get_contents('php://input'), true);

if (!$input || empty($input['action'])) {
    echo json_encode(['success'=>false, 'error'=>'Invalid action.']);
    exit;
}

$action = $input['action'];
$sceneFile = 'scene.json';
$scene = ['image'=>'','aspects'=>[],'countdowns'=>[],'zones'=>[],'npcs'=>[]]; // Default structure

if (file_exists($sceneFile)) {
    $scene_content = file_get_contents($sceneFile);
    if ($scene_content !== false) {
        $decoded_scene = json_decode($scene_content, true);
        // Check if decoding was successful or if the file was empty (which is valid for an empty scene object)
        if ($decoded_scene !== null || json_last_error() === JSON_ERROR_NONE) {
             $scene = $decoded_scene;
        } else {
            error_log("updateScene.php: Error decoding scene.json: " . json_last_error_msg() . ". Using default structure.");
        }
    } else {
        error_log("updateScene.php: Could not read scene.json. Using default structure.");
    }
}


switch ($action) {
    case 'addAspect':
        if (!empty($input['value'])) {
            $scene['aspects'][] = $input['value'];
        }
        break;
    case 'removeAspect':
        if (isset($input['index']) && is_numeric($input['index']) && isset($scene['aspects'][intval($input['index'])])) {
            array_splice($scene['aspects'], intval($input['index']), 1);
        }
        break;
    case 'addCountdown':
        if (!empty($input['name']) && isset($input['value']) && is_numeric($input['value'])) {
            $scene['countdowns'][] = ['name'=>$input['name'], 'value'=>intval($input['value'])];
        }
        break;
    case 'updateCountdown':
        if (isset($input['index']) && is_numeric($input['index']) && isset($input['value']) && is_numeric($input['value']) && isset($scene['countdowns'][intval($input['index'])])) {
            $scene['countdowns'][intval($input['index'])]['value'] = intval($input['value']);
        }
        break;
    case 'removeCountdown':
        if (isset($input['index']) && is_numeric($input['index']) && isset($scene['countdowns'][intval($input['index'])])) {
            array_splice($scene['countdowns'], intval($input['index']), 1);
        }
        break;
    case 'addZone':
        if (!empty($input['value'])) {
            $scene['zones'][] = $input['value'];
        }
        break;
    case 'removeZone':
        if (isset($input['index']) && is_numeric($input['index']) && isset($scene['zones'][intval($input['index'])])) {
            array_splice($scene['zones'], intval($input['index']), 1);
        }
        break;
    case 'addNPC': // This is sent by JS correctly as 'addNPC'
        if (!empty($input['name']) && isset($input['aspects'])) {
            $aspects_to_add = is_array($input['aspects']) ? $input['aspects'] : [];
            if (!empty($input['aspects']) && !is_array($input['aspects']) && is_string($input['aspects'])) {
                 $aspects_to_add = array_map('trim', explode(',', $input['aspects']));
            }
            $scene['npcs'][] = ['name'=>$input['name'], 'aspects'=>$aspects_to_add];
        }
        break;
    case 'removeNpc': // Corrected: JS sends 'removeNpc' (camelCase)
    case 'removeNPC': // Added for robustness / old client compatibility
        if (isset($input['index']) && is_numeric($input['index'])) {
            $index_to_remove = intval($input['index']);
            if (isset($scene['npcs'][$index_to_remove])) { // Check if index is valid
                 array_splice($scene['npcs'], $index_to_remove, 1);
            } else {
                error_log("updateScene.php: Attempt to remove NPC at invalid index $index_to_remove. Current NPCs: " . count($scene['npcs']));
            }
        }
        break;
    default:
        error_log("updateScene.php: Unknown action '$action'.");
        // Optionally send back a success:false with an error message
        // echo json_encode(['success'=>false, 'error'=>"Unknown action: $action"]);
        // exit;
        break;
}

if (file_put_contents($sceneFile, json_encode($scene, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES))) {
    echo json_encode(['success'=>true]);
} else {
    error_log("updateScene.php: Failed to write updated scene.json");
    echo json_encode(['success'=>false, 'error'=>'Failed to save scene changes.']);
}
?>