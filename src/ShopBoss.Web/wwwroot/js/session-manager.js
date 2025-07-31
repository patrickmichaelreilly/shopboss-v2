// ShopBoss Session Management
// Handles session restoration and work order persistence

const ShopBossSession = {
    // Check if session has active work order, attempt restore from localStorage if not
    async validateAndRestoreSession() {
        try {
            // First check if session already has active work order
            const sessionCheck = await fetch('/Admin/GetActiveWorkOrder');
            const sessionData = await sessionCheck.json();
            
            if (sessionData.success) {
                // Session is valid, save to localStorage for future recovery
                const workOrderData = {
                    id: sessionData.activeWorkOrderId,
                    name: sessionData.activeWorkOrderName,
                    savedAt: new Date().toISOString()
                };
                ShopBossPreferences.Global.setLastActiveWorkOrder(workOrderData);
                return { success: true, restored: false, workOrder: workOrderData };
            }
            
            // Session is empty, try to restore from localStorage
            const lastWorkOrder = ShopBossPreferences.Global.getLastActiveWorkOrder();
            if (!lastWorkOrder || !lastWorkOrder.id) {
                return { success: false, message: "No active work order found in session or localStorage" };
            }
            
            // Check if saved work order is recent (within 7 days)
            const savedDate = new Date(lastWorkOrder.savedAt);
            const daysSinceLastSave = (new Date() - savedDate) / (1000 * 60 * 60 * 24);
            if (daysSinceLastSave > 7) {
                ShopBossPreferences.Global.clearLastActiveWorkOrder();
                return { success: false, message: "Saved work order is too old, cleared from localStorage" };
            }
            
            // Attempt to restore the session
            const restoreResponse = await fetch('/Admin/RestoreActiveWorkOrder', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: `workOrderId=${encodeURIComponent(lastWorkOrder.id)}`
            });
            
            const restoreData = await restoreResponse.json();
            if (restoreData.success) {
                // Update localStorage with fresh data
                const updatedWorkOrderData = {
                    id: restoreData.activeWorkOrderId,
                    name: restoreData.activeWorkOrderName,
                    savedAt: new Date().toISOString()
                };
                ShopBossPreferences.Global.setLastActiveWorkOrder(updatedWorkOrderData);
                
                return { 
                    success: true, 
                    restored: true, 
                    workOrder: updatedWorkOrderData,
                    message: `Session restored: ${restoreData.activeWorkOrderName}`
                };
            } else {
                // Failed to restore, clear localStorage
                ShopBossPreferences.Global.clearLastActiveWorkOrder();
                return { success: false, message: restoreData.message };
            }
            
        } catch (error) {
            console.warn('Session validation/restoration failed:', error);
            return { success: false, message: "Session check failed" };
        }
    },

    // Update localStorage when work order changes
    saveActiveWorkOrder(workOrderId, workOrderName) {
        const workOrderData = {
            id: workOrderId,
            name: workOrderName,
            savedAt: new Date().toISOString()
        };
        ShopBossPreferences.Global.setLastActiveWorkOrder(workOrderData);
    },

    // Clear localStorage when work order is explicitly cleared
    clearActiveWorkOrder() {
        ShopBossPreferences.Global.clearLastActiveWorkOrder();
    },

    // Show restoration message to user (stations can customize this)
    showRestorationMessage(message, type = 'info') {
        // Try to use station-specific billboard if available
        if (typeof showBillboard === 'function') {
            showBillboard('main-billboard', message, type, 'Session Restored');
        } else {
            // Fallback to console for stations without billboard
            console.log(`Session: ${message}`);
        }
    }
};

// Auto-restore session on page load for station pages
document.addEventListener('DOMContentLoaded', async function() {
    // Only run on station pages (not admin pages)
    const isStationPage = document.body.classList.contains('station-page') || 
                         window.location.pathname.includes('/Sorting') ||
                         window.location.pathname.includes('/Assembly') ||
                         window.location.pathname.includes('/Cnc') ||
                         window.location.pathname.includes('/Shipping');
    
    if (isStationPage) {
        const result = await ShopBossSession.validateAndRestoreSession();
        
        if (result.success && result.restored) {
            // Show user-friendly restoration message
            ShopBossSession.showRestorationMessage(
                `Work order "${result.workOrder.name}" restored from previous session`,
                'success'
            );
            
            // Refresh page if needed to update UI with restored session
            setTimeout(() => {
                window.location.reload();
            }, 2000);
        }
    }
});

// Make available globally
window.ShopBossSession = ShopBossSession;