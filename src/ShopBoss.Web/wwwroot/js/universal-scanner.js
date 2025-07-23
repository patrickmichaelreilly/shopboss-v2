// Universal Scanner JavaScript Module - Pure Input Component
class UniversalScanner {
    constructor(containerId, options = {}) {
        this.containerId = containerId;
        this.clearOnSuccess = options.clearOnSuccess !== false;
        this.showRecentScans = options.showRecentScans !== false;
        
        this.input = document.getElementById(`scanner-input-${containerId}`);
        this.submitButton = document.getElementById(`scanner-submit-${containerId}`);
        this.clearButton = document.getElementById(`scanner-clear-${containerId}`);
        this.statusDiv = document.getElementById(`scanner-status-${containerId}`);
        this.resultsDiv = document.getElementById(`scanner-results-${containerId}`);
        this.recentScansDiv = document.getElementById(`recent-scans-${containerId}`);
        
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
        
        
        // Recent scans will be populated in real-time as scans happen
        
        // Set up document-level key listener for barcode scanning
        this.setupDocumentKeyListener();
        
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
        
        
        if (autoHide && type === 'success') {
            setTimeout(() => {
                this.statusDiv.style.display = 'none';
            }, 3000);
        }
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
        }
        
        if (this.statusDiv) {
            this.statusDiv.style.display = 'none';
        }
        
        if (this.resultsDiv) {
            this.resultsDiv.style.display = 'none';
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
    
    setupDocumentKeyListener() {
        // Initialize barcode accumulation
        this.barcodeBuffer = '';
        this.barcodeTimeout = null;
        
        // Simplest possible document-level keydown listener - no conditionals
        this.documentKeyHandler = (e) => {
            // Handle Enter key - process accumulated barcode
            if (e.key === 'Enter') {
                if (this.barcodeBuffer.trim()) {
                    const barcode = this.barcodeBuffer.trim();
                    this.barcodeBuffer = '';
                    this.emitScanEvent(barcode);
                }
                return;
            }
            
            // Accumulate printable characters
            if (e.key.length === 1) {
                this.barcodeBuffer += e.key;
                
                // Reset timeout on each keystroke
                clearTimeout(this.barcodeTimeout);
                this.barcodeTimeout = setTimeout(() => {
                    this.barcodeBuffer = '';
                }, 2000);
            }
        };
        
        // Add the document listener
        document.addEventListener('keydown', this.documentKeyHandler);
        
        // Clean up on page unload
        window.addEventListener('beforeunload', () => {
            document.removeEventListener('keydown', this.documentKeyHandler);
            clearTimeout(this.barcodeTimeout);
        });
    }
    
    // Session ID handling removed - no longer needed for pure input component
    
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
    
    // Health indicator management for compact mode - static green dot
    updateHealthIndicator(status = 'ready', message = '') {
        const healthIndicator = document.getElementById(`scanner-health-${this.containerId}`);
        if (!healthIndicator) return;
        
        // Always show as ready (static green dot)
        healthIndicator.classList.remove('ready', 'not-ready', 'processing');
        healthIndicator.classList.add('ready');
        healthIndicator.title = 'Scanner Ready';
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
                clearOnSuccess: true,
                showRecentScans: true
            });
        }
    });
    
    console.log('Universal scanners initialized:', Object.keys(window.universalScanners));
});

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = UniversalScanner;
}