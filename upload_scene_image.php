<?php
header('Content-Type: application/json');

// Enhanced error checking for file upload
if (!isset($_FILES['sceneImage'])) {
    // This can happen if post_max_size is exceeded, wiping $_FILES and $_POST
    // Or if the form field name 'sceneImage' in FormData doesn't match.
    error_log("upload_scene_image.php: 'sceneImage' not found in \$_FILES. Check post_max_size in php.ini or FormData key.");
    echo json_encode(['success' => false, 'error' => 'No file data received. Possible server configuration issue (e.g., post_max_size).']);
    exit;
}

if ($_FILES['sceneImage']['error'] !== UPLOAD_ERR_OK) {
    $upload_errors = [
        UPLOAD_ERR_INI_SIZE   => 'File exceeds upload_max_filesize directive in php.ini.',
        UPLOAD_ERR_FORM_SIZE  => 'File exceeds MAX_FILE_SIZE directive in HTML form (not used here).',
        UPLOAD_ERR_PARTIAL    => 'File was only partially uploaded.',
        UPLOAD_ERR_NO_FILE    => 'No file was uploaded by the client.',
        UPLOAD_ERR_NO_TMP_DIR => 'Missing a temporary folder on the server.',
        UPLOAD_ERR_CANT_WRITE => 'Failed to write file to disk on the server.',
        UPLOAD_ERR_EXTENSION  => 'A PHP extension stopped the file upload.',
    ];
    $error_message = isset($upload_errors[$_FILES['sceneImage']['error']])
                     ? $upload_errors[$_FILES['sceneImage']['error']]
                     : 'Unknown upload error (Code: ' . $_FILES['sceneImage']['error'] . ').';
    error_log("upload_scene_image.php: Upload error code " . $_FILES['sceneImage']['error'] . ": " . $error_message);
    echo json_encode(['success' => false, 'error' => $error_message]);
    exit;
}

// Proceed with validated file
$file_name = $_FILES['sceneImage']['name'];
$file_tmp_name = $_FILES['sceneImage']['tmp_name'];

$ext = strtolower(pathinfo($file_name, PATHINFO_EXTENSION));
$allowed_exts = ['jpg', 'jpeg', 'png', 'gif', 'webp', 'avif']; // Added avif
if (!in_array($ext, $allowed_exts)) {
    echo json_encode(['success'=>false, 'error'=>'Invalid file type. Allowed: ' . implode(', ', $allowed_exts)]);
    exit;
}

$uploadDir = 'uploads';
if (!is_dir($uploadDir)) {
    if (!mkdir($uploadDir, 0777, true)) { // Added 0777 and recursive true
        error_log("upload_scene_image.php: Failed to create directory: $uploadDir");
        echo json_encode(['success' => false, 'error' => 'Failed to create uploads directory. Check server permissions.']);
        exit;
    }
}

// Sanitize filename part slightly - though time() makes it mostly unique
$safe_basename = preg_replace("/[^A-Za-z0-9\._-]/", '', pathinfo($file_name, PATHINFO_FILENAME));
if(empty($safe_basename)) $safe_basename = "scene_image"; // ensure there's some name part

$target = $uploadDir . '/' . $safe_basename . '_' . time() . '.' . $ext;

if (move_uploaded_file($file_tmp_name, $target)) {
    $sceneFile = 'scene.json';
    $scene = ['image'=>'','aspects'=>[],'countdowns'=>[],'zones'=>[],'npcs'=>[]]; // Default structure

    if (file_exists($sceneFile)) {
        $scene_content = file_get_contents($sceneFile);
        if ($scene_content !== false) {
            $decoded_scene = json_decode($scene_content, true);
            if ($decoded_scene !== null || json_last_error() === JSON_ERROR_NONE) {
                 $scene = $decoded_scene; // Use existing content if valid
            } else {
                error_log("upload_scene_image.php: Failed to decode scene.json: " . json_last_error_msg() . ". Using default scene structure.");
            }
        } else {
             error_log("upload_scene_image.php: Failed to read scene.json. Using default scene structure.");
        }
    } else {
        // scene.json doesn't exist, will be created with default structure + new image.
    }

    $scene['image'] = $target;
    if (file_put_contents($sceneFile, json_encode($scene, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES))) {
        echo json_encode(['success'=>true, 'imagePath'=>$target]);
    } else {
        error_log("upload_scene_image.php: Failed to write updated scene.json");
        echo json_encode(['success'=>false, 'error'=>'Failed to save scene data.']);
    }
} else {
    error_log("upload_scene_image.php: move_uploaded_file failed for '$file_tmp_name' to '$target'. Check permissions on $uploadDir. PHP error: " . error_get_last()['message']);
    echo json_encode(['success'=>false, 'error'=>'Upload processing failed on server. Check server logs/permissions.']);
}
?>