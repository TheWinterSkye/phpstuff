// script.js
document.addEventListener('DOMContentLoaded', function() {
    // userName and isGM are global for this script, isGM is set in updatePlayers
    var playerListDiv = document.getElementById('playerList');
    var chatMessagesDiv = document.getElementById('chatMessages');
    var chatInput = document.getElementById('chatInput');
    var sceneImage = document.getElementById('sceneImage');
    var sceneImageInput = document.getElementById('sceneImageInput');
    
    const defaultSkills = [
        'Athletics', 'Burglary', 'Contacts', 'Deceive', 'Drive', 'Empathy', 
        'Fight', 'Investigate', 'Lore', 'Notice', 'Physique', 'Provoke', 
        'Rapport', 'Resources', 'Shoot', 'Stealth', 'Will'
    ];

    function getSafeName(name) {
        return name.replace(/[^A-Za-z0-9]/g, '');
    }

    function applyGMRoles() {
        if (isGM) {
            document.body.classList.add('is-gm');
            document.getElementById('sceneDisplay').style.display = 'none';

        } else {
            document.body.classList.remove('is-gm');
            document.querySelectorAll('.gm-only').forEach(el => el.style.display = 'none');
            document.getElementById('sceneDisplay').style.display = 'block';
        }
        // Show GM-only controls for scene tracker if GM
        const sceneTrackerGMControls = ['sceneAspects', 'sceneCountdowns', 'sceneZones', 'sceneNPCs'];
        sceneTrackerGMControls.forEach(id => {
            const el = document.getElementById(id);
            if (el) el.style.display = isGM ? 'block' : 'none';
        });
        if (sceneImageInput) sceneImageInput.style.display = isGM ? 'inline-block' : 'none';

    }


    function updatePlayers() {
        fetch('getPlayers.php')
        .then(response => response.json())
        .then(data => {
            playerListDiv.innerHTML = '';
            let gmFound = false;
            data.players.forEach(function(player) {
                var entry = document.createElement('div');
                entry.className = 'playerEntry';
                
                var img = document.createElement('img');
                img.className = 'playerImg';
                img.src = player.image ? player.image : 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><rect width="100" height="100" fill="%235c5f77"/><text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" font-size="40" fill="%23a9b1d6">?</text></svg>'; // Basic SVG placeholder
                img.alt = player.name;
                img.title = `View ${player.name}'s sheet`;
                img.addEventListener('click', function() {
                    if (player.name === userName || isGM) {
                        openCharSheet(player.name);
                    }
                });
                entry.appendChild(img);

                var infoContainer = document.createElement('div');
                infoContainer.className = 'playerInfoContainer';

                var nameSpan = document.createElement('div');
                nameSpan.className = 'playerName';
                nameSpan.textContent = player.name + (player.isGM ? ' [GM]' : '');
                if (player.name === userName) {
                    nameSpan.style.color = 'var(--secondary-accent)'; // Highlight current player
                    if (player.isGM) {
                        isGM = true; // Set global isGM
                        gmFound = true;
                    }
                }
                infoContainer.appendChild(nameSpan);
                
                var statsDiv = document.createElement('div');
                statsDiv.className = 'playerStats';
                statsDiv.innerHTML = `FP: ${player.fatePoints}<br>` +
                                     `Stress P: ${player.stressPhysical} / ${player.physicalStressBoxes || 'N/A'}<br>` +
                                     `Stress M: ${player.stressMental} / ${player.mentalStressBoxes || 'N/A'}`;
                infoContainer.appendChild(statsDiv);
                entry.appendChild(infoContainer);

                // Add remove button for GM, not for self or other GMs
                if (isGM && player.name !== userName && !player.isGM) { 
                    var removeBtn = document.createElement('button');
                    removeBtn.textContent = 'X';
                    removeBtn.className = 'removePlayerBtn';
                    removeBtn.title = 'Remove ' + player.name;
                    removeBtn.onclick = function(e) {
                        e.stopPropagation(); // Prevent triggering click on player entry
                        if (confirm(`Are you sure you want to remove player ${player.name}? Their character file will remain.`)) {
                            removePlayer(player.name);
                        }
                    };
                    entry.appendChild(removeBtn);
                }
                
                playerListDiv.appendChild(entry);
            });
            if (!gmFound && data.players.find(p => p.name === userName && p.isGM)) {
                isGM = true; // Double check if current user is GM
            }
            applyGMRoles(); // Apply GM specific UI changes
        });
    }

    function removePlayer(playerName) {
        if (!isGM) return;
        fetch('removePlayer.php', {
            method: 'POST',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify({name: playerName})
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                updatePlayers(); // Refresh player list
            } else {
                alert('Error removing player: ' + (data.error || 'Unknown error'));
            }
        });
    }

    function updateChat() {
        fetch('getChat.php')
        .then(response => response.json())
        .then(data => {
            chatMessagesDiv.innerHTML = '';
            data.messages.forEach(function(msg) {
                var msgElem = document.createElement('div');
                msgElem.className = 'chatMsg';
                var time = new Date(msg.time*1000).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                
                var timeSpan = document.createElement('span');
                timeSpan.className = 'timestamp';
                timeSpan.textContent = `[${time}]`;
                
                var userSpan = document.createElement('span');
                userSpan.className = 'user';
                userSpan.textContent = msg.user + ': ';
                
                var textSpan = document.createElement('span');
                textSpan.className = 'text';
                textSpan.textContent = msg.text;

                msgElem.appendChild(timeSpan);
                msgElem.appendChild(userSpan);
                msgElem.appendChild(textSpan);
                chatMessagesDiv.appendChild(msgElem);
            });
            chatMessagesDiv.scrollTop = chatMessagesDiv.scrollHeight;
        });
    }

    function sendChat(text) {
        if (!text) return;

        if (text.toLowerCase().startsWith("/roll")) {
            let parts = text.split(" ");
            let modifier = 0;
            let description = "";
            if (parts.length > 1 && !isNaN(parseInt(parts[1]))) {
                modifier = parseInt(parts[1]);
                description = parts.slice(2).join(" ");
            } else {
                description = parts.slice(1).join(" ");
            }
            rollFateDice(modifier, description);
            chatInput.value = '';
            return;
        }

        fetch('sendChat.php', {
            method: 'POST',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify({user: userName, text: text})
        }).then(() => {
            chatInput.value = '';
            updateChat();
        });
    }

    document.getElementById('sendChatBtn').addEventListener('click', function() {
        sendChat(chatInput.value);
    });
    chatInput.addEventListener('keypress', function(e) {
        if (e.key === 'Enter') {
            sendChat(chatInput.value);
            e.preventDefault(); // Prevents form submission if chat is in a form
        }
    });

    function rollFateDice(modifier = 0, description = "") {
        var symbols = [];
        var total = 0;
        for (var i = 0; i < 4; i++) {
            var r = Math.floor(Math.random() * 3) - 1; // -1, 0, or +1
            total += r;
            if (r > 0) symbols.push('+');
            else if (r < 0) symbols.push('-');
            else symbols.push('0'); // Using 0 for blank for clarity
        }
        let resultText = `rolls 4dF: [${symbols.join('] [')}] = ${total}`;
        if (modifier !== 0) {
            resultText += ` ${modifier >= 0 ? '+' : ''} ${modifier} = ${total + modifier}`;
        }
        if (description) {
            resultText += ` (for ${description})`;
        }
        sendChat(resultText);
    }

    document.getElementById('rollDiceBtn').addEventListener('click', function() {
        rollFateDice(0, "");
    });

    if (sceneImageInput) {
        sceneImageInput.addEventListener('change', function() {
            if (!isGM) return;
            var file = sceneImageInput.files[0];
            if (!file) return;
            var formData = new FormData();
            formData.append('sceneImage', file);
            fetch('upload_scene_image.php', {method: 'POST', body: formData})
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    sceneImage.src = data.imagePath + '?' + new Date().getTime(); // Cache bust
                    updateScene(); // To make sure scene.json is updated everywhere
                } else {
                    alert('Scene image upload failed: ' + (data.error || 'Unknown error'));
                }
            });
        });
    }
    
    function updateSceneDisplayLists(data) {
        const aspectDisplayList = document.getElementById('aspectDisplayList');
        const countdownDisplayList = document.getElementById('countdownDisplayList');
        const zoneDisplayList = document.getElementById('zoneDisplayList');

        aspectDisplayList.innerHTML = '';
        data.aspects.forEach(asp => {
            const li = document.createElement('li');
            li.textContent = asp;
            aspectDisplayList.appendChild(li);
        });

        countdownDisplayList.innerHTML = '';
        data.countdowns.forEach(cd => {
            const li = document.createElement('li');
            li.textContent = `${cd.name}: ${cd.value}`;
            countdownDisplayList.appendChild(li);
        });

        zoneDisplayList.innerHTML = '';
        data.zones.forEach(zone => {
            const li = document.createElement('li');
            li.textContent = zone;
            zoneDisplayList.appendChild(li);
        });
    }


    function updateScene() {
        fetch('getScene.php')
        .then(response => response.json())
        .then(data => {
            if (data.image) {
                // Check if src needs updating to avoid flicker if not changed
                if (sceneImage.src.split('?')[0] !== data.image) {
                     sceneImage.src = data.image + '?' + new Date().getTime(); // Cache bust
                }
            } else {
                sceneImage.src = 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 100"><rect width="200" height="100" fill="%231a1b26"/><text x="50%" y="50%" fill="%235c5f77" font-size="16" dominant-baseline="middle" text-anchor="middle">No Scene Image</text></svg>';
            }

            updateSceneDisplayLists(data); // Update non-GM view

            if (!isGM) return; // Rest of this function is GM controls

            var aspectList = document.getElementById('aspectList');
            aspectList.innerHTML = '';
            data.aspects.forEach((asp, index) => {
                var li = document.createElement('li');
                li.textContent = asp;
                var btn = document.createElement('button');
                btn.textContent = 'X';
                btn.className = 'danger';
                btn.onclick = function() { removeSceneItem('aspect', index); };
                li.appendChild(btn);
                aspectList.appendChild(li);
            });
            
            var countdownList = document.getElementById('countdownList');
            countdownList.innerHTML = '';
            data.countdowns.forEach((cd, index) => {
                var li = document.createElement('li');
                li.textContent = `${cd.name}: ${cd.value}`;
                var btnGroup = document.createElement('div');
                var decBtn = document.createElement('button');
                decBtn.textContent = '-1';
                decBtn.onclick = function() { updateCountdown(index, cd.value - 1); };
                btnGroup.appendChild(decBtn);
                var remBtn = document.createElement('button');
                remBtn.textContent = 'X';
                remBtn.className = 'danger';
                remBtn.onclick = function() { removeSceneItem('countdown', index); };
                btnGroup.appendChild(remBtn);
                li.appendChild(btnGroup);
                countdownList.appendChild(li);
            });

            var zoneList = document.getElementById('zoneList');
            zoneList.innerHTML = '';
            data.zones.forEach((zone, index) => {
                var li = document.createElement('li');
                li.textContent = zone;
                var btn = document.createElement('button');
                btn.textContent = 'X';
                btn.className = 'danger';
                btn.onclick = function() { removeSceneItem('zone', index); };
                li.appendChild(btn);
                zoneList.appendChild(li);
            });
            
            var npcList = document.getElementById('npcList');
            npcList.innerHTML = '';
            data.npcs.forEach((npc, index) => {
                var li = document.createElement('li');
                li.textContent = `${npc.name} - Aspects: ${npc.aspects.join(', ')}`;
                var btn = document.createElement('button');
                btn.textContent = 'X';
                btn.className = 'danger';
                btn.onclick = function() { removeSceneItem('npc', index); }; // Corrected 'Npc' to 'npc'
                li.appendChild(btn);
                npcList.appendChild(li);
            });
        });
    }

    // Scene item management for GM
    document.getElementById('addAspectBtn')?.addEventListener('click', function() {
        var val = document.getElementById('newAspectInput').value.trim();
        if (val) {
            updateSceneItem('addAspect', {value: val});
            document.getElementById('newAspectInput').value = '';
        }
    });
    document.getElementById('addCountdownBtn')?.addEventListener('click', function() {
        var name = document.getElementById('newCountdownName').value.trim();
        var val = parseInt(document.getElementById('newCountdownValue').value);
        if (name && !isNaN(val)) {
            updateSceneItem('addCountdown', {name: name, value: val});
            document.getElementById('newCountdownName').value = '';
            document.getElementById('newCountdownValue').value = '';
        }
    });
    document.getElementById('addZoneBtn')?.addEventListener('click', function() {
        var val = document.getElementById('newZoneInput').value.trim();
        if (val) {
            updateSceneItem('addZone', {value: val});
            document.getElementById('newZoneInput').value = '';
        }
    });
    document.getElementById('addNPCBtn')?.addEventListener('click', function() {
        var name = document.getElementById('newNPCName').value.trim();
        var aspects = document.getElementById('newNPCAspects').value.trim();
        if (name && aspects) {
            var aspectsArr = aspects.split(',').map(s => s.trim());
            updateSceneItem('addNPC', {name: name, aspects: aspectsArr});
            document.getElementById('newNPCName').value = '';
            document.getElementById('newNPCAspects').value = '';
        }
    });

    function updateSceneItem(action, data) {
        if (!isGM) return;
        fetch('updateScene.php', {
            method: 'POST',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify(Object.assign({action: action}, data))
        }).then(updateScene);
    }
    function removeSceneItem(type, index) {
        if (!isGM) return;
        updateSceneItem('remove' + type.charAt(0).toUpperCase() + type.slice(1), {index: index});
    }
    function updateCountdown(index, value) {
        if (!isGM) return;
        if (value >= 0) {
            updateSceneItem('updateCountdown', {index: index, value: value});
        }
    }

    // Character Sheet Modal
    var charModal = document.getElementById('charModal');
    var closeModal = document.getElementById('closeModal');
    var charImagePreview = document.getElementById('charImagePreview');
    var charImageInputModal = document.getElementById('charImageInput'); // Renamed to avoid conflict

    closeModal.onclick = function() { charModal.style.display = 'none'; };
    window.onclick = function(event) {
        if (event.target == charModal) {
            charModal.style.display = 'none';
        }
    }
    
    charImageInputModal.onchange = evt => {
        const [file] = charImageInputModal.files
        if (file) {
            charImagePreview.src = URL.createObjectURL(file);
            charImagePreview.style.display = 'block';
        }
    }

    function populateAspects(aspectsData) {
        const container = document.getElementById('aspectsContainer');
        container.innerHTML = '';
        aspectsData.forEach((aspect, index) => {
            const div = document.createElement('div');
            div.className = 'aspect-entry form-group';
            
            const label = document.createElement('label');
            label.textContent = aspect.type || `Aspect ${index + 1}`;
            
            const input = document.createElement('input');
            input.type = 'text';
            input.className = 'char-aspect-value';
            input.dataset.type = aspect.type || `Aspect ${index + 1}`; // Store original type
            input.value = aspect.value;
            input.placeholder = aspect.type || `Enter aspect ${index + 1}`;
            
            div.appendChild(label);
            div.appendChild(input);
            container.appendChild(div);
        });
    }

    function populateSkills(skillsData) {
        const container = document.getElementById('skillsContainer');
        container.innerHTML = ''; // Clear existing
        (skillsData || []).forEach(skill => {
            addSkillToForm(skill.name, skill.value);
        });
    }

    function addSkillToForm(name = '', value = 0, isCustom = false) {
        const container = document.getElementById('skillsContainer');
        const entryDiv = document.createElement('div');
        entryDiv.className = 'skill-entry';

        const nameInput = document.createElement('input');
        nameInput.type = 'text';
        nameInput.className = 'skill-name';
        nameInput.value = name;
        nameInput.placeholder = 'Skill Name';
        if (!isCustom && defaultSkills.includes(name)) {
            nameInput.readOnly = true; 
            nameInput.style.backgroundColor = "var(--input-bg)";
            nameInput.style.border = "1px solid var(--text-darker)";

        }

        const valueInput = document.createElement('input');
        valueInput.type = 'number';
        valueInput.className = 'skill-value';
        valueInput.value = value;
        valueInput.min = "0";
        valueInput.max = "8"; 

        const removeBtn = document.createElement('button');
        removeBtn.type = 'button';
        removeBtn.textContent = 'X';
        removeBtn.className = 'danger remove-skill-btn';
        removeBtn.onclick = () => entryDiv.remove();
        
        entryDiv.appendChild(nameInput);
        entryDiv.appendChild(valueInput);
        if (isCustom || !defaultSkills.includes(name)) { 
             entryDiv.appendChild(removeBtn);
        }
        container.appendChild(entryDiv);
    }
    document.getElementById('addSkillBtn').addEventListener('click', () => addSkillToForm('', 0, true));

    function populateStunts(stuntsData) {
        const container = document.getElementById('stuntsContainer');
        container.innerHTML = ''; 
        (stuntsData || []).forEach(stuntText => {
            addStuntToForm(stuntText);
        });
    }

    function addStuntToForm(text = '') {
        const container = document.getElementById('stuntsContainer');
        const entryDiv = document.createElement('div');
        entryDiv.className = 'stunt-entry';

        const textarea = document.createElement('textarea');
        textarea.className = 'stunt-text';
        textarea.value = text;
        textarea.placeholder = 'Stunt description';
        textarea.rows = 2;

        const removeBtn = document.createElement('button');
        removeBtn.type = 'button';
        removeBtn.textContent = 'X';
        removeBtn.className = 'danger remove-stunt-btn';
        removeBtn.onclick = () => entryDiv.remove();
        
        entryDiv.appendChild(textarea);
        entryDiv.appendChild(removeBtn);
        container.appendChild(entryDiv);
    }
    document.getElementById('addStuntBtn').addEventListener('click', () => addStuntToForm());


    function openCharSheet(nameToLoad) {
        var safeName = getSafeName(nameToLoad);
        document.getElementById('charForm').dataset.characterName = nameToLoad;

        fetch(`char_${safeName}.json?v=${new Date().getTime()}`) 
        .then(response => response.json())
        .then(data => {
            document.getElementById('charNameTitle').textContent = data.name;
            document.getElementById('charNameDisplay').textContent = data.name;
            
            if (data.image && data.image !== '') {
                charImagePreview.src = data.image + '?' + new Date().getTime(); 
                charImagePreview.style.display = 'block';
                document.getElementById('charForm').dataset.loadedImagePath = data.image; // Store for download
            } else {
                charImagePreview.src = '#';
                charImagePreview.style.display = 'none';
                document.getElementById('charForm').dataset.loadedImagePath = '';
            }
            charImageInputModal.value = ''; 

            document.getElementById('charFatePoints').value = data.fatePoints;
            document.getElementById('charRefresh').value = data.refresh;
            
            populateAspects(data.aspects || [ 
                {type: 'High Concept', value: ''}, {type: 'Trouble', value: ''},
                {type: 'Other Aspect 1', value: ''}, {type: 'Other Aspect 2', value: ''},
                {type: 'Other Aspect 3', value: ''}
            ]);
            
            populateSkills(data.skills);
            populateStunts(data.stunts || []);

            document.getElementById('charStressPhys').value = data.stressPhysical;
            document.getElementById('charPhysStressBoxes').value = data.physicalStressBoxes;
            document.getElementById('charStressMental').value = data.stressMental;
            document.getElementById('charMentalStressBoxes').value = data.mentalStressBoxes;
            
            document.getElementById('consMild1').value = data.consequences.mild1 || (data.consequences.mild || ''); 
            document.getElementById('consMild2').value = data.consequences.mild2 || '';
            document.getElementById('consModerate').value = data.consequences.moderate || '';
            document.getElementById('consSevere').value = data.consequences.severe || '';
            
            document.getElementById('charConditions').value = data.conditions || (data.complications || ''); 

            charModal.style.display = 'flex';
        }).catch(error => console.error("Error loading character sheet:", error));
    }

    document.getElementById('saveCharBtn').addEventListener('click', function() {
        var originalName = document.getElementById('charForm').dataset.characterName;
        
        var skills = [];
        document.querySelectorAll('#skillsContainer .skill-entry').forEach(entry => {
            const name = entry.querySelector('.skill-name').value.trim();
            const value = parseInt(entry.querySelector('.skill-value').value);
            if (name) { 
                skills.push({name: name, value: isNaN(value) ? 0 : value});
            }
        });

        var stunts = [];
        document.querySelectorAll('#stuntsContainer .stunt-entry').forEach(entry => {
            const text = entry.querySelector('.stunt-text').value.trim();
            if (text) { 
                stunts.push(text);
            }
        });
        
        var aspects = [];
        document.querySelectorAll('#aspectsContainer .aspect-entry').forEach(entry => {
            const type = entry.querySelector('.char-aspect-value').dataset.type;
            const value = entry.querySelector('.char-aspect-value').value.trim();
            aspects.push({type: type, value: value});
        });

        var charData = {
            name: originalName, 
            fatePoints: parseInt(document.getElementById('charFatePoints').value),
            refresh: parseInt(document.getElementById('charRefresh').value),
            aspects: aspects,
            skills: skills,
            stunts: stunts,
            stressPhysical: parseInt(document.getElementById('charStressPhys').value),
            physicalStressBoxes: parseInt(document.getElementById('charPhysStressBoxes').value),
            stressMental: parseInt(document.getElementById('charStressMental').value),
            mentalStressBoxes: parseInt(document.getElementById('charMentalStressBoxes').value),
            consequences: {
                mild1: document.getElementById('consMild1').value,
                mild2: document.getElementById('consMild2').value,
                moderate: document.getElementById('consModerate').value,
                severe: document.getElementById('consSevere').value
            },
            conditions: document.getElementById('charConditions').value
        };

        var formData = new FormData();
        formData.append('json', JSON.stringify(charData));
        var imageFile = document.getElementById('charImageInput').files[0];
        if (imageFile) {
            formData.append('image', imageFile);
        }

        fetch('save_char.php', {
            method: 'POST',
            body: formData
        }).then(response => response.json())
        .then(data => {
            if (data.success) {
                updatePlayers(); 
                charModal.style.display = 'none';
            } else {
                alert('Error saving character: ' + (data.error || 'Unknown error'));
            }
        });
    });

    document.getElementById('downloadCharBtn').addEventListener('click', function() {
        var originalName = document.getElementById('charForm').dataset.characterName;
        if (!originalName) {
            alert("Cannot download: Character name not set.");
            return;
        }
        
        var skills = [];
        document.querySelectorAll('#skillsContainer .skill-entry').forEach(entry => {
            const name = entry.querySelector('.skill-name').value.trim();
            const value = parseInt(entry.querySelector('.skill-value').value);
            if (name) { skills.push({name: name, value: isNaN(value) ? 0 : value}); }
        });

        var stunts = [];
        document.querySelectorAll('#stuntsContainer .stunt-entry').forEach(entry => {
            const text = entry.querySelector('.stunt-text').value.trim();
            if (text) { stunts.push(text); }
        });
        
        var aspects = [];
        document.querySelectorAll('#aspectsContainer .aspect-entry').forEach(entry => {
            const type = entry.querySelector('.char-aspect-value').dataset.type;
            const value = entry.querySelector('.char-aspect-value').value.trim();
            aspects.push({type: type, value: value});
        });

        let currentImagePath = '';
        const previewSrc = document.getElementById('charImagePreview').src;
        // Check if previewSrc is a valid URL, not a blob/data URI, and from the same origin
        if (previewSrc && previewSrc !== '#' && !previewSrc.startsWith('blob:') && !previewSrc.startsWith('data:')) {
            try {
                const url = new URL(previewSrc);
                if (url.origin === window.location.origin) { 
                    currentImagePath = url.pathname.substring(1) + url.search; 
                    currentImagePath = currentImagePath.split('?')[0]; // Remove cache buster
                }
            } catch (e) { /* Not a full URL, might be already relative or just a placeholder */ }
        } else if(document.getElementById('charForm').dataset.loadedImagePath) {
            currentImagePath = document.getElementById('charForm').dataset.loadedImagePath;
        }

        var charDataToDownload = {
            name: originalName, 
            image: currentImagePath,
            fatePoints: parseInt(document.getElementById('charFatePoints').value),
            refresh: parseInt(document.getElementById('charRefresh').value),
            aspects: aspects,
            skills: skills,
            stunts: stunts,
            stressPhysical: parseInt(document.getElementById('charStressPhys').value),
            physicalStressBoxes: parseInt(document.getElementById('charPhysStressBoxes').value),
            stressMental: parseInt(document.getElementById('charStressMental').value),
            mentalStressBoxes: parseInt(document.getElementById('charMentalStressBoxes').value),
            consequences: {
                mild1: document.getElementById('consMild1').value,
                mild2: document.getElementById('consMild2').value,
                moderate: document.getElementById('consModerate').value,
                severe: document.getElementById('consSevere').value
            },
            conditions: document.getElementById('charConditions').value
        };

        const jsonData = JSON.stringify(charDataToDownload, null, 2);
        const blob = new Blob([jsonData], {type: 'application/json'});
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        const safeFileName = getSafeName(originalName) || 'character';
        a.download = `char_${safeFileName}.json`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    });

    document.getElementById('loadCharFileBtn').addEventListener('click', function() {
        document.getElementById('loadCharInput').click();
    });

    document.getElementById('loadCharInput').addEventListener('change', function(e) {
        var file = e.target.files[0];
        if (!file) return;
        var reader = new FileReader();
        reader.onload = function(e_reader) { // Renamed to avoid conflict with outer 'e'
            try {
                var data = JSON.parse(e_reader.target.result);
                document.getElementById('charFatePoints').value = data.fatePoints || 0;
                document.getElementById('charRefresh').value = data.refresh || 0;
                
                populateAspects(data.aspects || []);
                populateSkills(data.skills || []);
                populateStunts(data.stunts || []);

                document.getElementById('charStressPhys').value = data.stressPhysical || 0;
                document.getElementById('charPhysStressBoxes').value = data.physicalStressBoxes || 2;
                document.getElementById('charStressMental').value = data.stressMental || 0;
                document.getElementById('charMentalStressBoxes').value = data.mentalStressBoxes || 2;

                if (data.consequences) {
                    document.getElementById('consMild1').value = data.consequences.mild1 || data.consequences.mild || '';
                    document.getElementById('consMild2').value = data.consequences.mild2 || '';
                    document.getElementById('consModerate').value = data.consequences.moderate || '';
                    document.getElementById('consSevere').value = data.consequences.severe || '';
                }
                document.getElementById('charConditions').value = data.conditions || data.complications || '';
                
                // Store loaded image path for potential re-download if not changed by user
                document.getElementById('charForm').dataset.loadedImagePath = data.image || ''; 
                // Clear image preview and file input as we don't load image data from JSON
                charImagePreview.src = '#'; 
                charImagePreview.style.display = 'none';
                document.getElementById('charImageInput').value = ''; 

                alert( (data.name || "Imported character") + ' data loaded into form. Review and save to server, or download.');
            } catch (error) {
                alert('Error parsing character file: ' + error.message);
                console.error("Error parsing JSON:", error);
            }
        };
        reader.readAsText(file);
        e.target.value = null; 
    });

    // Initial load and periodic updates
    updatePlayers(); 
    updateChat();
    updateScene();
    setInterval(updateChat, 3000); 
    setInterval(updatePlayers, 7000); 
    setInterval(updateScene, 7000); 
});