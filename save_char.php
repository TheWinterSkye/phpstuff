<?php
header('Content-Type: application/json');

if (!isset($_POST['json'])) {
    echo json_encode(['success' => false, 'error' => 'No character data received.']);
    exit;
}

$charData = json_decode($_POST['json'], true);

if (!$charData || !isset($charData['name'])) {
    echo json_encode(['success' => false, 'error' => 'Invalid character data format.']);
    exit;
}

$name = $charData['name'];
$safeName = preg_replace('/[^A-Za-z0-9]/', '', $name);
$charFile = "char_$safeName.json";

// Ensure 'uploads' directory exists
$uploadDir = 'uploads';
if (!is_dir($uploadDir)) {
    if (!mkdir($uploadDir, 0777, true)) {
        echo json_encode(['success' => false, 'error' => 'Failed to create uploads directory. Check permissions.']);
        exit;
    }
}

// Handle image upload
$newImagePath = null;
if (isset($_FILES['image']) && $_FILES['image']['error'] === UPLOAD_ERR_OK) {
    $ext = strtolower(pathinfo($_FILES['image']['name'], PATHINFO_EXTENSION));
    // Basic validation for image type
    if (!in_array($ext, ['jpg', 'jpeg', 'png', 'gif', 'webp'])) {
        echo json_encode(['success' => false, 'error' => 'Invalid image file type.']);
        exit;
    }
    $targetFileName = 'char_' . $safeName . '_' . time() . '.' . $ext; // Add timestamp to avoid caching issues and ensure uniqueness
    $targetPath = $uploadDir . '/' . $targetFileName;
    
    if (move_uploaded_file($_FILES['image']['tmp_name'], $targetPath)) {
        $charData['image'] = $targetPath; // Update image path in data to be saved
        $newImagePath = $targetPath;
    } else {
        echo json_encode(['success' => false, 'error' => 'Failed to move uploaded image. Check permissions for uploads/ directory.']);
        exit;
    }
} else {
    // If no new image uploaded, try to preserve existing image path from current file
    // This assumes charData sent from client does NOT include 'image' field unless new one is uploaded
    // Or, if it does, it's the old path. The client should handle this logic.
    // For safety, let's read the existing character file to get the current image path if not overridden.
    if (file_exists($charFile) && !isset($charData['image'])) { // if client did not send image field at all.
        $existingCharData = json_decode(file_get_contents($charFile), true);
        if (isset($existingCharData['image'])) {
            $charData['image'] = $existingCharData['image'];
        }
    } elseif (!isset($charData['image'])) { // if no image data sent and no existing file
         $charData['image'] = ''; // Default to empty if no image data present
    }
}


// Ensure all expected fields exist in $charData with defaults if not present from client
// This makes the saved JSON more consistent.
$defaultSkills = [ /* ... same as in index.php ... */ ]; // You might want to centralize this
$charData = array_merge([
    'image' => '',
    'fatePoints' => 3,
    'refresh' => 3,
    'aspects' => [
        ['type' => 'High Concept', 'value' => ''], ['type' => 'Trouble', 'value' => ''],
        ['type' => 'Other Aspect 1', 'value' => ''], ['type' => 'Other Aspect 2', 'value' => ''],
        ['type' => 'Other Aspect 3', 'value' => '']
    ],
    'skills' => $defaultSkills,
    'stunts' => [],
    'stressPhysical' => 0, 'physicalStressBoxes' => 2,
    'stressMental' => 0, 'mentalStressBoxes' => 2,
    'consequences' => ['mild1' => '', 'mild2' => '', 'moderate' => '', 'severe' => ''],
    'conditions' => ''
], $charData); // $charData from POST will override these defaults


if (file_put_contents($charFile, json_encode($charData, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES))) {
    $response = ['success' => true, 'message' => 'Character saved.'];
    if ($newImagePath) {
        $response['imagePath'] = $newImagePath;
    }
    echo json_encode($response);
} else {
    echo json_encode(['success' => false, 'error' => 'Failed to write character file. Check permissions.']);
}
?>