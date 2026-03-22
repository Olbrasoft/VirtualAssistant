// VirtualAssistant Remote Dictation Control
// Connects to SignalR hub and provides remote recording control

const elements = {
    connectionStatus: document.getElementById('connectionStatus'),
    btnToggle: document.getElementById('btnToggle'),
    btnEnter: document.getElementById('btnEnter'),
    btnClear: document.getElementById('btnClear'),
    toggleIcon: document.getElementById('toggleIcon'),
    toggleText: document.getElementById('toggleText'),
    transcriptionText: document.getElementById('transcriptionText')
};

let connection = null;
let isRecording = false;
let isTranscribing = false;
let durationInterval = null;
let recordingStartTime = null;

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
