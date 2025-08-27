// SmartSheet OAuth Status Management
// Handles authentication status, token refresh, and UI updates

class SmartSheetAuth {
    constructor() {
        this.checkInterval = null;
        this.isChecking = false;
        this.init();
    }

    init() {
        // Check status immediately on page load
        this.checkAuthStatus();
        
        // Set up periodic status checking (every 5 minutes)
        this.checkInterval = setInterval(() => {
            this.checkAuthStatus();
        }, 5 * 60 * 1000); // 5 minutes
        
        // Set up click handlers
        this.setupEventHandlers();
        
        console.log('SmartSheet Auth manager initialized');
    }

    setupEventHandlers() {
        // Handle auth action button clicks
        const actionButton = document.getElementById('smartsheetActionButton');
        if (actionButton) {
            actionButton.addEventListener('click', (e) => {
                e.preventDefault();
                this.handleAuthAction();
            });
        }
    }

    async checkAuthStatus() {
        if (this.isChecking) return; // Prevent multiple simultaneous checks
        this.isChecking = true;
        
        try {
            const response = await fetch('/smartsheet/auth/status');
            const data = await response.json();
            
            this.updateUI(data);
        } catch (error) {
            console.error('Error checking SmartSheet auth status:', error);
            this.updateUI({
                isAuthenticated: false,
                userEmail: null,
                error: 'Connection error'
            });
        } finally {
            this.isChecking = false;
        }
    }

    updateUI(authData) {
        const statusBadge = document.getElementById('smartsheetStatusBadge');
        const statusText = document.getElementById('smartsheetStatusText');
        const detailStatus = document.getElementById('smartsheetDetailStatus');
        const userInfo = document.getElementById('smartsheetUserInfo');
        const userEmail = document.getElementById('smartsheetUserEmail');
        const actionButton = document.getElementById('smartsheetActionButton');
        const icon = document.getElementById('smartsheetIcon');

        if (authData.isAuthenticated) {
            // User is authenticated
            statusBadge.textContent = 'Connected';
            statusBadge.className = 'badge ms-2 bg-success';
            detailStatus.textContent = 'Connected';
            
            if (authData.userEmail) {
                userInfo.style.display = 'block';
                userEmail.textContent = authData.userEmail;
            }
            
            actionButton.innerHTML = '<i class="fas fa-sign-out-alt me-2"></i>Disconnect';
            actionButton.title = 'Disconnect from SmartSheet';
            icon.className = 'fas fa-table text-success me-2';
            
        } else {
            // User is not authenticated or token expired
            statusBadge.textContent = authData.isExpired ? 'Expired' : 'Not Connected';
            statusBadge.className = authData.isExpired ? 'badge ms-2 bg-warning' : 'badge ms-2 bg-secondary';
            detailStatus.textContent = authData.error || (authData.isExpired ? 'Token expired' : 'Not connected');
            
            userInfo.style.display = 'none';
            userEmail.textContent = '';
            
            actionButton.innerHTML = '<i class="fas fa-sign-in-alt me-2"></i>Connect to SmartSheet';
            actionButton.title = 'Connect to SmartSheet';
            icon.className = 'fas fa-table text-muted me-2';
        }
    }

    async handleAuthAction() {
        const statusBadge = document.getElementById('smartsheetStatusBadge');
        
        if (statusBadge.textContent === 'Connected') {
            // User is connected - handle logout
            await this.logout();
        } else {
            // User is not connected - start OAuth flow
            this.startOAuthFlow();
        }
    }

    async logout() {
        try {
            const response = await fetch('/smartsheet/auth/logout', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });
            
            const data = await response.json();
            
            if (data.success) {
                this.showNotification('Disconnected from SmartSheet', 'info');
                // Update UI immediately
                this.updateUI({
                    isAuthenticated: false,
                    userEmail: null
                });
            } else {
                this.showNotification('Error disconnecting: ' + data.message, 'error');
            }
        } catch (error) {
            console.error('Error during logout:', error);
            this.showNotification('Error disconnecting from SmartSheet', 'error');
        }
    }

    startOAuthFlow() {
        // Open OAuth flow in a popup window
        const popup = window.open(
            '/smartsheet/auth/login',
            'smartsheet_auth',
            'width=600,height=600,scrollbars=yes,resizable=yes'
        );

        // Monitor the popup for completion
        const checkClosed = setInterval(() => {
            if (popup.closed) {
                clearInterval(checkClosed);
                // Check auth status after popup closes
                setTimeout(() => {
                    this.checkAuthStatus();
                }, 1000);
            }
        }, 1000);

        // Handle popup blocking
        if (!popup || popup.closed) {
            // Popup was blocked - redirect in same window
            this.showNotification('Popup blocked. Redirecting to SmartSheet...', 'info');
            window.location.href = '/smartsheet/auth/login';
        }
    }

    async refreshToken() {
        try {
            const response = await fetch('/smartsheet/auth/refresh', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });
            
            const data = await response.json();
            
            if (data.success) {
                this.showNotification('SmartSheet connection refreshed', 'success');
                this.checkAuthStatus(); // Update UI
                return true;
            } else {
                this.showNotification('Please reconnect to SmartSheet', 'warning');
                // Update UI to show disconnected state
                this.updateUI({
                    isAuthenticated: false,
                    userEmail: null
                });
                return false;
            }
        } catch (error) {
            console.error('Error refreshing token:', error);
            return false;
        }
    }

    showNotification(message, type = 'info') {
        // Use the global notification system if available
        if (typeof showNotification === 'function') {
            showNotification(message, type);
        } else {
            // Fallback to console log
            console.log(`${type.toUpperCase()}: ${message}`);
        }
    }

    destroy() {
        if (this.checkInterval) {
            clearInterval(this.checkInterval);
            this.checkInterval = null;
        }
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    window.smartSheetAuth = new SmartSheetAuth();
});

// Clean up on page unload
window.addEventListener('beforeunload', function() {
    if (window.smartSheetAuth) {
        window.smartSheetAuth.destroy();
    }
});

// Global functions for compatibility
function checkSmartSheetAuth() {
    if (window.smartSheetAuth) {
        window.smartSheetAuth.checkAuthStatus();
    }
}

function refreshSmartSheetToken() {
    if (window.smartSheetAuth) {
        return window.smartSheetAuth.refreshToken();
    }
    return Promise.resolve(false);
}