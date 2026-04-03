// VirtualAssistant Remote Dictation Control
// Connects to SignalR hub and provides remote recording control

const elements = {
    connectionStatus: document.getElementById('connectionStatus'),
    btnToggle: document.getElementById('btnToggle'),
    btnEnter: document.getElementById('btnEnter'),
    btnClear: document.getElementById('btnClear'),
    toggleIcon: document.getElementById('toggleIcon'),
    toggleText: document.getElementById('toggleText'),
    transcriptionText: document.getElementById('transcriptionText'),
    btnDiscord: document.getElementById('btnDiscord'),
    btnFerdium: document.getElementById('btnFerdium'),
    workspaceButtons: {
        1: document.getElementById('btnWorkspace1'),
        4: document.getElementById('btnWorkspace4'),
        5: document.getElementById('btnWorkspace5'),
        6: document.getElementById('btnWorkspace6')
    }
};

let connection = null;
let isRecording = false;
let isTranscribing = false;
let focusedApp = '';
let durationInterval = null;
let recordingStartTime = null;
let currentWorkspace = 0;
let totalWorkspaces = 0;

function buildConnection() {
    const hubUrl = window.location.origin + '/hubs/dictation';

    connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Information)
        .build();

    connection.onreconnecting((error) => {
        console.log('Reconnecting...', error);
        setConnectionStatus(false);
    });

    connection.onreconnected((connectionId) => {
        console.log('Reconnected:', connectionId);
        setConnectionStatus(true);
        refreshStatus();
    });

    connection.onclose((error) => {
        console.log('Connection closed:', error);
        setConnectionStatus(false);
    });

    connection.on('DictationEvent', handleDictationEvent);
    connection.on('AppFocusChanged', handleAppFocusChanged);
    connection.on('WorkspaceChanged', handleWorkspaceChanged);
    connection.on('Connected', (connectionId) => {
        console.log('Connected with ID:', connectionId);
    });
}

function handleDictationEvent(event) {
    console.log('DictationEvent:', event);

    switch (event.eventType) {
        case 0: // RecordingStarted
            setRecordingState(true, false);
            break;
        case 1: // RecordingStopped
            setRecordingState(false, false);
            break;
        case 2: // TranscriptionStarted
            setRecordingState(false, true);
            break;
        case 3: // TranscriptionCompleted
            setRecordingState(false, false);
            if (event.text) {
                setTranscriptionText(event.text);
            }
            break;
    }
}

function setConnectionStatus(connected) {
    elements.connectionStatus.textContent = connected ? 'Připojeno' : 'Odpojeno';
    elements.connectionStatus.className = 'connection-status ' + (connected ? 'connected' : 'disconnected');

    elements.btnToggle.disabled = !connected;
    elements.btnEnter.disabled = !connected;
    elements.btnClear.disabled = !connected;
    elements.btnDiscord.disabled = !connected;
    elements.btnFerdium.disabled = !connected;
    for (const btn of Object.values(elements.workspaceButtons)) {
        btn.disabled = !connected;
    }
}

function setRecordingState(recording, transcribing) {
    isRecording = recording;
    isTranscribing = transcribing;

    if (recording) {
        elements.btnToggle.classList.remove('transcribing');
        elements.btnToggle.classList.add('recording');
        elements.toggleIcon.textContent = '\u25A0';
        elements.toggleText.textContent = 'Stop';
        elements.btnToggle.disabled = false;
        recordingStartTime = Date.now();
        startDurationTimer();
    } else if (transcribing) {
        elements.btnToggle.classList.remove('recording');
        elements.btnToggle.classList.add('transcribing');
        elements.toggleIcon.textContent = '\u2715';
        elements.toggleText.textContent = 'Zrušit';
        elements.btnToggle.disabled = false;
        stopDurationTimer();
    } else {
        elements.btnToggle.classList.remove('recording', 'transcribing');
        elements.toggleIcon.textContent = '\u25CF';
        elements.toggleText.textContent = 'Diktovat';
        elements.btnToggle.disabled = false;
        stopDurationTimer();
    }
}

function setTranscriptionText(text) {
    elements.transcriptionText.textContent = text;
    elements.transcriptionText.classList.remove('empty');
}

function handleAppFocusChanged(wmClass) {
    focusedApp = (wmClass || '').toLowerCase();
    updateAppButton(elements.btnDiscord, 'discord', 'Discord');
    updateAppButton(elements.btnFerdium, 'ferdium', 'Ferdium');
}

function handleWorkspaceChanged(workspace, total) {
    console.log('WorkspaceChanged:', workspace, '/', total);
    currentWorkspace = workspace;
    totalWorkspaces = total;
    updateWorkspaceButtons();
}

function updateWorkspaceButtons() {
    for (const [num, btn] of Object.entries(elements.workspaceButtons)) {
        const wsNum = parseInt(num);
        // Workspace 1 always visible, others based on totalWorkspaces
        if (wsNum === 1) {
            btn.classList.remove('hidden');
        } else if (totalWorkspaces >= wsNum) {
            btn.classList.remove('hidden');
        } else {
            btn.classList.add('hidden');
        }
        // Highlight active workspace
        if (wsNum === currentWorkspace) {
            btn.classList.add('active');
        } else {
            btn.classList.remove('active');
        }
    }
}

function updateAppButton(btn, wmClass, label) {
    const isActive = focusedApp === wmClass;
    if (isActive) {
        btn.style.borderBottom = '4px solid #f44336';
        btn.querySelector('.app-label').textContent = 'Close ' + label;
    } else {
        btn.style.borderBottom = 'none';
        btn.querySelector('.app-label').textContent = label;
    }
}

function startDurationTimer() {
    stopDurationTimer();
    durationInterval = setInterval(() => {
        if (recordingStartTime && isRecording) {
            const elapsed = Math.floor((Date.now() - recordingStartTime) / 1000);
            const minutes = Math.floor(elapsed / 60).toString().padStart(2, '0');
            const seconds = (elapsed % 60).toString().padStart(2, '0');
            elements.toggleText.textContent = 'Nahrávání ' + minutes + ':' + seconds;
        }
    }, 1000);
}

function stopDurationTimer() {
    if (durationInterval) {
        clearInterval(durationInterval);
        durationInterval = null;
    }
}

async function refreshStatus() {
    try {
        const status = await connection.invoke('GetStatus');
        console.log('Status:', status);
        setRecordingState(status.isRecording, status.isTranscribing);

        const focused = await connection.invoke('GetFocusedApp');
        handleAppFocusChanged(focused);

        const wsInfo = await connection.invoke('GetWorkspaceInfo');
        handleWorkspaceChanged(wsInfo.currentWorkspace, wsInfo.totalWorkspaces);
    } catch (error) {
        console.error('Failed to get status:', error);
    }
}

// Haptic feedback
elements.btnToggle.addEventListener('pointerdown', () => {
    if (elements.btnToggle.disabled) return;
    if ('vibrate' in navigator) {
        let pattern;
        if (isTranscribing) {
            pattern = 30;
        } else if (isRecording) {
            pattern = 50;
        } else {
            pattern = [100, 50, 100];
        }
        navigator.vibrate(pattern);
    }
});

elements.btnEnter.addEventListener('pointerdown', () => {
    if (elements.btnEnter.disabled) return;
    if ('vibrate' in navigator) navigator.vibrate(50);
});

elements.btnClear.addEventListener('pointerdown', () => {
    if (elements.btnClear.disabled) return;
    if ('vibrate' in navigator) navigator.vibrate(50);
});

// Button handlers
elements.btnToggle.addEventListener('click', async () => {
    try {
        if (isTranscribing) {
            await connection.invoke('CancelTranscription');
        } else {
            await connection.invoke('ToggleRecording');
        }
    } catch (error) {
        console.error('Toggle failed:', error);
        elements.btnToggle.disabled = false;
    }
});

elements.btnEnter.addEventListener('click', async () => {
    try {
        elements.btnEnter.disabled = true;
        await connection.invoke('PressEnter');
    } catch (error) {
        console.error('Enter failed:', error);
    } finally {
        elements.btnEnter.disabled = false;
    }
});

elements.btnClear.addEventListener('click', async () => {
    try {
        elements.btnClear.disabled = true;
        await connection.invoke('ClearText');
    } catch (error) {
        console.error('Clear failed:', error);
    } finally {
        elements.btnClear.disabled = false;
    }
});

// App launcher haptic feedback
elements.btnDiscord.addEventListener('pointerdown', () => {
    if (elements.btnDiscord.disabled) return;
    if ('vibrate' in navigator) navigator.vibrate(50);
});

elements.btnFerdium.addEventListener('pointerdown', () => {
    if (elements.btnFerdium.disabled) return;
    if ('vibrate' in navigator) navigator.vibrate(50);
});

// App launcher handlers
elements.btnDiscord.addEventListener('click', async () => {
    try {
        elements.btnDiscord.disabled = true;
        if (focusedApp === 'discord') {
            await connection.invoke('CloseApp', 'discord');
        } else {
            await connection.invoke('ActivateApp', 'discord');
        }
    } catch (error) {
        console.error('Discord action failed:', error);
    } finally {
        if (connection.state === signalR.HubConnectionState.Connected) {
            elements.btnDiscord.disabled = false;
        }
    }
});

elements.btnFerdium.addEventListener('click', async () => {
    try {
        elements.btnFerdium.disabled = true;
        if (focusedApp === 'ferdium') {
            await connection.invoke('CloseApp', 'ferdium');
        } else {
            await connection.invoke('ActivateApp', 'ferdium');
        }
    } catch (error) {
        console.error('Ferdium action failed:', error);
    } finally {
        if (connection.state === signalR.HubConnectionState.Connected) {
            elements.btnFerdium.disabled = false;
        }
    }
});

// Workspace button handlers
for (const [num, btn] of Object.entries(elements.workspaceButtons)) {
    const wsNum = parseInt(num);

    btn.addEventListener('pointerdown', () => {
        if (btn.disabled) return;
        if ('vibrate' in navigator) navigator.vibrate(50);
    });

    btn.addEventListener('click', async () => {
        try {
            btn.disabled = true;
            await connection.invoke('SwitchWorkspace', wsNum);
        } catch (error) {
            console.error('SwitchWorkspace failed:', error);
        } finally {
            if (connection.state === signalR.HubConnectionState.Connected) {
                btn.disabled = false;
            }
        }
    });
}

// Initialize
async function initialize() {
    buildConnection();

    try {
        await connection.start();
        console.log('SignalR connected');
        setConnectionStatus(true);
        await refreshStatus();
    } catch (error) {
        console.error('Failed to connect:', error);
        setConnectionStatus(false);
        setTimeout(initialize, 5000);
    }
}

initialize();
