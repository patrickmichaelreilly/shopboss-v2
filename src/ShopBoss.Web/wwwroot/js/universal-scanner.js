// Universal Scanner JavaScript Module - Pure Input Component
class UniversalScanner {
    constructor(containerId, options = {}) {
        this.containerId = containerId;
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
        this.scanCooldown = 100; // 1 second cooldown between scans
        
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
        
        // Recent scans will be populated in real-time as scans happen
        
        // Set up periodic focus return for barcode scanners
        this.setupFocusManagement();
        
        // Initialize collapse state
        this.initializeCollapseState();
        
        // Initialize health indicator
        this.updateHealthIndicator('ready', 'Scanner Ready');
        
        console.log(`Universal Scanner initialized for container: ${this.containerId}`);
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
        this.updateHealthIndicator('processing', 'Processing scan...');
        
        try {
            // Track last scanned barcode
            this.lastScannedBarcode = barcode;
            
            // Check if this is a command barcode
            if (this.isCommandBarcode(barcode)) {
                this.handleCommand(barcode);
                return;
            }
            
            // Emit scan event for non-command barcodes
            this.emitScanEvent(barcode);
            
            // Show basic feedback
            this.showStatus('info', `📡 Scan received: ${barcode}`, false);
            
            // Clear input if configured
            if (this.clearOnSuccess) {
                this.clearInput();
            }
            
            // Add to recent scans
            if (this.showRecentScans) {
                this.addToRecentScans({
                    barcode: barcode,
                    result: 'Forwarded to page handler',
                    success: true,
                    timestamp: new Date()
                });
            }
            
        } catch (error) {
            console.error('Scan processing error:', error);
            this.showStatus('danger', '❌ Error processing scan.');
        } finally {
            this.isProcessing = false;
            this.setProcessingState(false);
            this.updateHealthIndicator('ready', 'Scanner Ready');
            this.focus();
        }
    }
    
    // Command barcode detection and handling
    isCommandBarcode(barcode) {
        return barcode.toUpperCase().startsWith('NAV-');
    }
    
    handleCommand(barcode) {
        const command = barcode.toUpperCase();
        
        // Navigation commands
        if (command.startsWith('NAV-')) {
            this.handleNavigationCommand(command);
            return;
        }
        
        // Unknown command
        this.showStatus('warning', `❓ Unknown command: ${barcode}`);
        this.addToRecentScans({
            barcode: barcode,
            result: 'Unknown command',
            success: false,
            timestamp: new Date()
        });
    }
    
    handleNavigationCommand(command) {
        const destination = command.replace('NAV-', '');
        let url = null;
        let stationName = '';
        
        switch (destination) {
            case 'ADMIN':
                url = '/Admin';
                stationName = 'Admin Station';
                break;
            case 'CNC':
                url = '/Cnc';
                stationName = 'CNC Station';
                break;
            case 'SORTING':
                url = '/Sorting';
                stationName = 'Sorting Station';
                break;
            case 'ASSEMBLY':
                url = '/Assembly';
                stationName = 'Assembly Station';
                break;
            case 'SHIPPING':
                url = '/Shipping';
                stationName = 'Shipping Station';
                break;
            default:
                this.showStatus('warning', `❓ Unknown navigation destination: ${destination}`);
                this.addToRecentScans({
                    barcode: command,
                    result: 'Unknown navigation destination',
                    success: false,
                    timestamp: new Date()
                });
                return;
        }
        
        // Show navigation feedback
        this.showStatus('success', `🧭 Navigating to ${stationName}...`, false);
        this.addToRecentScans({
            barcode: command,
            result: `Navigating to ${stationName}`,
            success: true,
            timestamp: new Date()
        });
        
        // Navigate after a brief delay for user feedback
        setTimeout(() => {
            window.location.href = url;
        }, 1500);
    }
    
    emitScanEvent(barcode) {
        // Create and dispatch a custom event that pages can listen to
        const scanEvent = new CustomEvent('scanReceived', {
            detail: {
                barcode: barcode,
                timestamp: new Date(),
                containerId: this.containerId,
                scanner: this
            },
            bubbles: true
        });
        
        // Dispatch on document for global listeners
        document.dispatchEvent(scanEvent);
        
        console.log('Universal Scanner: Emitted scanReceived event', { barcode, containerId: this.containerId });
    }
    
    // Public method for pages to show scan results
    showScanResult(success, message, autoHide = true) {
        const statusType = success ? 'success' : 'danger';
        this.showStatus(statusType, message, autoHide);
        
        // Don't add to recent scans here - already added in processScan()
        // This prevents duplicate entries when pages call showScanResult()
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
    
    // Recent scans are now populated in real-time by the scanner itself
    // No API calls needed - each page can persist its own recent scans if needed
    
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
        console.log(`[Scanner-${this.containerId}] focus() called:`, {
            isProcessing: this.isProcessing,
            currentActiveElement: document.activeElement?.tagName + (document.activeElement?.id ? `#${document.activeElement.id}` : ''),
            hasModalInput: !!this.input,
            isCollapsed: this.isCollapsed()
        });
        
        if (!this.isProcessing) {
            if (this.isCollapsed()) {
                console.log(`[Scanner-${this.containerId}] Scanner collapsed - using document-level listener, no focus needed`);
            } else if (this.input) {
                console.log(`[Scanner-${this.containerId}] Attempting to focus modal input`);
                // Check if input is in a modal that's currently shown
                const modal = this.input.closest('.modal');
                if (modal && modal.classList.contains('show')) {
                    this.input.focus();
                    console.log(`[Scanner-${this.containerId}] Modal input focused. New active element:`, document.activeElement?.tagName + (document.activeElement?.id ? `#${document.activeElement.id}` : ''));
                } else if (!modal) {
                    // Not in a modal, focus normally
                    this.input.focus();
                    console.log(`[Scanner-${this.containerId}] Non-modal input focused. New active element:`, document.activeElement?.tagName + (document.activeElement?.id ? `#${document.activeElement.id}` : ''));
                } else {
                    console.log(`[Scanner-${this.containerId}] Modal not shown, skipping focus`);
                }
            }
        } else {
            console.log(`[Scanner-${this.containerId}] Skipping focus - scanner is processing`);
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
                if (!this.isProcessing && !this.isCollapsed()) {
                    // When not collapsed (modal open), focus modal input
                    if (document.activeElement !== this.input) {
                        const modal = this.input ? this.input.closest('.modal') : null;
                        if (!modal || (modal && modal.classList.contains('show'))) {
                            this.focus();
                        }
                    }
                }
            }, 2000);
        });
        
        // Set up document-level key listener for collapsed state
        this.setupDocumentKeyListener();
    }
    
    setupDocumentKeyListener() {
        // Initialize barcode accumulation
        this.barcodeBuffer = '';
        this.barcodeTimeout = null;
        
        // Document-level keydown listener for collapsed state
        this.documentKeyHandler = (e) => {
            // Only handle when scanner is collapsed and not processing
            if (!this.isCollapsed() || this.isProcessing) {
                return;
            }
            
            // Handle Enter key - process accumulated barcode
            if (e.key === 'Enter') {
                e.preventDefault();
                if (this.barcodeBuffer.trim()) {
                    this.processScanFromDocument();
                }
                return;
            }
            
            // Skip special keys, only accumulate printable characters
            if (e.key.length === 1) {
                this.barcodeBuffer += e.key;
                
                // Reset timeout on each keystroke
                clearTimeout(this.barcodeTimeout);
                this.barcodeTimeout = setTimeout(() => {
                    // Clear buffer after 2 seconds of inactivity
                    this.barcodeBuffer = '';
                }, 2000);
            }
        };
        
        // Add the document listener
        document.addEventListener('keydown', this.documentKeyHandler);
        
        // Clean up on page unload
        window.addEventListener('beforeunload', () => {
            if (this.documentKeyHandler) {
                document.removeEventListener('keydown', this.documentKeyHandler);
            }
            if (this.barcodeTimeout) {
                clearTimeout(this.barcodeTimeout);
            }
        });
    }
    
    async processScanFromDocument() {
        if (!this.isCollapsed()) return;
        
        const barcode = this.barcodeBuffer.trim();
        this.barcodeBuffer = '';
        clearTimeout(this.barcodeTimeout);
        
        if (!barcode) return;
        
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
        this.updateStatusIndicator('processing', 'Processing scan...');
        
        try {
            // Track last scanned barcode
            this.lastScannedBarcode = barcode;
            
            // Check if this is a command barcode
            if (this.isCommandBarcode(barcode)) {
                this.handleCommand(barcode);
                return;
            }
            
            // Emit scan event for non-command barcodes
            this.emitScanEvent(barcode);
            
            // Add to recent scans
            if (this.showRecentScans) {
                this.addToRecentScans({
                    barcode: barcode,
                    result: 'Forwarded to page handler',
                    success: true,
                    timestamp: new Date()
                });
            }
            
            // Show basic feedback via status indicator
            this.updateStatusIndicator('info', `Scan received: ${barcode}`);
            
        } catch (error) {
            console.error('Scan processing error:', error);
            this.updateStatusIndicator('error', 'Error processing scan.');
        } finally {
            this.isProcessing = false;
            // No focus management needed for document-level listener
        }
    }
    
    // Session ID handling removed - no longer needed for pure input component
    
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
    
    initializeCollapseState() {
        // Always start collapsed - no user preference storage
        if (this.bodyElement) {
            this.bodyElement.classList.remove('show');
            if (this.toggleIcon) {
                this.toggleIcon.classList.add('collapsed');
            }
        }
        
        // Listen for bootstrap collapse events for visual updates only
        if (this.bodyElement) {
            this.bodyElement.addEventListener('shown.bs.collapse', () => {
                if (this.toggleIcon) {
                    this.toggleIcon.classList.remove('collapsed');
                }
            });
            
            this.bodyElement.addEventListener('hidden.bs.collapse', () => {
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
        // Universal Scanner is modal-based, "collapsed" means modal is not shown
        const modal = this.input ? this.input.closest('.modal') : null;
        const isCollapsed = modal ? !modal.classList.contains('show') : true;
        
        // Reduce debugging frequency - only log occasionally
        if (Math.random() < 0.01) { // Only log 1% of the time
            console.log(`[Scanner-${this.containerId}] isCollapsed() called:`, {
                hasModal: !!modal,
                modalClasses: modal ? Array.from(modal.classList) : null,
                hasShowClass: modal ? modal.classList.contains('show') : false,
                isCollapsed: isCollapsed
            });
        }
        
        return isCollapsed;
    }
    
    // Health indicator management for compact mode
    updateHealthIndicator(status = 'ready', message = '') {
        const healthIndicator = document.getElementById(`scanner-health-${this.containerId}`);
        if (!healthIndicator) return;
        
        // Clear existing classes
        healthIndicator.classList.remove('ready', 'not-ready', 'processing');
        
        // Update based on status
        switch(status) {
            case 'ready':
                healthIndicator.classList.add('ready');
                healthIndicator.title = message || 'Scanner Ready';
                break;
            case 'not-ready':
                healthIndicator.classList.add('not-ready');
                healthIndicator.title = message || 'Scanner Not Ready';
                break;
            case 'processing':
                healthIndicator.classList.add('processing');
                healthIndicator.title = message || 'Processing Scan...';
                break;
        }
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
        
        if (containerId && !window.universalScanners[containerId]) {
            window.createUniversalScanner(containerId, {
                autoFocus: true,
                clearOnSuccess: true,
                showRecentScans: true
            });
        }
    });
    
    console.log('Universal scanners initialized:', Object.keys(window.universalScanners));
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