// ShopBoss User Preferences Manager
// Centralized localStorage-based preferences system

const ShopBossPreferences = {
    // Preference key constants
    Keys: {
        SORTING_SELECTED_RACK: 'shopboss.sorting.selectedRackId',
        CNC_SHOW_PROCESSED: 'shopboss.cnc.showProcessed',
        CNC_GROUP_BY_MATERIAL: 'shopboss.cnc.groupByMaterial',
        CNC_AUTO_PRINT_LABELS: 'shopboss.cnc.autoPrintLabels',
        ASSEMBLY_SHOW_BILLBOARD: 'shopboss.assembly.showBillboard',
        LAST_ACTIVE_WORK_ORDER: 'shopboss.global.lastActiveWorkOrder'
    },

    // Get a preference value with fallback to default
    get: function(key, defaultValue = null) {
        try {
            const value = localStorage.getItem(key);
            if (value === null) {
                return defaultValue;
            }
            
            // Try to parse as JSON for complex values
            try {
                return JSON.parse(value);
            } catch {
                // Return as string if not valid JSON
                return value;
            }
        } catch (error) {
            console.warn('LocalStorage not available, using default value for', key, error);
            return defaultValue;
        }
    },

    // Set a preference value
    set: function(key, value) {
        try {
            const serializedValue = typeof value === 'string' ? value : JSON.stringify(value);
            localStorage.setItem(key, serializedValue);
            return true;
        } catch (error) {
            console.warn('Failed to save preference', key, error);
            return false;
        }
    },

    // Remove a preference
    remove: function(key) {
        try {
            localStorage.removeItem(key);
            return true;
        } catch (error) {
            console.warn('Failed to remove preference', key, error);
            return false;
        }
    },

    // Clear all ShopBoss preferences
    clearAll: function() {
        try {
            const keys = Object.keys(localStorage);
            const shopBossKeys = keys.filter(key => key.startsWith('shopboss.'));
            shopBossKeys.forEach(key => localStorage.removeItem(key));
            return true;
        } catch (error) {
            console.warn('Failed to clear preferences', error);
            return false;
        }
    },

    // Station-specific convenience methods
    Sorting: {
        getSelectedRack: function() {
            return ShopBossPreferences.get(ShopBossPreferences.Keys.SORTING_SELECTED_RACK);
        },
        setSelectedRack: function(rackId) {
            return ShopBossPreferences.set(ShopBossPreferences.Keys.SORTING_SELECTED_RACK, rackId);
        }
    },

    CNC: {
        getShowProcessed: function() {
            return ShopBossPreferences.get(ShopBossPreferences.Keys.CNC_SHOW_PROCESSED, true); // Default to true
        },
        setShowProcessed: function(show) {
            return ShopBossPreferences.set(ShopBossPreferences.Keys.CNC_SHOW_PROCESSED, show);
        },
        getGroupByMaterial: function() {
            return ShopBossPreferences.get(ShopBossPreferences.Keys.CNC_GROUP_BY_MATERIAL, false); // Default to false
        },
        setGroupByMaterial: function(group) {
            return ShopBossPreferences.set(ShopBossPreferences.Keys.CNC_GROUP_BY_MATERIAL, group);
        },
        getAutoPrintLabels: function() {
            return ShopBossPreferences.get(ShopBossPreferences.Keys.CNC_AUTO_PRINT_LABELS, true); // Default to true
        },
        setAutoPrintLabels: function(enabled) {
            return ShopBossPreferences.set(ShopBossPreferences.Keys.CNC_AUTO_PRINT_LABELS, enabled);
        }
    },
    
    Assembly: {
        getShowBillboard: function() {
            return ShopBossPreferences.get(ShopBossPreferences.Keys.ASSEMBLY_SHOW_BILLBOARD, true); // Default to true
        },
        setShowBillboard: function(show) {
            return ShopBossPreferences.set(ShopBossPreferences.Keys.ASSEMBLY_SHOW_BILLBOARD, show);
        }
    },

    Global: {
        getLastActiveWorkOrder: function() {
            return ShopBossPreferences.get(ShopBossPreferences.Keys.LAST_ACTIVE_WORK_ORDER);
        },
        setLastActiveWorkOrder: function(workOrderData) {
            return ShopBossPreferences.set(ShopBossPreferences.Keys.LAST_ACTIVE_WORK_ORDER, workOrderData);
        },
        clearLastActiveWorkOrder: function() {
            return ShopBossPreferences.remove(ShopBossPreferences.Keys.LAST_ACTIVE_WORK_ORDER);
        }
    }
};

// Make available globally
window.ShopBossPreferences = ShopBossPreferences;