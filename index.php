<?php
session_start();

// Handle login form submission
if (!isset($_SESSION['name'])) {
    if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['username'])) {
        $name = trim($_POST['username']);
        $name = preg_replace('/[^A-Za-z0-9_ \\-]/', '', $name);
        if (!empty($name)) {
            $_SESSION['name'] = $name;
            $playersFile = 'players.json';
            if (!file_exists($playersFile)) {
                file_put_contents($playersFile, json_encode(['players'=>[]], JSON_PRETTY_PRINT));
            }
            $players = json_decode(file_get_contents($playersFile), true);
            $exists = false;
            foreach ($players['players'] as $p) {
                if ($p['name'] === $name) { $exists = true; break; }
            }
            if (!$exists) {
                $isGM = true;
                foreach ($players['players'] as $p) {
                    if (!empty($p['isGM'])) { $isGM = false; break; }
                }
                $players['players'][] = ['name' => $name, 'isGM' => $isGM];
                file_put_contents($playersFile, json_encode($players, JSON_PRETTY_PRINT));
            }
            
            $safeName = preg_replace('/[^A-Za-z0-9]/', '', $name);
            $charFile = "char_$safeName.json";
            if (!file_exists($charFile)) {
                $defaultSkills = [
                    ['name' => 'Athletics', 'value' => 0], ['name' => 'Burglary', 'value' => 0],
                    ['name' => 'Contacts', 'value' => 0], ['name' => 'Deceive', 'value' => 0],
                    ['name' => 'Drive', 'value' => 0], ['name' => 'Empathy', 'value' => 0],
                    ['name' => 'Fight', 'value' => 0], ['name' => 'Investigate', 'value' => 0],
                    ['name' => 'Lore', 'value' => 0], ['name' => 'Notice', 'value' => 0],
                    ['name' => 'Physique', 'value' => 0], ['name' => 'Provoke', 'value' => 0],
                    ['name' => 'Rapport', 'value' => 0], ['name' => 'Resources', 'value' => 0],
                    ['name' => 'Shoot', 'value' => 0], ['name' => 'Stealth', 'value' => 0],
                    ['name' => 'Will', 'value' => 0]
                ];
                $emptyChar = [
                    'name' => $name,
                    'image' => '',
                    'fatePoints' => 3,
                    'refresh' => 3,
                    'aspects' => [
                        ['type' => 'High Concept', 'value' => ''],
                        ['type' => 'Trouble', 'value' => ''],
                        ['type' => 'Other Aspect 1', 'value' => ''],
                        ['type' => 'Other Aspect 2', 'value' => ''],
                        ['type' => 'Other Aspect 3', 'value' => '']
                    ],
                    'skills' => $defaultSkills,
                    'stunts' => [],
                    'stressPhysical' => 0,
                    'stressMental' => 0,
                    'physicalStressBoxes' => 2,
                    'mentalStressBoxes' => 2,
                    'consequences' => ['mild1' => '', 'mild2' => '', 'moderate' => '', 'severe' => ''],
                    'conditions' => ''
                ];
                file_put_contents($charFile, json_encode($emptyChar, JSON_PRETTY_PRINT));
            }
            
            if (!file_exists('chat.json')) {
                file_put_contents('chat.json', json_encode(['messages'=>[]], JSON_PRETTY_PRINT));
            }
            if (!file_exists('scene.json')) {
                $emptyScene = ['image'=>'','aspects'=>[],'countdowns'=>[],'zones'=>[],'npcs'=>[]];
                file_put_contents('scene.json', json_encode($emptyScene, JSON_PRETTY_PRINT));
            }
            if (!is_dir('uploads')) {
                mkdir('uploads', 0777, true);
            }
            header('Location: ' . $_SERVER['PHP_SELF']);
            exit;
        }
    }
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Fate Condensed VTT</title>
    <link rel="stylesheet" href="style.css">
</head>
<body>
<?php if (!isset($_SESSION['name'])): ?>
    <div id="loginDiv">
        <h2>Enter your name to join:</h2>
        <form method="post">
            <input type="text" name="username" placeholder="Your Character Name" required autofocus>
            <button type="submit">Join Game</button>
        </form>
    </div>
<?php else: ?>
    <script>
        var userName = <?php echo json_encode($_SESSION['name']); ?>;
        var isGM = false; // Will be updated by JS
    </script>
    <div id="container">
        <div id="leftPanel" class="panel">
            <h3>Players</h3>
            <div id="playerList"></div>
        </div>
        
        <div id="centerPanel" class="panel">
            <h3>Scene <span class="gm-only-inline">(GM: <input type="file" id="sceneImageInput" accept="image/*">)</span></h3>
            <div id="sceneImageContainer">
                <img id="sceneImage" src="" alt="Scene Image">
            </div>
        </div>
        
        <div id="rightPanel" class="panel">
            <h3>Scene Tracker <span class="gm-only-inline">(GM Controls)</span></h3>
            <div id="sceneAspects" class="gm-only">
                <h4>Aspects</h4>
                <ul id="aspectList"></ul>
                <input type="text" id="newAspectInput" placeholder="New Aspect">
                <button id="addAspectBtn">Add Aspect</button>
            </div>
            <div id="sceneCountdowns" class="gm-only">
                <h4>Countdowns</h4>
                <ul id="countdownList"></ul>
                <input type="text" id="newCountdownName" placeholder="Name">
                <input type="number" id="newCountdownValue" placeholder="Value" min="1">
                <button id="addCountdownBtn">Add Countdown</button>
            </div>
            <div id="sceneZones" class="gm-only">
                <h4>Zones</h4>
                <ul id="zoneList"></ul>
                <input type="text" id="newZoneInput" placeholder="New Zone">
                <button id="addZoneBtn">Add Zone</button>
            </div>
            <div id="sceneNPCs" class="gm-only">
                <h4>NPCs</h4>
                <ul id="npcList"></ul>
                <input type="text" id="newNPCName" placeholder="Name">
                <input type="text" id="newNPCAspects" placeholder="Aspects (comma separated)">
                <button id="addNPCBtn">Add NPC</button>
            </div>
             <div id="sceneDisplay" class="player-view"> <!-- For non-GMs to see scene data -->
                <h4>Scene Aspects</h4>
                <ul id="aspectDisplayList"></ul>
                <h4>Scene Countdowns</h4>
                <ul id="countdownDisplayList"></ul>
                <h4>Scene Zones</h4>
                <ul id="zoneDisplayList"></ul>
            </div>
        </div>
        
        <div id="bottomPanel">
            <div id="chatBox">
                <div id="chatMessages"></div>
                <div id="chatInputContainer">
                    <input type="text" id="chatInput" placeholder="Type a message or /roll [mod] [desc]">
                    <button id="sendChatBtn">Send</button>
                    <button id="rollDiceBtn">4dF</button>
                </div>
            </div>
        </div>
    </div>

    <div id="charModal" class="modal" style="display:none;">
        <div class="modal-content">
            <div class="modal-header">
                <h3>Character Sheet: <span id="charNameTitle"></span></h3>
                <span class="close" id="closeModal">×</span>
            </div>
            <form id="charForm">
                <div class="char-sheet-grid">
                    <div class="char-section" id="char-core-stats">
                        <h4>Core Stats</h4>
                        <div class="form-group">
                            <label for="charNameDisplay">Name:</label>
                            <span id="charNameDisplay" style="font-size: var(--font-size-lg); color: var(--primary-accent);"></span>
                        </div>
                        <div class="form-group">
                            <label for="charImageInput">Character Image:</label>
                            <input type="file" id="charImageInput" accept="image/*">
                            <img id="charImagePreview" src="#" alt="Preview" style="max-width:100px; max-height:100px; display:none; border-radius:var(--border-radius-sm); margin-top:5px;">
                        </div>
                        <div class="form-group">
                            <label for="charFatePoints">Fate Points:</label>
                            <input type="number" id="charFatePoints" min="0">
                        </div>
                        <div class="form-group">
                            <label for="charRefresh">Refresh:</label>
                            <input type="number" id="charRefresh" min="0">
                        </div>
                    </div>

                    <div class="char-section" id="char-aspects-section">
                        <h4>Aspects</h4>
                        <div id="aspectsContainer">
                            <!-- Aspect inputs will be generated here by JS -->
                        </div>
                    </div>

                    <div class="char-section" id="char-skills-section">
                        <h4>Skills</h4>
                        <div id="skillsContainer">
                            <!-- Skill inputs will be generated here by JS -->
                        </div>
                        <button type="button" id="addSkillBtn">Add Custom Skill</button>
                    </div>
                    
                    <div class="char-section" id="char-stress-consequences-section">
                        <h4>Stress & Consequences</h4>
                        <div class="form-group">
                            <label for="charStressPhys">Physical Stress (Current / Max):</label>
                            <input type="number" id="charStressPhys" min="0" style="width: calc(50% - 5px); margin-right:10px;">
                            <input type="number" id="charPhysStressBoxes" min="0" title="Max Physical Stress Boxes" style="width: calc(50% - 5px);">
                        </div>
                        <div class="form-group">
                            <label for="charStressMental">Mental Stress (Current / Max):</label>
                            <input type="number" id="charStressMental" min="0" style="width: calc(50% - 5px); margin-right:10px;">
                            <input type="number" id="charMentalStressBoxes" min="0" title="Max Mental Stress Boxes" style="width: calc(50% - 5px);">
                        </div>
                        <h5>Consequences</h5>
                        <div class="form-group">
                            <label for="consMild1">Mild 1:</label> <input type="text" id="consMild1">
                        </div>
                        <div class="form-group">
                            <label for="consMild2">Mild 2:</label> <input type="text" id="consMild2">
                        </div>
                        <div class="form-group">
                            <label for="consModerate">Moderate:</label> <input type="text" id="consModerate">
                        </div>
                        <div class="form-group">
                            <label for="consSevere">Severe:</label> <input type="text" id="consSevere">
                        </div>
                    </div>

                    <div class="char-section" id="char-stunts-section">
                        <h4>Stunts</h4>
                        <div id="stuntsContainer">
                            <!-- Stunt inputs will be generated here by JS -->
                        </div>
                        <button type="button" id="addStuntBtn">Add Stunt</button>
                    </div>
                    
                    <div class="char-section" id="char-conditions-section">
                        <h4>Conditions / Notes</h4>
                        <div class="form-group">
                            <label for="charConditions">Describe any conditions or important notes:</label>
                            <textarea id="charConditions" rows="4"></textarea>
                        </div>
                    </div>
                </div>
                <div class="char-buttons">
                    <button type="button" id="loadCharFileBtn">Load from File</button>
                    <input type="file" id="loadCharInput" accept=".json"> <!-- Hidden -->
                    <button type="button" id="downloadCharBtn">Download as File</button> 
                    <button type="button" id="saveCharBtn" class="primary">Save Character to Server</button>  <!-- CHANGED LABEL -->
                </div>
            </form>
        </div>
    </div>

    <script src="script.js"></script>
<?php endif; ?>
</body>
</html>