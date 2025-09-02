// Lightweight HTTP helpers and response normalization for incremental migration

// Normalize various API response shapes to { success, data, message }
function normalizeResponse(raw) {
    try {
        if (raw && typeof raw === 'object') {
            const hasSuccess = Object.prototype.hasOwnProperty.call(raw, 'success');
            const hasData = Object.prototype.hasOwnProperty.call(raw, 'data');
            const hasMessage = Object.prototype.hasOwnProperty.call(raw, 'message');

            if (hasSuccess) {
                return {
                    success: !!raw.success,
                    data: hasData ? raw.data : raw,
                    message: hasMessage ? (raw.message || '') : ''
                };
            }
            // Fallback: treat object as data with implicit success
            return { success: true, data: raw, message: '' };
        }
        // Primitive/empty: treat as success with no data
        return { success: true, data: raw, message: '' };
    } catch (e) {
        return { success: false, data: null, message: 'Failed to parse response' };
    }
}

// JSON POST helper
function apiPostJson(url, body, headers = {}) {
    return fetch(url, {
        method: 'POST',
        headers: Object.assign({ 'Content-Type': 'application/json' }, headers),
        body: JSON.stringify(body || {})
    })
    .then(r => r.json())
    .then(normalizeResponse);
}

// Form POST helper
function apiPostForm(url, formDataOrParams) {
    // Accept FormData or URLSearchParams; if string provided, pass through
    let body = formDataOrParams;
    let headers = {};
    if (typeof formDataOrParams === 'string') {
        body = formDataOrParams;
        headers['Content-Type'] = 'application/x-www-form-urlencoded';
    }
    return fetch(url, { method: 'POST', body, headers })
        .then(r => r.json())
        .then(normalizeResponse);
}

// Generic GET JSON helper
function apiGetJson(url) {
    return fetch(url)
        .then(r => r.json())
        .then(normalizeResponse);
}

// JSON PUT helper
function apiPutJson(url, body, headers = {}) {
    return fetch(url, {
        method: 'PUT',
        headers: Object.assign({ 'Content-Type': 'application/json' }, headers),
        body: JSON.stringify(body || {})
    })
    .then(r => r.json())
    .then(normalizeResponse);
}

// JSON DELETE helper
function apiDeleteJson(url) {
    return fetch(url, { method: 'DELETE' })
        .then(r => r.json())
        .then(normalizeResponse);
}

// Small helper to bubble errors via notification when available
function notifyError(message) {
    if (typeof showNotification === 'function') {
        showNotification(message || 'An error occurred', 'error');
    } else {
        console.error(message || 'An error occurred');
    }
}
