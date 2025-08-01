// CNC Station JavaScript

// Toggle functionality for nest sheet display
function toggleCutSheets() {
    const showProcessed = document.getElementById('showCutToggle').checked;
    const processedCards = document.querySelectorAll('.nest-sheet-card[data-status="cut"]');
    const pendingCards = document.querySelectorAll('.nest-sheet-card[data-status="pending"]');
    const noPendingMessage = document.getElementById('noPendingSheetsMessage');
    
    processedCards.forEach(card => {
        card.style.display = showProcessed ? 'block' : 'none';
    });
    
    // Show message if no pending sheets are visible and processed sheets are hidden
    if (noPendingMessage) {
        const hasPendingCards = pendingCards.length > 0;
        const shouldShowMessage = !showProcessed && !hasPendingCards && processedCards.length > 0;
        noPendingMessage.style.display = shouldShowMessage ? 'block' : 'none';
    }
    
    // Save preference to localStorage
    ShopBossPreferences.CNC.setShowProcessed(showProcessed);
}

function toggleMaterialGrouping() {
    const groupByMaterial = document.getElementById('groupByMaterialToggle').checked;
    const container = document.getElementById('nestSheetsContainer');
    const cards = Array.from(document.querySelectorAll('.nest-sheet-card'));
    
    if (groupByMaterial) {
        // Group cards by material
        const materialGroups = {};
        
        cards.forEach(card => {
            const material = card.dataset.material;
            const materialDisplay = card.dataset.materialDisplay;
            if (!materialGroups[material]) {
                materialGroups[material] = {
                    display: materialDisplay,
                    cards: []
                };
            }
            materialGroups[material].cards.push(card);
        });
        
        // Clear container and rebuild with groups
        container.innerHTML = '';
        
        Object.keys(materialGroups).sort().forEach(material => {
            const group = materialGroups[material];
            
            // Create material header
            const materialHeader = document.createElement('div');
            materialHeader.className = 'col-12 mb-2';
            materialHeader.innerHTML = `
                <h5 class="text-primary border-bottom pb-2 mb-3">
                    <i class="fas fa-layer-group me-2"></i>${group.display}
                    <span class="badge bg-primary ms-2">${group.cards.length}</span>
                </h5>
            `;
            
            // Create row for this material group
            const materialRow = document.createElement('div');
            materialRow.className = 'row';
            
            container.appendChild(materialHeader);
            container.appendChild(materialRow);
            
            // Add cards to this group
            group.cards.forEach(card => {
                materialRow.appendChild(card);
            });
        });
    } else {
        // Return to single row layout
        container.innerHTML = '<div class="row" id="nestSheetsRow"></div>';
        const row = document.getElementById('nestSheetsRow');
        cards.forEach(card => {
            row.appendChild(card);
        });
    }
    
    // Reapply processed sheet visibility after regrouping
    toggleCutSheets();
    
    // Save preference to localStorage
    ShopBossPreferences.CNC.setGroupByMaterial(groupByMaterial);
}

// Process nest sheet by barcode
function processNestSheet(barcode, sheetName) {
    if (!confirm(`Are you sure you want to process nest sheet "${sheetName}"?\n\nThis will mark all associated parts as "Cut" and cannot be undone.`)) {
        return;
    }

    fetch('/Cnc/ProcessNestSheet', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: `barcode=${encodeURIComponent(barcode)}`
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            location.reload(); // Refresh to show updated status
        } else {
            alert('Error: ' + data.message);
        }
    })
    .catch(error => {
        console.error('Error processing nest sheet:', error);
        alert('An error occurred while processing the nest sheet.');
    });
}

// View nest sheet details
function viewNestSheetDetails(nestSheetId) {
    fetch(`/Cnc/GetNestSheetDetails/${nestSheetId}`)
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const nestSheet = data.nestSheet;
                const content = `
                    <div class="row mb-4">
                        <div class="col-md-6">
                            <h6>General Information</h6>
                            <table class="table table-sm">
                                <tr><td><strong>Name:</strong></td><td>${nestSheet.name}</td></tr>
                                <tr><td><strong>Barcode:</strong></td><td><code>${nestSheet.barcode}</code></td></tr>
                                <tr><td><strong>Material:</strong></td><td>${nestSheet.material || 'Not specified'}</td></tr>
                                <tr><td><strong>Status:</strong></td><td><span class="badge ${nestSheet.isProcessed ? 'bg-success' : 'bg-warning'}">${nestSheet.isProcessed ? 'Completed' : 'Pending'}</span></td></tr>
                            </table>
                        </div>
                        <div class="col-md-6">
                            <h6>Dimensions & Progress</h6>
                            <table class="table table-sm">
                                ${nestSheet.length ? `<tr><td><strong>Length:</strong></td><td>${nestSheet.length} mm</td></tr>` : ''}
                                ${nestSheet.width ? `<tr><td><strong>Width:</strong></td><td>${nestSheet.width} mm</td></tr>` : ''}
                                ${nestSheet.thickness ? `<tr><td><strong>Thickness:</strong></td><td>${nestSheet.thickness} mm</td></tr>` : ''}
                                <tr><td><strong>Total Parts:</strong></td><td>${nestSheet.partCount}</td></tr>
                                <tr><td><strong>Cut Parts:</strong></td><td>${nestSheet.cutPartCount}</td></tr>
                                <tr><td><strong>Created:</strong></td><td>${nestSheet.createdDate}</td></tr>
                                ${nestSheet.processedDate ? `<tr><td><strong>Processed:</strong></td><td>${nestSheet.processedDate}</td></tr>` : ''}
                            </table>
                        </div>
                    </div>
                    <h6>Parts (${nestSheet.partCount})</h6>
                    <div class="table-responsive">
                        <table class="table table-striped table-sm">
                            <thead>
                                <tr>
                                    <th>Part Name</th>
                                    <th>Product</th>
                                    <th>Qty</th>
                                    <th>Status</th>
                                    <th>Material</th>
                                    <th>Dimensions</th>
                                </tr>
                            </thead>
                            <tbody>
                                ${nestSheet.parts.map(part => `
                                    <tr>
                                        <td>${part.name}</td>
                                        <td>${part.productName}</td>
                                        <td>${part.qty}</td>
                                        <td><span class="badge ${part.status === 'Cut' ? 'bg-success' : part.status === 'Pending' ? 'bg-secondary' : 'bg-primary'}">${part.status}</span></td>
                                        <td>${part.material || 'N/A'}</td>
                                        <td class="small">${[part.length && `L:${part.length}`, part.width && `W:${part.width}`, part.thickness && `T:${part.thickness}`].filter(Boolean).join('×') || 'N/A'}</td>
                                    </tr>
                                `).join('')}
                            </tbody>
                        </table>
                    </div>
                `;
                
                document.getElementById('nestSheetDetailsContent').innerHTML = content;
                const modal = new bootstrap.Modal(document.getElementById('nestSheetDetailsModal'));
                modal.show();
            } else {
                alert('Error: ' + data.message);
            }
        })
        .catch(error => {
            console.error('Error loading nest sheet details:', error);
            alert('An error occurred while loading nest sheet details.');
        });
}

// Load recent scans modal
function loadRecentScans() {
    const modal = new bootstrap.Modal(document.getElementById('recentScansModal'));
    const content = document.getElementById('recentScansContent');
    
    // Show loading
    content.innerHTML = `
        <div class="text-center">
            <div class="spinner-border" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
            <div class="mt-2">Loading recent scans...</div>
        </div>
    `;
    
    modal.show();
    
    fetch('/Cnc/GetRecentScans')
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                displayRecentScans(data.scans);
            } else {
                content.innerHTML = `
                    <div class="alert alert-warning">
                        <i class="fas fa-exclamation-triangle me-2"></i>
                        Failed to load recent scans: ${data.message}
                    </div>
                `;
            }
        })
        .catch(error => {
            console.error('Error loading recent scans:', error);
            content.innerHTML = `
                <div class="alert alert-danger">
                    <i class="fas fa-times-circle me-2"></i>
                    An error occurred while loading recent scans.
                </div>
            `;
        });
}

function displayRecentScans(scans) {
    const content = document.getElementById('recentScansContent');
    
    if (!scans || scans.length === 0) {
        content.innerHTML = `
            <div class="text-center text-muted">
                <i class="fas fa-search fa-3x mb-3"></i>
                <p>No recent scans found.</p>
            </div>
        `;
        return;
    }

    const scanRows = scans.map(scan => {
        const statusIcon = scan.isSuccessful 
            ? '<i class="fas fa-check-circle text-success"></i>'
            : '<i class="fas fa-times-circle text-danger"></i>';
            
        const statusClass = scan.isSuccessful ? 'table-success' : 'table-danger';
        
        return `
            <tr class="${statusClass}">
                <td>${scan.timestamp}</td>
                <td><code>${scan.barcode}</code></td>
                <td>${statusIcon}</td>
                <td>${scan.nestSheetName || 'N/A'}</td>
                <td>${scan.partsProcessed || 'N/A'}</td>
                <td>${scan.errorMessage || 'Success'}</td>
            </tr>
        `;
    }).join('');

    content.innerHTML = `
        <div class="table-responsive">
            <table class="table table-sm">
                <thead>
                    <tr>
                        <th>Time</th>
                        <th>Barcode</th>
                        <th>Status</th>
                        <th>Nest Sheet</th>
                        <th>Parts</th>
                        <th>Details</th>
                    </tr>
                </thead>
                <tbody>
                    ${scanRows}
                </tbody>
            </table>
        </div>
    `;
}

// Toast notification function
function showToast(message, type = 'info') {
    const toastContainer = document.getElementById('toast-container') || createToastContainer();
    const toast = document.createElement('div');
    toast.className = `toast align-items-center text-white bg-${type === 'success' ? 'success' : type === 'error' ? 'danger' : 'primary'} border-0`;
    toast.setAttribute('role', 'alert');
    toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">${message}</div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
        </div>
    `;
    toastContainer.appendChild(toast);
    const bsToast = new bootstrap.Toast(toast);
    bsToast.show();
    
    // Remove toast element after it's hidden
    toast.addEventListener('hidden.bs.toast', () => {
        toast.remove();
    });
}

function createToastContainer() {
    const container = document.createElement('div');
    container.id = 'toast-container';
    container.className = 'toast-container position-fixed top-0 end-0 p-3';
    container.style.zIndex = '1055';
    document.body.appendChild(container);
    return container;
}

// SignalR and Initialization
document.addEventListener('DOMContentLoaded', function() {
    // Listen for active work order changes from the dropdown
    window.addEventListener('activeWorkOrderChanged', function(event) {
        const { workOrderId, workOrderName } = event.detail;
        
        // Update SignalR groups for real-time updates
        if (window.cncConnection && window.cncConnection.state === signalR.HubConnectionState.Connected) {
            // Leave previous work order group and join new one
            if (window.currentWorkOrderId) {
                window.cncConnection.invoke("LeaveWorkOrderGroup", window.currentWorkOrderId);
            }
            if (workOrderId) {
                window.cncConnection.invoke("JoinWorkOrderGroup", workOrderId);
                window.currentWorkOrderId = workOrderId;
            }
        }
        
        // Reload to get the correct context for the new work order
        location.reload();
    });

    // Restore user preferences from localStorage on page load
    const showProcessed = ShopBossPreferences.CNC.getShowProcessed();
    const showToggle = document.getElementById('showCutToggle');
    if (showToggle) {
        showToggle.checked = showProcessed;
        toggleCutSheets(); // Apply the restored state
    }
    
    // Restore "Group by Material" toggle state
    const groupByMaterial = ShopBossPreferences.CNC.getGroupByMaterial();
    const groupToggle = document.getElementById('groupByMaterialToggle');
    if (groupToggle) {
        groupToggle.checked = groupByMaterial;
        toggleMaterialGrouping(); // Apply the restored state
    }

    // Initialize SignalR connection
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/status")
        .build();

    // Store connection globally for work order change handling
    window.cncConnection = connection;

    // Start SignalR connection
    connection.start().then(function () {
        // Join CNC station group for real-time updates
        connection.invoke("JoinCncGroup");
        
        // Join work order group if active work order is set
        const activeWorkOrderIdElement = document.querySelector('[data-active-work-order-id]');
        const activeWorkOrderId = activeWorkOrderIdElement ? activeWorkOrderIdElement.dataset.activeWorkOrderId : null;
        if (activeWorkOrderId) {
            connection.invoke("JoinWorkOrderGroup", activeWorkOrderId);
            window.currentWorkOrderId = activeWorkOrderId;
        }
    }).catch(function (err) {
        console.error("SignalR connection error:", err.toString());
    });

    // Handle real-time status updates
    connection.on("StatusUpdate", function (data) {
        if (data.type === "nest-sheet-processed") {
            // Show toast notification
            showToast(`Nest sheet "${data.nestSheetName}" processed successfully! ${data.partsProcessed} parts marked as Cut.`, 'success');
            
            // Refresh immediately without delay
            location.reload();
        }
    });

    connection.on("NestSheetProcessed", function (data) {
        showToast(`Nest sheet "${data.nestSheetName}" processed at ${data.processedDate}`, 'success');
    });

    // Auto-refresh recent scans when modal is opened
    const recentScansModal = document.getElementById('recentScansModal');
    if (recentScansModal) {
        recentScansModal.addEventListener('shown.bs.modal', function () {
            loadRecentScans();
        });
    }
});