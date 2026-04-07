// VirtualAssistant Remote Dictation Control
// Connects to SignalR hub and provides remote recording control

const elements = {
    connectionStatus: document.getElementById('connectionStatus'),
    controls: document.getElementById('controls'),
    btnDictate: document.getElementById('btnDictate'),
    btnZoneFast: document.getElementById('btnZoneFast'),
    btnZoneSlow: document.getElementById('btnZoneSlow'),
    btnEnter: document.getElementById('btnEnter'),
    btnClear: document.getElementById('btnClear'),
    dictateIcon: document.getElementById('dictateIcon'),
    dictateText: document.getElementById('dictateText'),
    fastText: document.getElementById('fastText'),
    slowText: document.getElementById('slowText'),
    transcriptionText: document.getElementById('transcriptionText'),
    btnPaste: document.getElementById('btnPaste'),
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
// Tracks whether the LAST dictation was stopped via the fast (quick) zone.
// Used to drive the auto-Enter on QuickTranscriptionCompleted.
let lastStopWasQuick = false;
let quickEnterPending = false;
let focusedApp = '';
let lastTranscription = '';
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
            // Quick mode: server sent QuickTranscriptionCompleted first, now idle → send Enter
            if (quickEnterPending) {
                quickEnterPending = false;
                console.log('Quick dictation: sending auto-Enter via PressEnter');
                connection.invoke('PressEnter').catch(function(err) {
                    console.error('Quick auto-Enter failed:', err);
                });
            }
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
        case 4: // RawTranscriptionCompleted (raw STT before LLM correction)
            if (event.text) {
                setTranscriptionText(event.text);
            }
            break;
        case 5: // QuickTranscriptionCompleted (server-sent, indicates quick mode)
            if (event.text) {
                setTranscriptionText(event.text);
            }
            quickEnterPending = true;
            console.log('Quick dictation: QuickTranscriptionCompleted received, Enter pending');
            break;
    }
}

function setConnectionStatus(connected) {
    elements.connectionStatus.textContent = connected ? 'Připojeno' : 'Odpojeno';
    elements.connectionStatus.className = 'connection-status ' + (connected ? 'connected' : 'disconnected');

    elements.btnDictate.disabled = !connected;
    elements.btnEnter.disabled = !connected;
    elements.btnClear.disabled = !connected;
    elements.btnDiscord.disabled = !connected;
    elements.btnFerdium.disabled = !connected;
    elements.btnPaste.disabled = !connected || !lastTranscription;
    for (const btn of Object.values(elements.workspaceButtons)) {
        btn.disabled = !connected;
    }
}

function setRecordingState(recording, transcribing) {
    isRecording = recording;
    isTranscribing = transcribing;

    if (recording) {
        // Show the two release zones (.controls.recording hides the single
        // dictate button and reveals .dictation-zones via CSS).
        elements.controls.classList.add('recording');
        elements.btnDictate.classList.remove('transcribing');
        elements.btnZoneFast.disabled = false;
        elements.btnZoneSlow.disabled = false;
        recordingStartTime = Date.now();
        startDurationTimer(elements.fastText, elements.slowText);
    } else if (transcribing) {
        // Recording stopped, transcription in progress. Hide the two zones,
        // show the single button in transcribing (yellow) state. The user
        // can tap it to cancel.
        elements.controls.classList.remove('recording');
        elements.btnDictate.classList.add('transcribing');
        elements.dictateIcon.textContent = '\u2715';
        elements.dictateText.textContent = 'Zrušit';
        elements.btnDictate.disabled = false;
        stopDurationTimer();
    } else {
        // Idle: single button, default label.
        elements.controls.classList.remove('recording');
        elements.btnDictate.classList.remove('transcribing');
        elements.dictateIcon.textContent = '\u25CF';
        elements.dictateText.textContent = 'Diktovat';
        elements.btnDictate.disabled = false;
        elements.fastText.textContent = 'Rychle';
        elements.slowText.textContent = 'Pomalu';
        stopDurationTimer();
    }
}

function setTranscriptionText(text) {
    elements.transcriptionText.textContent = text;
    elements.transcriptionText.classList.remove('empty');
    lastTranscription = text;
    elements.btnPaste.classList.add('visible');
    elements.btnPaste.disabled = false;
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

function startDurationTimer(...textElements) {
    stopDurationTimer();
    const targets = textElements.length > 0 ? textElements : [elements.dictateText];
    durationInterval = setInterval(() => {
        if (recordingStartTime && isRecording) {
            const elapsed = Math.floor((Date.now() - recordingStartTime) / 1000);
            const minutes = Math.floor(elapsed / 60).toString().padStart(2, '0');
            const seconds = (elapsed % 60).toString().padStart(2, '0');
            // Show elapsed time on every target. The two zone buttons each
            // get the same string so the user sees the running counter on
            // whichever side they're hovering.
            for (const t of targets) {
                t.textContent = minutes + ':' + seconds;
            }
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
function dictationHaptic() {
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
}

elements.btnDictate.addEventListener('pointerdown', () => {
    if (elements.btnDictate.disabled) return;
    dictationHaptic();
});

elements.btnZoneFast.addEventListener('pointerdown', () => {
    if (elements.btnZoneFast.disabled) return;
    dictationHaptic();
});

elements.btnZoneSlow.addEventListener('pointerdown', () => {
    if (elements.btnZoneSlow.disabled) return;
    dictationHaptic();
});

elements.btnEnter.addEventListener('pointerdown', () => {
    if (elements.btnEnter.disabled) return;
    if ('vibrate' in navigator) navigator.vibrate(50);
});

elements.btnClear.addEventListener('pointerdown', () => {
    if (elements.btnClear.disabled) return;
    if ('vibrate' in navigator) navigator.vibrate(50);
});

// Single dictate button — idle starts recording, transcribing cancels.
elements.btnDictate.addEventListener('click', async () => {
    try {
        if (isTranscribing) {
            await connection.invoke('CancelTranscription');
        } else if (!isRecording) {
            // Always start in normal mode. The user picks fast vs slow at
            // STOP time by releasing on one of the two zones below.
            lastStopWasQuick = false;
            await connection.invoke('StartDictation');
        }
    } catch (error) {
        console.error('StartDictation failed:', error);
        elements.btnDictate.disabled = false;
    }
});

// Fast zone — release here = quick processing (no LLM, auto-Enter).
elements.btnZoneFast.addEventListener('click', async () => {
    if (!isRecording) return;
    try {
        elements.btnZoneFast.disabled = true;
        elements.btnZoneSlow.disabled = true;
        lastStopWasQuick = true;
        await connection.invoke('StopDictationWithMode', true);
    } catch (error) {
        console.error('StopDictationWithMode(true) failed:', error);
        elements.btnZoneFast.disabled = false;
        elements.btnZoneSlow.disabled = false;
    }
});

// Slow zone — release here = LLM-corrected processing.
elements.btnZoneSlow.addEventListener('click', async () => {
    if (!isRecording) return;
    try {
        elements.btnZoneFast.disabled = true;
        elements.btnZoneSlow.disabled = true;
        lastStopWasQuick = false;
        await connection.invoke('StopDictationWithMode', false);
    } catch (error) {
        console.error('StopDictationWithMode(false) failed:', error);
        elements.btnZoneFast.disabled = false;
        elements.btnZoneSlow.disabled = false;
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

// Paste transcription handler
elements.btnPaste.addEventListener('pointerdown', () => {
    if (elements.btnPaste.disabled) return;
    if ('vibrate' in navigator) navigator.vibrate(50);
});

elements.btnPaste.addEventListener('click', async () => {
    if (!lastTranscription || connection?.state !== signalR.HubConnectionState.Connected) return;
    try {
        elements.btnPaste.disabled = true;
        await connection.invoke('PasteTranscription', lastTranscription);
    } catch (error) {
        console.error('Paste failed:', error);
    } finally {
        elements.btnPaste.disabled = false;
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
