// VirtualAssistant Remote Dictation Control
// Connects to SignalR hub and provides remote recording control

// Bumped on every script change. Rendered next to the connection status so
// the user can verify the phone is actually running the latest JS and not a
// stale cached copy. MUST match the ?v= query string in remote.html.
const SCRIPT_VERSION = 'v28';

const elements = {
    connectionStatus: document.getElementById('connectionStatus'),
    debugLogContent: document.getElementById('debugLogContent'),
    controls: document.getElementById('controls'),
    btnDictate: document.getElementById('btnDictate'),
    btnZoneFast: document.getElementById('btnZoneFast'),
    btnZoneSlow: document.getElementById('btnZoneSlow'),
    btnEnter: document.getElementById('btnEnter'),
    enterIcon: document.getElementById('enterIcon'),
    enterText: document.getElementById('enterText'),
    btnClear: document.getElementById('btnClear'),
    dictateIcon: document.getElementById('dictateIcon'),
    dictateText: document.getElementById('dictateText'),
    fastText: document.getElementById('fastText'),
    slowText: document.getElementById('slowText'),
    recordTimer: document.getElementById('recordTimer'),
    transcriptionText: document.getElementById('transcriptionText'),
    btnPaste: document.getElementById('btnPaste'),
    btnClipboardPaste: document.getElementById('btnClipboardPaste'),
    btnScreenshotPaste: document.getElementById('btnScreenshotPaste'),
    btnDiscord: document.getElementById('btnDiscord'),
    btnFerdium: document.getElementById('btnFerdium'),
    workspaceButtons: {
        1: document.getElementById('btnWorkspace1'),
        2: document.getElementById('btnWorkspace2'),
        3: document.getElementById('btnWorkspace3'),
        4: document.getElementById('btnWorkspace4'),
        5: document.getElementById('btnWorkspace5'),
        6: document.getElementById('btnWorkspace6'),
        7: document.getElementById('btnWorkspace7'),
        8: document.getElementById('btnWorkspace8')
    }
};

let connection = null;
let isRecording = false;
let isTranscribing = false;
// Set when the server emits QuickTranscriptionCompleted (event 5). After
// the recording fully stops we then send PressEnter on the user's behalf.
let quickEnterPending = false;
// Tracks the zone the user is hovering over while dragging from the
// dictate button to a release zone — used by the press-and-release
// gesture handler. null while no drag is in progress.
let dragHoveredZone = null;
// Coordinates of the most recent pointerdown that started a dictate
// gesture. Used by the global pointerup handler to decide whether the
// user actually slid (press-and-release UX) or just tapped (tap-to-start).
// Without this distinction, the immediate UI swap to Rychle/Pomalu makes
// every tap end on a zone — instantly stopping the recording with 0 bytes.
let dictateGestureStartX = 0;
let dictateGestureStartY = 0;
// Pixel distance threshold above which we treat the gesture as a slide.
// Below this, the user merely tapped — keep recording, do not select a zone.
const SLIDE_THRESHOLD_PX = 40;
// Minimum age of a recording (in ms) before zone-stop interactions are
// allowed. The CSS swap from btnDictate → zones happens in the same JS
// turn as setRecordingState(true), and on touch devices the original tap
// can synthesize a click on whatever element is under the finger at
// release time. Without this guard, that synthetic click can hit a zone
// and instantly stop a recording the user just started — yielding the
// "0 bytes captured" log pattern. 300 ms is comfortably longer than any
// touch screen's tap-to-release latency and shorter than any human's
// deliberate "start, then stop" double-tap.
const ZONE_STOP_GUARD_MS = 300;
// Maximum number of debug log lines kept on screen. 8 was too small —
// one full dictation cycle generates ~10 log entries (pointerdown,
// invoke, OK, setRecordingState true, pointerup, SR event 0, SR event 2,
// setRecordingState transcribing, SR event 4, SR event 3,
// setRecordingState idle, SR event 1) so any earlier failed-tap evidence
// gets pushed out before the user can screenshot it.
const DEBUG_LOG_MAX = 100;
const debugLogLines = [];

function debugLog(message) {
    const timestamp = new Date().toLocaleTimeString('cs-CZ', { hour12: false });
    debugLogLines.push(timestamp + ' ' + message);
    if (debugLogLines.length > DEBUG_LOG_MAX) {
        debugLogLines.shift();
    }
    if (elements.debugLogContent) {
        elements.debugLogContent.textContent = debugLogLines.join('\n');
        // Auto-scroll the debug-log container (parent of content) to bottom
        // so the newest entry is always visible without manual scroll.
        const container = elements.debugLogContent.parentElement;
        if (container) container.scrollTop = container.scrollHeight;
    }
    console.log('[debug]', message);
}
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
    debugLog('SR event ' + event.eventType + (event.text ? ' "' + event.text.slice(0, 20) + '"' : ''));

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
    // Always include the script version so we can tell at a glance whether
    // a stale cached copy of remote.js is being used.
    const label = connected ? 'Připojeno' : 'Odpojeno';
    elements.connectionStatus.textContent = label + ' · ' + SCRIPT_VERSION;
    elements.connectionStatus.className = 'connection-status ' + (connected ? 'connected' : 'disconnected');

    elements.btnDictate.disabled = !connected;
    elements.btnEnter.disabled = !connected;
    elements.btnClear.disabled = !connected;
    if (elements.btnClipboardPaste) elements.btnClipboardPaste.disabled = !connected;
    if (elements.btnScreenshotPaste) {
        if (!connected) {
            elements.btnScreenshotPaste.classList.add('hidden');
            elements.btnScreenshotPaste.disabled = true;
        }
    }
    elements.btnDiscord.disabled = !connected;
    elements.btnFerdium.disabled = !connected;
    elements.btnPaste.disabled = !connected || !lastTranscription;
    // Force the recording zones disabled while offline. setRecordingState()
    // re-enables them only when a new recording actually starts after the
    // connection is restored, so we never enable them here. Without this,
    // hidden but focusable zone buttons could still receive haptics/clicks
    // from a stale state when the SignalR connection drops.
    if (!connected) {
        elements.btnZoneFast.disabled = true;
        elements.btnZoneSlow.disabled = true;
    }
    for (const btn of Object.values(elements.workspaceButtons)) {
        btn.disabled = !connected;
    }
}

function setRecordingState(recording, transcribing) {
    // Idempotent: skip if state already matches. Prevents the timer from
    // resetting when a duplicate event arrives (e.g. invoke success path
    // and SignalR broadcast both call this with the same state).
    if (isRecording === recording && isTranscribing === transcribing) return;

    debugLog('setRecordingState rec=' + recording + ' tx=' + transcribing);
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
        // Show duration on the dedicated timer element so the zone labels
        // ("Rychle" / "Pomalu") stay visible during recording.
        elements.recordTimer.textContent = '00:00';
        startDurationTimer();

        // Swap Enter → Zrušit during recording
        elements.btnEnter.classList.add('cancel-mode');
        elements.enterIcon.textContent = '\u2715';
        elements.enterText.textContent = 'Zrušit';
    } else if (transcribing) {
        // Recording stopped, transcription in progress. Hide the two zones,
        // show the single button in transcribing (yellow) state. The user
        // can tap it to cancel.
        elements.controls.classList.remove('recording');
        elements.btnDictate.classList.add('transcribing');
        elements.dictateIcon.textContent = '\u2715';
        elements.dictateText.textContent = 'Zrušit';
        elements.btnDictate.disabled = false;
        elements.btnZoneFast.disabled = true;
        elements.btnZoneSlow.disabled = true;
        elements.recordTimer.textContent = '';
        stopDurationTimer();
        restoreEnterButton();
    } else {
        // Idle: single button, default label.
        elements.controls.classList.remove('recording');
        elements.btnDictate.classList.remove('transcribing');
        elements.dictateIcon.textContent = '\u25CF';
        elements.dictateText.textContent = 'Diktovat';
        elements.btnDictate.disabled = false;
        elements.btnZoneFast.disabled = true;
        elements.btnZoneSlow.disabled = true;
        elements.recordTimer.textContent = '';
        stopDurationTimer();
        restoreEnterButton();
    }
}

function restoreEnterButton() {
    elements.btnEnter.classList.remove('cancel-mode');
    elements.enterIcon.textContent = '\u21B5';
    elements.enterText.textContent = 'Enter';
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

function startDurationTimer() {
    stopDurationTimer();
    durationInterval = setInterval(() => {
        if (recordingStartTime && isRecording) {
            const elapsed = Math.floor((Date.now() - recordingStartTime) / 1000);
            const minutes = Math.floor(elapsed / 60).toString().padStart(2, '0');
            const seconds = (elapsed % 60).toString().padStart(2, '0');
            // Update the dedicated timer element above the two zones, so
            // the zone labels ("Rychle" / "Pomalu") stay visible.
            elements.recordTimer.textContent = minutes + ':' + seconds;
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

        await checkScreenshotAvailability();
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

// Dictation gesture model
// =======================
//
// Two interaction styles, both supported:
//
//   A) Press-and-release (mobile):
//        1. pointerdown on btnDictate → start recording
//        2. user keeps the finger pressed and slides over to one of the
//           two release zones
//        3. pointerup on either zone → stop with that mode
//        4. pointerup outside any zone (e.g. user lifted off mid-button)
//           → default to slow (LLM-corrected) so the user does not lose
//           audio because of an off-target release
//
//   B) Tap / click (desktop, or mobile users who prefer two taps):
//        1. tap btnDictate → start recording
//        2. tap a zone → stop with that mode
//
// Both styles share the same SignalR calls. The press-and-release path
// is implemented via a global pointerup listener so we can detect
// release on a zone even when the pointer started on btnDictate.

let dictateGestureInProgress = false;
let dictateStartedFromGesture = false;

function getZoneAtPoint(clientX, clientY) {
    const target = document.elementFromPoint(clientX, clientY);
    if (!target) return null;
    if (target === elements.btnZoneFast || elements.btnZoneFast.contains(target)) return 'fast';
    if (target === elements.btnZoneSlow || elements.btnZoneSlow.contains(target)) return 'slow';
    return null;
}

async function startDictationFromGesture() {
    if (isRecording || isTranscribing) return;
    try {
        debugLog('invoke StartDictation');
        await connection.invoke('StartDictation');
        debugLog('StartDictation OK → setRecordingState(true)');
        // Update the UI to recording state immediately. Do not wait for the
        // SignalR broadcast — on flaky transports (long polling on mobile,
        // congested WiFi) the RecordingStarted event can be delayed enough
        // that the user is left looking at the blue Diktovat button while
        // the server is already recording, with no way to stop. The server
        // returning success from StartDictation guarantees the state has
        // already transitioned. The follow-up broadcast is now idempotent.
        setRecordingState(true, false);
    } catch (error) {
        debugLog('StartDictation FAIL: ' + error);
        elements.btnDictate.disabled = false;
        dictateStartedFromGesture = false;
    }
}

async function stopDictationWithMode(quick, source) {
    source = source || 'unknown';
    if (!isRecording) {
        debugLog('stopDictationWithMode(' + quick + ') ignored from ' + source + ': isRecording=false');
        return;
    }
    // Time guard: refuse to stop a recording that just started — but
    // ONLY for synthetic-click stop paths (zone click handlers fired by
    // the browser after the original btnDictate tap). The deliberate
    // press-and-release gesture (source='global-pointerup-slide') is
    // explicitly exempt: a user CAN intentionally do a sub-300ms press-
    // and-release to fire-and-forget a single word, and we must not
    // block that.
    //
    // Touch devices can synthesize a click on the zone that replaced
    // btnDictate in the same JS turn the user tapped, instantly killing
    // the 0-byte recording the user just initiated. Anything stopping a
    // recording within ZONE_STOP_GUARD_MS of its start via a click
    // handler is treated as that synthetic click and ignored. A
    // deliberate tap-to-stop (after the user can SEE the zone) will
    // always be later than 300 ms.
    const age = recordingStartTime ? (Date.now() - recordingStartTime) : Infinity;
    const guardApplies = source !== 'global-pointerup-slide';
    if (guardApplies && age < ZONE_STOP_GUARD_MS) {
        debugLog('stopDictationWithMode(' + quick + ') BLOCKED from ' + source + ': age=' + age + 'ms < ' + ZONE_STOP_GUARD_MS + 'ms guard');
        return;
    }
    try {
        debugLog('stopDictationWithMode(' + quick + ') from ' + source + ': age=' + age + 'ms');
        elements.btnZoneFast.disabled = true;
        elements.btnZoneSlow.disabled = true;
        await connection.invoke('StopDictationWithMode', quick);
    } catch (error) {
        debugLog('StopDictationWithMode FAIL: ' + error);
        elements.btnZoneFast.disabled = false;
        elements.btnZoneSlow.disabled = false;
    }
}

elements.btnDictate.addEventListener('pointerdown', async (ev) => {
    if (elements.btnDictate.disabled) {
        debugLog('btnDictate pointerdown IGNORED: disabled');
        return;
    }
    dictationHaptic();
    // Cancel transcription state has its own click handler below;
    // pointerdown only kicks off a fresh recording.
    if (isTranscribing) {
        debugLog('btnDictate pointerdown IGNORED: isTranscribing=true');
        return;
    }
    if (isRecording) {
        debugLog('btnDictate pointerdown IGNORED: isRecording=true');
        return;
    }
    if (!isRecording) {
        dictateGestureInProgress = true;
        // Capture pointerdown coordinates so the global pointerup handler
        // can tell whether the user actually slid (press-and-release UX)
        // or just tapped. Since the immediate UI swap puts a Rychle/Pomalu
        // zone at the same pixel where Diktovat was, a plain tap would
        // otherwise instantly stop the recording with 0 bytes captured.
        dictateGestureStartX = ev.clientX;
        dictateGestureStartY = ev.clientY;
        // Set the "started from gesture" flag synchronously BEFORE the
        // await so the click event (which fires after pointerup, before
        // any awaits resolve) sees it and skips the duplicate StartDictation
        // call. Without this guard, both pointerdown and click race to
        // invoke StartDictation, and the second call ends up as a server
        // no-op that yields no broadcast — leaving the UI stuck.
        dictateStartedFromGesture = true;
        debugLog('pointerdown btnDictate (' + ev.clientX + ',' + ev.clientY + ')');
        try { ev.target.releasePointerCapture?.(ev.pointerId); } catch (_) {}
        await startDictationFromGesture();
    }
});

elements.btnZoneFast.addEventListener('pointerdown', () => {
    if (elements.btnZoneFast.disabled) return;
    dictationHaptic();
});

elements.btnZoneSlow.addEventListener('pointerdown', () => {
    if (elements.btnZoneSlow.disabled) return;
    dictationHaptic();
});

// Global pointerup — handles the press-and-release gesture (style A).
// Fires whether the release happens on a zone, the dictate button, or
// somewhere else entirely. Decides what to do based on the element under
// the pointer at release time.
document.addEventListener('pointerup', async (ev) => {
    if (!dictateGestureInProgress) return;
    dictateGestureInProgress = false;

    if (!isRecording) {
        debugLog('global pointerup: isRecording=false, ignoring zone check');
        return;
    }

    // Slide detection: if the user did not move the pointer beyond the
    // threshold, this was a plain tap. The immediate UI swap puts a
    // zone at the same pixel where Diktovat was — a tap would otherwise
    // hit a zone and instantly stop the recording with 0 bytes. Only
    // honour the zone selection when the user actually slid.
    const dx = ev.clientX - dictateGestureStartX;
    const dy = ev.clientY - dictateGestureStartY;
    const distance = Math.sqrt(dx * dx + dy * dy);
    debugLog('global pointerup d=' + distance.toFixed(0) + 'px (start=' + dictateGestureStartX + ',' + dictateGestureStartY + ' end=' + ev.clientX + ',' + ev.clientY + ')');
    if (distance < SLIDE_THRESHOLD_PX) {
        // Tap, not a slide. Keep recording — the user will tap a zone
        // separately when they want to stop.
        return;
    }

    const zone = getZoneAtPoint(ev.clientX, ev.clientY);
    if (zone === 'fast') {
        await stopDictationWithMode(true, 'global-pointerup-slide');
    } else if (zone === 'slow') {
        await stopDictationWithMode(false, 'global-pointerup-slide');
    }
    // No zone under the pointer → keep recording. The user can either
    // tap a zone (style B) or press-and-release again to stop.
});

elements.btnEnter.addEventListener('pointerdown', () => {
    if (elements.btnEnter.disabled) return;
    if ('vibrate' in navigator) navigator.vibrate(50);
});

elements.btnClear.addEventListener('pointerdown', () => {
    if (elements.btnClear.disabled) return;
    if ('vibrate' in navigator) navigator.vibrate(50);
});

// Click fallback (style B): tap-to-start, tap-to-stop, tap-to-cancel.
elements.btnDictate.addEventListener('click', async () => {
    // pointerdown already handled the press-and-release start; this only
    // fires when the user did a clean click (e.g. desktop mouse) AND
    // pointerdown didn't start recording.
    if (dictateStartedFromGesture) {
        dictateStartedFromGesture = false;
        return;
    }
    try {
        if (isTranscribing) {
            await connection.invoke('CancelTranscription');
        } else if (!isRecording) {
            await connection.invoke('StartDictation');
            // Same defensive UI update as the gesture path: don't wait
            // for the SignalR broadcast to confirm recording started.
            setRecordingState(true, false);
        }
    } catch (error) {
        console.error('StartDictation failed:', error);
        elements.btnDictate.disabled = false;
    }
});

elements.btnZoneFast.addEventListener('click', () => {
    debugLog('btnZoneFast click handler fired');
    stopDictationWithMode(true, 'btnZoneFast-click');
});
elements.btnZoneSlow.addEventListener('click', () => {
    debugLog('btnZoneSlow click handler fired');
    stopDictationWithMode(false, 'btnZoneSlow-click');
});

elements.btnEnter.addEventListener('click', async () => {
    try {
        elements.btnEnter.disabled = true;
        if (isRecording) {
            // Cancel recording — discard audio
            await connection.invoke('CancelTranscription');
        } else {
            await connection.invoke('PressEnter');
        }
    } catch (error) {
        console.error('Enter/Cancel failed:', error);
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

// Screenshot availability check
async function checkScreenshotAvailability() {
    if (!elements.btnScreenshotPaste) return;
    try {
        if (connection?.state !== signalR.HubConnectionState.Connected) return;
        const available = await connection.invoke('IsScreenshotAvailable');
        if (available) {
            elements.btnScreenshotPaste.classList.remove('hidden');
            elements.btnScreenshotPaste.disabled = false;
        } else {
            elements.btnScreenshotPaste.classList.add('hidden');
            elements.btnScreenshotPaste.disabled = true;
        }
    } catch (error) {
        console.error('Screenshot check failed:', error);
    }
}

// Poll screenshot availability every 15s
setInterval(checkScreenshotAvailability, 15000);

// Screenshot paste handler
if (elements.btnScreenshotPaste) {
    elements.btnScreenshotPaste.addEventListener('pointerdown', () => {
        if (elements.btnScreenshotPaste.disabled) return;
        if ('vibrate' in navigator) navigator.vibrate(50);
    });

    elements.btnScreenshotPaste.addEventListener('click', async () => {
        if (connection?.state !== signalR.HubConnectionState.Connected) return;
        try {
            elements.btnScreenshotPaste.disabled = true;
            await connection.invoke('InsertScreenshotPath');
        } catch (error) {
            console.error('InsertScreenshotPath failed:', error);
        } finally {
            elements.btnScreenshotPaste.disabled = false;
        }
    });
}

// Clipboard paste handler (guarded for stale HTML without the button)
if (elements.btnClipboardPaste) {
    elements.btnClipboardPaste.addEventListener('pointerdown', () => {
        if (elements.btnClipboardPaste.disabled) return;
        if ('vibrate' in navigator) navigator.vibrate(50);
    });

    elements.btnClipboardPaste.addEventListener('click', async () => {
        if (connection?.state !== signalR.HubConnectionState.Connected) return;
        try {
            elements.btnClipboardPaste.disabled = true;
            await connection.invoke('PasteFromClipboard');
        } catch (error) {
            console.error('PasteFromClipboard failed:', error);
        } finally {
            elements.btnClipboardPaste.disabled = false;
        }
    });
}

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
    // Render the script version into the status indicator before any
    // network activity, so even on a complete failure to reach the server
    // the user can still see which JS version the browser actually loaded.
    setConnectionStatus(false);
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
