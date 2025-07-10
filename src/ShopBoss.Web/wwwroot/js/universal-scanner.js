// Universal Scanner JavaScript Module
class UniversalScanner {
    constructor(containerId, options = {}) {
        this.containerId = containerId;
        this.station = options.station || 'Unknown';
        this.autoFocus = options.autoFocus !== false;
        this.clearOnSuccess = options.clearOnSuccess !== false;
        this.showRecentScans = options.showRecentScans !== false;
        
        this.input = document.getElementById(`scanner-input-${containerId}`);
        this.submitButton = document.getElementById(`scanner-submit-${containerId}`);
        this.clearButton = document.getElementById(`scanner-clear-${containerId}`);
        this.statusDiv = document.getElementById(`scanner-status-${containerId}`);
        this.resultsDiv = document.getElementById(`scanner-results-${containerId}`);
        this.recentScansDiv = document.getElementById(`recent-scans-${containerId}`);
        this.statusIndicator = document.getElementById(`scanner-status-indicator-${containerId}`);
        this.toggleIcon = document.getElementById(`scanner-toggle-${containerId}`);
        this.bodyElement = document.getElementById(`scanner-body-${containerId}`);
        
        this.isProcessing = false;
        this.lastScanTime = 0;
        this.scanCooldown = 1000; // 1 second cooldown between scans
        
        this.init();
    }
    
    init() {
        if (!this.input) {
            console.error(`Scanner input not found for container: ${this.containerId}`);
            return;
        }
        
        // Event listeners
        this.input.addEventListener('keydown', (e) => this.handleKeydown(e));
        this.input.addEventListener('input', (e) => this.handleInput(e));
        this.submitButton?.addEventListener('click', () => this.processScan());
        this.clearButton?.addEventListener('click', () => this.clearInput());
        
        // Auto-focus
        if (this.autoFocus) {
            this.focus();
        }
        
        // Load recent scans if enabled
        if (this.showRecentScans) {
            this.loadRecentScans();
        }
        
        // Set up periodic focus return for barcode scanners
        this.setupFocusManagement();
        
        // Initialize collapse state
        this.initializeCollapseState();
        
        console.log(`Universal Scanner initialized for ${this.station} station`);
    }
    
    handleKeydown(event) {
        if (event.key === 'Enter') {
            event.preventDefault();
            this.processScan();
        } else if (event.key === 'Escape') {
            event.preventDefault();
            this.clearInput();
        }
    }
    
    handleInput(event) {
        const value = event.target.value.trim();
        
        // Auto-submit for certain patterns (like quick barcode scans)
        if (value.length > 8 && this.looksLikeBarcode(value)) {
            // Small delay to ensure full barcode is captured
            setTimeout(() => {
                if (this.input.value === value) {
                    this.processScan();
                }
            }, 100);
        }
    }
    
    looksLikeBarcode(value) {
        // Patterns that suggest a complete barcode scan
        return /^[A-Z0-9\-:]{8,}$/i.test(value) || 
               value.includes(':') || 
               value.includes('-');
    }
    
    async processScan() {
        const barcode = this.input.value.trim();
        
        if (!barcode) {
            this.showStatus('warning', '⚠️ Please enter a barcode');
            this.focus();
            return;
        }
        
        // Cooldown check
        const now = Date.now();
        if (now - this.lastScanTime < this.scanCooldown) {
            return;
        }
        this.lastScanTime = now;
        
        if (this.isProcessing) {
            return;
        }
        
        this.isProcessing = true;
        this.setProcessingState(true);
        
        try {
            const result = await this.submitScan(barcode);
            await this.handleScanResult(result);
        } catch (error) {
            console.error('Scan processing error:', error);
            this.showStatus('danger', '❌ Network error. Please try again.');
        } finally {
            this.isProcessing = false;
            this.setProcessingState(false);
            this.focus();
        }
    }
    
    async submitScan(barcode) {
        const sessionId = this.getSessionId();
        
        const response = await fetch('/api/scanner/process', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({
                barcode: barcode,
                station: this.station,
                sessionId: sessionId
            })
        });
        
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        
        return await response.json();
    }
    
    async handleScanResult(result) {
        if (!result) {
            this.showStatus('danger', '❌ Invalid response from server');
            return;
        }
        
        // Show status
        const statusType = result.success ? 'success' : 'danger';
        this.showStatus(statusType, result.message || 'Unknown result');
        
        // Show detailed results
        if (result.additionalData || result.suggestions) {
            this.showResults(result);
        }
        
        // Handle redirects
        if (result.success && result.redirectUrl) {
            this.showStatus('info', `🧭 Redirecting...`);
            setTimeout(() => {
                window.location.href = result.redirectUrl;
            }, 1500);
            return;
        }
        
        // Handle refresh requests
        if (result.success && result.requiresRefresh) {
            this.showStatus('info', '🔄 Refreshing page...');
            setTimeout(() => {
                window.location.reload();
            }, 1500);
            return;
        }
        
        // Clear input on success
        if (result.success && this.clearOnSuccess) {
            this.clearInput();
        }
        
        // Update recent scans
        if (this.showRecentScans) {
            this.addToRecentScans({
                barcode: this.input.value,
                result: result.message,
                success: result.success,
                timestamp: new Date()
            });
        }
    }
    
    showStatus(type, message, autoHide = true) {
        if (!this.statusDiv) return;
        
        const alertClass = `alert-${type}`;
        const alert = this.statusDiv.querySelector('.alert');
        
        alert.className = `alert ${alertClass}`;
        alert.querySelector('.status-message').textContent = message;
        
        this.statusDiv.style.display = 'block';
        
        // Update status indicator
        this.updateStatusIndicator(type, message);
        
        if (autoHide && type === 'success') {
            setTimeout(() => {
                this.statusDiv.style.display = 'none';
                this.hideStatusIndicator();
            }, 3000);
        }
    }
    
    showResults(result) {
        if (!this.resultsDiv) return;
        
        const content = this.resultsDiv.querySelector('.result-content');
        content.innerHTML = '';
        
        // Show additional data
        if (result.additionalData) {
            const dataDiv = document.createElement('div');
            dataDiv.innerHTML = `<pre class="bg-light p-2 rounded small">${JSON.stringify(result.additionalData, null, 2)}</pre>`;
            content.appendChild(dataDiv);
        }
        
        // Show suggestions
        if (result.suggestions && result.suggestions.length > 0) {
            const suggestionsDiv = document.createElement('div');
            suggestionsDiv.innerHTML = `
                <h6>💡 Suggestions:</h6>
                <ul class="list-unstyled">
                    ${result.suggestions.map(s => `<li>• ${s}</li>`).join('')}
                </ul>
            `;
            content.appendChild(suggestionsDiv);
        }
        
        this.resultsDiv.style.display = 'block';
        
        // Auto-hide after 10 seconds
        setTimeout(() => {
            this.resultsDiv.style.display = 'none';
        }, 10000);
    }
    
    addToRecentScans(scan) {
        if (!this.recentScansDiv) return;
        
        const tbody = this.recentScansDiv.querySelector('.recent-scans-body');
        if (!tbody) return;
        
        const row = document.createElement('tr');
        const statusIcon = scan.success ? '✅' : '❌';
        const timeStr = scan.timestamp.toLocaleTimeString();
        
        row.innerHTML = `
            <td class="text-muted small">${timeStr}</td>
            <td><code>${this.escapeHtml(scan.barcode)}</code></td>
            <td class="small">${statusIcon} ${this.escapeHtml(scan.result)}</td>
        `;
        
        tbody.insertBefore(row, tbody.firstChild);
        
        // Keep only last 10 scans
        while (tbody.children.length > 10) {
            tbody.removeChild(tbody.lastChild);
        }
        
        this.recentScansDiv.style.display = 'block';
    }
    
    async loadRecentScans() {
        try {
            const response = await fetch(`/api/scanner/recent-scans?station=${encodeURIComponent(this.station)}&limit=5`);
            if (response.ok) {
                const scans = await response.json();
                if (scans && scans.length > 0) {
                    scans.forEach(scan => {
                        this.addToRecentScans({
                            barcode: scan.barcode,
                            result: scan.result,
                            success: scan.success,
                            timestamp: new Date(scan.scanDate)
                        });
                    });
                }
            }
        } catch (error) {
            console.warn('Failed to load recent scans:', error);
        }
    }
    
    clearInput() {
        if (this.input) {
            this.input.value = '';
            this.focus();
        }
        
        if (this.statusDiv) {
            this.statusDiv.style.display = 'none';
        }
        
        if (this.resultsDiv) {
            this.resultsDiv.style.display = 'none';
        }
    }
    
    focus() {
        if (!this.isProcessing) {
            if (this.isCollapsed()) {
                if (this.invisibleInput) {
                    this.invisibleInput.focus();
                }
            } else if (this.input) {
                this.input.focus();
            }
        }
    }
    
    setProcessingState(processing) {
        if (this.submitButton) {
            this.submitButton.disabled = processing;
        }
        
        if (this.input) {
            this.input.disabled = processing;
        }
        
        const spinner = this.statusDiv?.querySelector('.spinner-border');
        if (spinner) {
            spinner.style.display = processing ? 'inline-block' : 'none';
        }
        
        if (processing) {
            this.showStatus('info', '🔍 Processing scan...', false);
        }
    }
    
    setupFocusManagement() {
        // Return focus to scanner input after brief delays
        // This helps with barcode scanners that may steal focus
        let focusTimeout;
        
        document.addEventListener('click', () => {
            clearTimeout(focusTimeout);
            focusTimeout = setTimeout(() => {
                if (!this.isProcessing && document.activeElement !== this.input && !this.isCollapsed()) {
                    this.focus();
                }
            }, 2000);
        });
        
        // Return focus every 5 seconds if no activity
        setInterval(() => {
            if (!this.isProcessing && document.activeElement !== this.input && !this.isCollapsed()) {
                this.focus();
            }
        }, 5000);
        
        // Set up invisible input for collapsed state
        this.setupInvisibleInput();
    }
    
    setupInvisibleInput() {
        // Create invisible input for when scanner is collapsed
        this.invisibleInput = document.createElement('input');
        this.invisibleInput.type = 'text';
        this.invisibleInput.style.position = 'fixed';
        this.invisibleInput.style.left = '-9999px';
        this.invisibleInput.style.top = '0';
        this.invisibleInput.style.opacity = '0';
        this.invisibleInput.style.zIndex = '-1';
        this.invisibleInput.autocomplete = 'off';
        this.invisibleInput.spellcheck = false;
        document.body.appendChild(this.invisibleInput);
        
        // Set up event listeners for invisible input
        this.invisibleInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && this.isCollapsed()) {
                e.preventDefault();
                this.processScanFromInvisible();
            }
        });
        
        this.invisibleInput.addEventListener('input', (e) => {
            if (this.isCollapsed()) {
                const value = e.target.value.trim();
                if (value.length > 8 && this.looksLikeBarcode(value)) {
                    setTimeout(() => {
                        if (this.invisibleInput.value === value) {
                            this.processScanFromInvisible();
                        }
                    }, 100);
                }
            }
        });
        
        // Focus invisible input when collapsed
        setInterval(() => {
            if (this.isCollapsed() && !this.isProcessing && document.activeElement !== this.invisibleInput) {
                this.invisibleInput.focus();
            }
        }, 1000);
    }
    
    async processScanFromInvisible() {
        if (!this.isCollapsed()) return;
        
        const barcode = this.invisibleInput.value.trim();
        this.invisibleInput.value = '';
        
        if (!barcode) return;
        
        this.isProcessing = true;
        this.updateStatusIndicator('processing', 'Processing scan...');
        
        try {
            const result = await this.submitScan(barcode);
            await this.handleScanResult(result);
        } catch (error) {
            console.error('Scan processing error:', error);
            this.updateStatusIndicator('error', 'Network error. Please try again.');
        } finally {
            this.isProcessing = false;
            setTimeout(() => {
                if (this.isCollapsed()) {
                    this.invisibleInput.focus();
                }
            }, 100);
        }
    }
    
    getSessionId() {
        // Get session ID from meta tag or generate one
        let sessionId = document.querySelector('meta[name="session-id"]')?.content;
        if (!sessionId) {
            sessionId = 'session-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9);
        }
        return sessionId;
    }
    
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
    
    initializeCollapseState() {
        const storageKey = `scanner-collapsed-${this.station}`;
        const isCollapsed = localStorage.getItem(storageKey) === 'true';
        
        if (isCollapsed && this.bodyElement) {
            this.bodyElement.classList.remove('show');
            if (this.toggleIcon) {
                this.toggleIcon.classList.add('collapsed');
            }
        }
        
        // Listen for bootstrap collapse events
        if (this.bodyElement) {
            this.bodyElement.addEventListener('shown.bs.collapse', () => {
                localStorage.setItem(storageKey, 'false');
                if (this.toggleIcon) {
                    this.toggleIcon.classList.remove('collapsed');
                }
            });
            
            this.bodyElement.addEventListener('hidden.bs.collapse', () => {
                localStorage.setItem(storageKey, 'true');
                if (this.toggleIcon) {
                    this.toggleIcon.classList.add('collapsed');
                }
            });
        }
    }
    
    updateStatusIndicator(type, message) {
        if (!this.statusIndicator) return;
        
        // Clear existing classes
        this.statusIndicator.classList.remove('success', 'error', 'processing');
        
        // Add appropriate class
        switch(type) {
            case 'success':
                this.statusIndicator.classList.add('success');
                break;
            case 'danger':
                this.statusIndicator.classList.add('error');
                break;
            case 'info':
                this.statusIndicator.classList.add('processing');
                break;
            default:
                this.statusIndicator.classList.add('processing');
        }
        
        this.statusIndicator.style.display = 'block';
        this.statusIndicator.title = message;
    }
    
    hideStatusIndicator() {
        if (this.statusIndicator) {
            this.statusIndicator.style.display = 'none';
            this.statusIndicator.classList.remove('success', 'error', 'processing');
        }
    }
    
    isCollapsed() {
        return this.bodyElement && !this.bodyElement.classList.contains('show');
    }
    
    // Public API methods
    setValue(value) {
        if (this.input) {
            this.input.value = value;
        }
    }
    
    getValue() {
        return this.input ? this.input.value : '';
    }
    
    enable() {
        if (this.input) {
            this.input.disabled = false;
        }
        if (this.submitButton) {
            this.submitButton.disabled = false;
        }
    }
    
    disable() {
        if (this.input) {
            this.input.disabled = true;
        }
        if (this.submitButton) {
            this.submitButton.disabled = true;
        }
    }
}

// Global scanner instances registry
window.universalScanners = window.universalScanners || {};

// Utility function to create scanner instances
window.createUniversalScanner = function(containerId, options = {}) {
    const scanner = new UniversalScanner(containerId, options);
    window.universalScanners[containerId] = scanner;
    return scanner;
};

// Auto-initialize scanners on page load
document.addEventListener('DOMContentLoaded', function() {
    // Find all scanner containers and auto-initialize
    const scannerInputs = document.querySelectorAll('.universal-scanner-input');
    
    scannerInputs.forEach(input => {
        const containerId = input.dataset.container;
        const station = input.dataset.station;
        
        if (containerId && station && !window.universalScanners[containerId]) {
            window.createUniversalScanner(containerId, {
                station: station,
                autoFocus: true,
                clearOnSuccess: true,
                showRecentScans: true
            });
        }
    });
});

// Global toggle function for scanner collapse
window.toggleScannerCollapse = function(containerId) {
    const toggleIcon = document.getElementById(`scanner-toggle-${containerId}`);
    const bodyElement = document.getElementById(`scanner-body-${containerId}`);
    
    if (toggleIcon && bodyElement) {
        // Toggle will be handled by Bootstrap, we just need to update the icon
        setTimeout(() => {
            if (bodyElement.classList.contains('show')) {
                toggleIcon.classList.remove('collapsed');
            } else {
                toggleIcon.classList.add('collapsed');
            }
        }, 350); // Bootstrap collapse animation duration
    }
};

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = UniversalScanner;
}