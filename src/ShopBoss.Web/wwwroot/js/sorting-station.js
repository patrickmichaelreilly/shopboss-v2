// Sorting Station JavaScript

// Global variables
let currentRackId = null;
let currentCutPartsCount = 0; // Will be initialized from ViewBag
let currentBlinkingBinLabel = null;
let signalRConnection = null;
let currentBinDetails = null;

// Move mode state
let isMoveMode = false;
let selectedSourceBin = null;

// ===== Rack Dropdown Functions =====

function selectRackFromDropdown(rackId, rackName, rackIcon, rackColor, occupied, total) {
    // Save rack selection to localStorage for persistence
    ShopBossPreferences.Sorting.setSelectedRack(rackId);
    
    // Navigate to URL with rackId parameter for consistency
    window.location.href = `/Sorting?rackId=${encodeURIComponent(rackId)}`;
}

function updateRackBadge(rackId, occupiedBins, totalBins) {
    // If bins data not provided, fetch from server
    if (occupiedBins === undefined || totalBins === undefined) {
        fetch(`/Sorting/GetRackOccupancy/${rackId}`)
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    updateRackBadge(rackId, data.occupiedBins, data.totalBins);
                }
            })
            .catch(error => console.error('Error fetching rack occupancy:', error));
        return;
    }
    
    // Update dropdown item badge
    const dropdownItem = document.querySelector(`[data-rack-id="${rackId}"]`);
    if (dropdownItem) {
        const badge = dropdownItem.querySelector('.badge');
        if (badge) {
            badge.textContent = `${occupiedBins}/${totalBins}`;
        }
        // Update data attributes for future selections
        dropdownItem.setAttribute('data-rack-occupied', occupiedBins);
        dropdownItem.setAttribute('data-rack-total', totalBins);
    }
    
    // If this is the currently selected rack, update the dropdown button badge
    if (currentRackId === rackId) {
        const selectedBadge = document.getElementById('selectedRackBadge');
        if (selectedBadge) {
            selectedBadge.textContent = `${occupiedBins}/${totalBins}`;
        }
    }
}

// ===== Rack Display Functions =====

function loadRackDetails(rackId) {
    currentRackId = rackId;
    
    if (!rackId) {
        console.error('loadRackDetails: No rackId provided');
        return;
    }
    
    fetch(`/Sorting/GetRackDetails/${encodeURIComponent(rackId)}`)
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                displayRackGrid(data.rack);
            } else {
                console.error('Failed to load rack details:', data.message);
            }
        })
        .catch(error => {
            console.error('Error loading rack details:', error);
        });
}

function displayRackGrid(rack) {
    const container = document.getElementById('rack-grid-container');
    
    if (!rack.bins || rack.bins.length === 0) {
        container.innerHTML = `
            <div class="text-center text-muted">
                <i class="fas fa-inbox fa-3x mb-3"></i>
                <p>No bins configured for this rack</p>
            </div>
        `;
        return;
    }
    
    // Use rack's configured grid dimensions
    const rows = rack.rows;
    const cols = rack.columns;
    
    // Create CSS Grid with determined dimensions
    let gridHtml = `<div class="rack-grid" style="grid-template-rows: repeat(${rows}, 1fr); grid-template-columns: repeat(${cols}, 1fr);">`;
    
    // Generate grid items from flat bin list
    for (let i = 0; i < rack.bins.length; i++) {
        const bin = rack.bins[i];
        
        // Determine status class
        const statusClass = bin.status === 'blocked' ? 'bin-blocked' : 
                          bin.progressPercentage >= 100 ? 'bin-full' : 
                          getBinStatusClass(bin.status);
        const title = getBinTooltip(bin);
        
        gridHtml += `
            <div class="grid-bin ${statusClass}" 
                 title="${title}"
                 onclick="selectBin('${bin.id}', '${bin.label}')">
                <div class="bin-content">
                    <div class="bin-label">${bin.label}</div>
                    ${bin.status !== 'empty' ? 
                        `<div class="bin-details">
                            <div class="product-id-circle">${bin.itemNumber || '?'}</div>
                            <div class="part-count">${bin.partsCount} parts</div>
                        </div>` : 
                        ''
                    }
                </div>
            </div>
        `;
    }
    
    // Fill empty grid cells if needed
    for (let i = rack.bins.length; i < rows * cols; i++) {
        gridHtml += `<div class="grid-bin bin-empty" style="visibility: hidden;"></div>`;
    }
    
    gridHtml += '</div>';

    // Add legend
    gridHtml += `
        <div class="rack-legend mt-3">
            <div class="d-flex justify-content-center flex-wrap gap-3">
                <div class="legend-item">
                    <div class="legend-color bin-empty"></div>
                    <span>Empty</span>
                </div>
                <div class="legend-item">
                    <div class="legend-color bin-partial"></div>
                    <span>Partial</span>
                </div>
                <div class="legend-item">
                    <div class="legend-color bin-full"></div>
                    <span>Full</span>
                </div>
                <div class="legend-item">
                    <div class="legend-color bin-blocked"></div>
                    <span>Blocked</span>
                </div>
            </div>
        </div>
    `;

    container.innerHTML = gridHtml;
    
    // Reapply blinking animation if there's a currently blinking bin
    if (currentBlinkingBinLabel) {
        applyBlinkingToBin(currentBlinkingBinLabel);
    }
}

function getBinStatusClass(status) {
    switch (status) {
        case 'empty': return 'bin-empty';
        case 'partial': return 'bin-partial';
        case 'full': return 'bin-full';
        case 'blocked': return 'bin-blocked';
        case 'reserved': return 'bin-reserved';
        default: return 'bin-empty';
    }
}

function getBinTooltip(bin) {
    if (bin.status === 'empty') {
        return `Bin ${bin.label}: Empty (Available)`;
    }
    
    let tooltip = `Bin ${bin.label}: ${bin.statusText}`;
    if (bin.contents) tooltip += `\nContents: ${bin.contents}`;
    if (bin.productName) tooltip += `\nProduct: ${bin.productName}`;
    if (bin.itemNumber) tooltip += `\nItem: ${bin.itemNumber}`;
    
    // Show Work Order info for blocked bins
    if (bin.status === 'blocked' && bin.statusText === 'Blocked - Different Work Order' && bin.workOrderId) {
        tooltip += `\nWork Order: ${bin.workOrderName || bin.workOrderId}`;
    }
    
    if (bin.assignedDate) tooltip += `\nAssigned: ${bin.assignedDate}`;
    if (bin.notes) tooltip += `\nNotes: ${bin.notes}`;
    tooltip += `\nProgress: ${bin.partsCount}${bin.totalNeeded ? `/${bin.totalNeeded}` : ''} parts`;
    tooltip += `\nClick to view details`;
    
    return tooltip;
}

function selectBin(binId, label) {
    if (isMoveMode) {
        handleMoveModeClick(binId, label);
    } else {
        showBinDetailModal(binId, label);
    }
}

// ===== Move Mode Functions =====

function toggleMoveMode() {
    if (isMoveMode) {
        exitMoveMode();
    } else {
        enterMoveMode();
    }
}

function enterMoveMode() {
    isMoveMode = true;
    selectedSourceBin = null;
    
    // Update button appearance
    const moveButton = document.getElementById('moveBinButton');
    if (moveButton) {
        moveButton.classList.remove('btn-outline-warning');
        moveButton.classList.add('btn-warning');
        moveButton.innerHTML = '<i class="fas fa-times me-2"></i>Cancel Move';
    }
    
    // Add visual indicators to bins
    addMoveModeVisualIndicators();
    
    // Show instructions
    showBillboard('sorting-billboard', 'Move Mode: Click a bin to move, then click the destination bin. Click Cancel Move to exit.', 'info', 'Move Mode Active');
}

function exitMoveMode() {
    isMoveMode = false;
    selectedSourceBin = null;
    
    // Reset button appearance
    const moveButton = document.getElementById('moveBinButton');
    if (moveButton) {
        moveButton.classList.remove('btn-warning');
        moveButton.classList.add('btn-outline-warning');
        moveButton.innerHTML = '<i class="fas fa-arrows-alt me-2"></i>Move Bin';
    }
    
    // Remove visual indicators
    removeMoveModeVisualIndicators();
    
    // Clear billboard
    clearBillboard('sorting-billboard');
}

function addMoveModeVisualIndicators() {
    const binElements = document.querySelectorAll('.grid-bin');
    binElements.forEach(bin => {
        bin.classList.add('move-mode-active');
        
        // Add different styling for empty vs occupied bins
        if (bin.classList.contains('bin-empty')) {
            bin.classList.add('move-destination-available');
        } else {
            bin.classList.add('move-source-available');
        }
    });
}

function removeMoveModeVisualIndicators() {
    const binElements = document.querySelectorAll('.grid-bin');
    binElements.forEach(bin => {
        bin.classList.remove('move-mode-active', 'move-destination-available', 'move-source-available', 'move-source-selected');
    });
}

function handleMoveModeClick(binId, label) {
    if (!selectedSourceBin) {
        // Selecting source bin
        const binElement = document.querySelector(`[onclick*="${binId}"]`);
        
        // Validate source bin is not empty
        if (binElement && binElement.classList.contains('bin-empty')) {
            showBillboard('sorting-billboard', `Bin ${label} is empty - nothing to move. Select a bin with contents.`, 'warning', 'Move Mode');
            return;
        }
        
        selectSourceBin(binId, label);
    } else {
        // Selecting destination bin
        if (selectedSourceBin.id === binId) {
            showBillboard('sorting-billboard', `Cannot move bin ${label} to itself. Select a different destination bin.`, 'warning', 'Move Mode');
            return;
        }
        
        selectDestinationBin(binId, label);
    }
}

function selectSourceBin(binId, label) {
    selectedSourceBin = { id: binId, label: label };
    
    // Highlight selected source bin
    const binElement = document.querySelector(`[onclick*="${binId}"]`);
    if (binElement) {
        binElement.classList.add('move-source-selected');
    }
    
    showBillboard('sorting-billboard', `Selected bin ${label} to move. Now click an empty bin as the destination.`, 'info', 'Move Mode');
}

function selectDestinationBin(binId, label) {
    const binElement = document.querySelector(`[onclick*="${binId}"]`);
    
    // Validate destination bin is empty
    if (binElement && !binElement.classList.contains('bin-empty')) {
        showBillboard('sorting-billboard', `Bin ${label} is not empty. Select an empty bin as the destination.`, 'warning', 'Move Mode');
        return;
    }
    
    // Perform the move
    performBinMove(selectedSourceBin.id, selectedSourceBin.label, binId, label);
}

async function performBinMove(sourceBinId, sourceLabel, destinationBinId, destinationLabel) {
    showBillboard('sorting-billboard', `Moving contents from bin ${sourceLabel} to bin ${destinationLabel}...`, 'info', 'Move Mode');
    
    const response = await fetch('/Sorting/MoveBin', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'X-Requested-With': 'XMLHttpRequest'
        },
        body: `sourceBinId=${encodeURIComponent(sourceBinId)}&destinationBinId=${encodeURIComponent(destinationBinId)}`
    });
    
    if (!response.ok) {
        showBillboard('sorting-billboard', `Failed to move bin contents`, 'danger', 'Move Error');
        return;
    }
    
    const data = await response.json();
    
    if (data.success) {
        showBillboard('sorting-billboard', `Successfully moved contents from bin ${sourceLabel} to bin ${destinationLabel}`, 'success', 'Move Complete');
        
        // Exit move mode
        exitMoveMode();
        
        // Refresh the rack display
        if (currentRackId) {
            loadRackDetails(currentRackId);
        }
    } else {
        showBillboard('sorting-billboard', `Failed to move bin contents: ${data.message}`, 'danger', 'Move Error');
    }
}

// ===== Cut Parts Functions =====

function loadCutParts() {
    const modal = new bootstrap.Modal(document.getElementById('cutPartsModal'));
    const content = document.getElementById('cutPartsContent');
    
    content.innerHTML = `
        <div class="text-center">
            <div class="spinner-border" role="status"></div>
            <div class="mt-2">Loading cut parts...</div>
        </div>
    `;
    
    modal.show();
    
    // Refresh cut parts count when modal opens
    fetch('/Sorting/GetCurrentCutPartsCount')
        .then(response => response.json())
        .then(countData => {
            if (countData.success) {
                updateCutPartsCount(countData.count);
            }
        });
    
    fetch('/Sorting/GetCutParts')
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                displayCutParts(data.parts);
            } else {
                content.innerHTML = `
                    <div class="alert alert-warning">
                        <i class="fas fa-exclamation-triangle me-2"></i>
                        Failed to load cut parts: ${data.message}
                    </div>
                `;
            }
        })
        .catch(error => {
            console.error('Error loading cut parts:', error);
            content.innerHTML = `
                <div class="alert alert-danger">
                    <i class="fas fa-times-circle me-2"></i>
                    An error occurred while loading cut parts.
                </div>
            `;
        });
}

function displayCutParts(parts) {
    const content = document.getElementById('cutPartsContent');
    
    if (!parts || parts.length === 0) {
        content.innerHTML = `
            <div class="text-center text-muted">
                <i class="fas fa-check-circle fa-3x mb-3 text-success"></i>
                <p>All parts have been sorted! No cut parts remaining.</p>
            </div>
        `;
        return;
    }

    let html = `
        <div class="table-responsive">
            <table class="table table-striped table-sm">
                <thead>
                    <tr>
                        <th>Part Name</th>
                        <th>Product</th>
                        <th>Qty</th>
                        <th>Material</th>
                        <th>Dimensions</th>
                        <th>Nest Sheet</th>
                        <th>Action</th>
                    </tr>
                </thead>
                <tbody>
    `;

    parts.forEach(part => {
        const dimensions = [
            part.length && `L:${part.length}`,
            part.width && `W:${part.width}`,
            part.thickness && `T:${part.thickness}`
        ].filter(Boolean).join('×') || 'N/A';

        html += `
            <tr>
                <td><strong>${part.name}</strong></td>
                <td>${part.productName}</td>
                <td>${part.qty}</td>
                <td>${part.material || 'N/A'}</td>
                <td class="small">${dimensions}</td>
                <td class="small">${part.nestSheetName}</td>
                <td>
                    <button type="button" class="btn btn-sm btn-primary" 
                            onclick="handleSortingScan('${part.id}', Object.values(window.universalScanners)[0], this)">
                        <i class="fas fa-sort me-1"></i>Sort
                    </button>
                </td>
            </tr>
        `;
    });

    html += `
                </tbody>
            </table>
        </div>
    `;

    content.innerHTML = html;
}

// ===== Scanning Functions =====

async function handleSortingScan(barcode, scanner, buttonElement = null) {
    try {
        // Show processing status
        scanner.showScanResult(false, '🔄 Processing part for sorting...', false);
        
        // Get currently displayed rack ID
        const selectedRackId = currentRackId;
        
        // Build request body with selected rack context
        let requestBody = `barcode=${encodeURIComponent(barcode)}`;
        if (selectedRackId) {
            requestBody += `&selectedRackId=${encodeURIComponent(selectedRackId)}`;
        }
        
        // Use existing sorting endpoint
        const response = await fetch('/Sorting/ScanPart', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: requestBody
        });
        
        const data = await response.json();
        
        if (data.success) {
            scanner.showScanResult(true, data.message);
            
            // Remove the part row if button was clicked from cut parts modal
            if (buttonElement) {
                const row = buttonElement.closest('tr');
                if (row) {
                    row.remove();
                }
            }
            
            // Refresh the current rack data
            setTimeout(() => {
                if (currentRackId) {
                    loadRackDetails(currentRackId);
                }
                // Update cut parts count
                if (data.remainingCutParts !== undefined) {
                    updateCutPartsCount(data.remainingCutParts);
                }
            }, 100);
        } else {
            scanner.showScanResult(false, data.message || 'Part sorting failed');
        }
    } catch (error) {
        console.error('Sorting scan error:', error);
        scanner.showScanResult(false, '❌ Network error. Please try again.');
    }
}

// ===== SignalR Functions =====

function setupSignalRConnection() {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/status")
        .withAutomaticReconnect()
        .build();

    connection.start().then(function () {
        // Join sorting station group
        connection.invoke("JoinGroup", "sorting-station").catch(err => console.error(err));
        
        // Join work order group if active work order exists
        const activeWorkOrderId = document.getElementById('activeWorkOrderId')?.value;
        if (activeWorkOrderId) {
            connection.invoke("JoinGroup", `workorder-${activeWorkOrderId}`).catch(err => console.error(err));
        }
        
    }).catch(function (err) {
        console.error("SignalR Connection Error: " + err.toString());
        // Retry connection in 5 seconds
        setTimeout(() => setupSignalRConnection(), 5000);
    });

    // Handle part sorted updates
    connection.on("PartSorted", function (data) {
        showBillboard('sorting-billboard', `${data.productName} ${data.partName} sorted to ${data.binLabel}`, 'info', 'Part Sorted');
        
        // Always refresh current rack
        if (currentRackId) {
            loadRackDetails(currentRackId);
            
            // Highlight the bin that was sorted to
            setTimeout(() => {
                highlightBinByLabel(data.binLabel);
            }, 100);
        }
    });

    // Handle rack occupancy updates
    connection.on("RackOccupancyUpdate", function (data) {
        updateRackBadge(data.rackId, data.occupiedBins, data.totalBins);
    });

    // Handle cut parts count updates
    connection.on("CutPartsCountUpdate", function (data) {
        updateCutPartsCount(data.count);
    });

    // Handle product ready for assembly
    connection.on("ProductReadyForAssembly", function (data) {
        showAssemblyReadyNotification(data);
    });

    // Handle status updates
    connection.on("StatusUpdate", function (data) {
        if (data.type === "part-sorted") {
            // Force refresh current rack
            if (currentRackId) {
                loadRackDetails(currentRackId);
            }
            
            // Update rack badge if available
            if (data.rackId) {
                updateRackBadge(data.rackId);
            }
        } else if (data.type === "bin-moved") {
            // Force refresh current rack after bin move
            if (currentRackId) {
                loadRackDetails(currentRackId);
            }
        }
    });

    // Connection handlers
    connection.onclose(function () {
        showBillboard('sorting-billboard', "Connection lost - attempting to reconnect...", 'warning', 'Connection Status');
    });

    connection.onreconnecting(function () {
        showBillboard('sorting-billboard', "Reconnecting to server...", 'info', 'Connection Status');
    });

    connection.onreconnected(function () {
        showBillboard('sorting-billboard', "Reconnected to server", 'success', 'Connection Status');
        
        // Rejoin groups after reconnection
        connection.invoke("JoinGroup", "sorting-station").catch(err => console.error(err));
        const activeWorkOrderId = document.getElementById('activeWorkOrderId')?.value;
        if (activeWorkOrderId) {
            connection.invoke("JoinGroup", `workorder-${activeWorkOrderId}`).catch(err => console.error(err));
        }
    });

    return connection;
}

// ===== Utility Functions =====

function updateCutPartsCount(count) {
    currentCutPartsCount = count;
    const countElement = document.getElementById('cutPartsCount');
    if (countElement) {
        countElement.textContent = count;
    }
}

function highlightBinByLabel(binLabel) {
    // Clear previous blinking bin
    currentBlinkingBinLabel = null;
    
    // Set new blinking bin
    currentBlinkingBinLabel = binLabel;
    
    // Apply the blinking animation
    applyBlinkingToBin(binLabel);
}

function applyBlinkingToBin(binLabel) {
    // Find all bin elements and clear existing highlights
    const binElements = document.querySelectorAll('.grid-bin');
    binElements.forEach(bin => {
        bin.classList.remove('recently-sorted');
    });
    
    // Find the bin with the matching label and apply blinking
    binElements.forEach(bin => {
        const labelElement = bin.querySelector('.bin-label');
        if (labelElement && labelElement.textContent.trim() === binLabel.trim()) {
            bin.classList.add('recently-sorted');
        }
    });
}

function showAssemblyReadyNotification(data) {
    // Show billboard message for assembly readiness
    showBillboard('sorting-billboard', `Product ${data.productName} (${data.itemNumber || data.productId}) is ready for assembly!`, 'success', 'Ready for Assembly');
}

// ===== Bin Detail Modal Functions =====

function showBinDetailModal(binId, label) {
    const modal = new bootstrap.Modal(document.getElementById('binDetailModal'));
    const content = document.getElementById('binDetailContent');
    const clearBtn = document.getElementById('clearBinBtn');
    
    // Update modal title with prominent bin label
    document.getElementById('binDetailModalLabel').innerHTML = 
        `<i class="fas fa-box me-2"></i>Bin <strong class="text-primary">${label}</strong> Details`;
    
    // Show loading state
    content.innerHTML = `
        <div class="text-center">
            <div class="spinner-border" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
            <div class="mt-2">Loading bin details...</div>
        </div>
    `;
    
    clearBtn.style.display = 'none';
    modal.show();
    
    // Fetch bin details
    fetch(`/Sorting/GetBinDetails?binId=${encodeURIComponent(binId)}`)
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                currentBinDetails = data.bin;
                displayBinDetails(data.bin);
                
                // Always show clear button
                clearBtn.style.display = 'block';
            } else {
                content.innerHTML = `
                    <div class="alert alert-warning">
                        <i class="fas fa-exclamation-triangle me-2"></i>
                        Failed to load bin details: ${data.message}
                    </div>
                `;
            }
        })
        .catch(error => {
            console.error('Error loading bin details:', error);
            content.innerHTML = `
                <div class="alert alert-danger">
                    <i class="fas fa-times-circle me-2"></i>
                    An error occurred while loading bin details.
                </div>
            `;
        });
}

function displayBinDetails(bin) {
    const content = document.getElementById('binDetailContent');
    const modal = document.getElementById('binDetailModal');
    
    // Check if bin is from different work order
    const activeWorkOrderId = document.getElementById('activeWorkOrderId')?.value;
    const isDifferentWorkOrder = bin.workOrderId && activeWorkOrderId && bin.workOrderId !== activeWorkOrderId;
    
    if (isDifferentWorkOrder) {
        modal.classList.add('different-work-order-modal');
    } else {
        modal.classList.remove('different-work-order-modal');
    }
    
    let html = `
        <div class="row mb-4">
            <div class="col-md-6">
                <h6>Bin Information</h6>
                <table class="table table-sm">
                    <tr><td><strong>Bin Label:</strong></td><td><span class="badge bg-primary fs-6 px-3 py-2">${bin.label}</span></td></tr>
                    <tr><td><strong>Status:</strong></td><td><span class="badge bg-${getBinStatusBadgeClass(bin.status === 'blocked' ? 'blocked' : bin.progressPercentage >= 100 ? 'full' : bin.status)}">${bin.status === 'blocked' ? 'Blocked' : bin.progressPercentage >= 100 ? 'Complete' : bin.statusText}</span></td></tr>
                    <tr><td><strong>Progress:</strong></td><td>${bin.status === 'empty' ? '' : `${bin.partsCount}/${bin.totalNeeded || 0} parts`}</td></tr>
                </table>
            </div>
            <div class="col-md-6">
                <h6>Assignment Details</h6>
                <table class="table table-sm">
                    <tr><td><strong>Product:</strong></td><td>${bin.productName || 'Not assigned'}</td></tr>
                    <tr><td><strong>Work Order:</strong></td><td>${bin.workOrderName || 'Not assigned'}</td></tr>
                    <tr><td><strong>Assigned:</strong></td><td>${bin.assignedDate || 'N/A'}</td></tr>
                    <tr><td><strong>Last Updated:</strong></td><td>${bin.lastUpdatedDate || 'N/A'}</td></tr>
                </table>
            </div>
        </div>
    `;
    
    if (bin.parts && bin.parts.length > 0) {
        html += `
            <h6>Parts in Bin (${bin.parts.length})</h6>
            <div class="table-responsive">
                <table class="table table-striped table-sm">
                    <thead>
                        <tr>
                            <th>Part Name</th>
                            <th>Product</th>
                            <th>Qty</th>
                            <th>Material</th>
                            <th>Dimensions</th>
                            <th>Action</th>
                        </tr>
                    </thead>
                    <tbody>
        `;
        
        bin.parts.forEach(part => {
            const dimensions = [
                part.length && `L:${part.length}`,
                part.width && `W:${part.width}`,
                part.thickness && `T:${part.thickness}`
            ].filter(Boolean).join('×') || 'N/A';
            
            html += `
                <tr>
                    <td><strong>${part.name}</strong></td>
                    <td>${part.productName}</td>
                    <td>${part.qty}</td>
                    <td>${part.material || 'N/A'}</td>
                    <td class="small">${dimensions}</td>
                    <td>
                        <button type="button" class="btn btn-sm btn-outline-danger" 
                                onclick="removePartFromBin('${part.id}', '${part.name}')"
                                title="Remove this part from bin">
                            <i class="fas fa-times"></i>
                        </button>
                    </td>
                </tr>
            `;
        });
        
        html += `
                    </tbody>
                </table>
            </div>
        `;
    } else {
        html += `
            <div class="text-center text-muted mt-4">
                <i class="fas fa-inbox fa-3x mb-3"></i>
                <p>This bin is empty</p>
            </div>
        `;
    }
    
    if (bin.notes) {
        html += `
            <div class="mt-3">
                <h6>Notes</h6>
                <div class="alert alert-info">${bin.notes}</div>
            </div>
        `;
    }
    
    content.innerHTML = html;
}

function getBinStatusBadgeClass(status) {
    switch (status) {
        case 'empty': return 'secondary';
        case 'partial': return 'info';
        case 'full': return 'success';
        case 'blocked': return 'danger';
        case 'reserved': return 'warning';
        default: return 'secondary';
    }
}

function removePartFromBin(partId, partName) {
    // Remove the row instantly
    const partRow = document.querySelector(`button[onclick*="${partId}"]`)?.closest('tr');
    if (partRow) {
        partRow.remove();
    }
    
    fetch('/Sorting/RemovePartFromBin', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: `partId=${encodeURIComponent(partId)}`
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showBillboard('sorting-billboard', `Part "${partName}" removed from bin successfully`, 'info', 'Part Removed');
            
            // Refresh the rack display
            if (currentRackId) {
                loadRackDetails(currentRackId);
                updateRackBadge(currentRackId);
            }
            
            // Update cut parts count
            if (data.updatedCutPartsCount !== undefined) {
                updateCutPartsCount(data.updatedCutPartsCount);
            }
        } else {
            showBillboard('sorting-billboard', `Failed to remove part: ${data.message}`, 'danger', 'Remove Error');
            // Refresh the modal to restore the removed row
            if (currentBinDetails) {
                showBinDetailModal(currentBinDetails.id, currentBinDetails.label);
            }
        }
    })
    .catch(error => {
        console.error('Error removing part from bin:', error);
        showBillboard('sorting-billboard', 'An error occurred while removing the part', 'danger', 'Network Error');
        // Refresh the modal to restore the removed row
        if (currentBinDetails) {
            showBinDetailModal(currentBinDetails.id, currentBinDetails.label);
        }
    });
}

function cleanupModalState() {
    // Force cleanup of any stuck modal state
    setTimeout(() => {
        // Remove any modal backdrops
        const backdrops = document.querySelectorAll('.modal-backdrop');
        backdrops.forEach(backdrop => backdrop.remove());
        
        // Reset body classes and styles
        document.body.classList.remove('modal-open');
        document.body.style.removeProperty('overflow');
        document.body.style.removeProperty('padding-right');
        
        // Reset any modal instances
        const modalElement = document.getElementById('binDetailModal');
        if (modalElement) {
            modalElement.style.display = '';
            modalElement.classList.remove('show');
            modalElement.setAttribute('aria-hidden', 'true');
            modalElement.removeAttribute('aria-modal');
            modalElement.removeAttribute('role');
        }
    }, 250);
}

function clearEntireBin() {
    if (!currentBinDetails) return;
    
    fetch('/Sorting/ClearBin', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: `binId=${encodeURIComponent(currentBinDetails.id)}`
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showBillboard('sorting-billboard', `Bin "${currentBinDetails.label}" cleared successfully. ${data.partsRemoved} parts removed.`, 'info', 'Bin Cleared');
            
            // Close the modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('binDetailModal'));
            modal.hide();
            
            // Clean up modal state
            cleanupModalState();
            
            // Refresh the rack display
            if (currentRackId) {
                loadRackDetails(currentRackId);
                updateRackBadge(currentRackId);
            }
            
            // Update cut parts count
            if (data.updatedCutPartsCount !== undefined) {
                updateCutPartsCount(data.updatedCutPartsCount);
            }
            
        } else {
            showBillboard('sorting-billboard', `Failed to clear bin: ${data.message}`, 'danger', 'Clear Error');
        }
    })
    .catch(error => {
        console.error('Error clearing bin:', error);
        showBillboard('sorting-billboard', 'An error occurred while clearing the bin', 'danger', 'Network Error');
    });
}

// ===== Initialization =====

document.addEventListener('DOMContentLoaded', function() {
    // Initialize from ViewBag values
    const selectedRackId = document.getElementById('selectedRackId')?.value;
    const urlParams = new URLSearchParams(window.location.search);
    const urlRackId = urlParams.get('rackId');
    
    // Initialize cut parts count
    const initialCount = document.getElementById('initialCutPartsCount')?.value;
    if (initialCount) {
        currentCutPartsCount = parseInt(initialCount, 10);
    }
    
    // Check if we should redirect to a preferred rack
    if (!urlRackId) {
        const preferredRackId = ShopBossPreferences.Sorting.getSelectedRack();
        if (preferredRackId) {
            const rackExists = document.querySelector(`[data-rack-id="${preferredRackId}"]`);
            if (rackExists) {
                window.location.href = `/Sorting?rackId=${encodeURIComponent(preferredRackId)}`;
                return;
            } else {
                ShopBossPreferences.Sorting.setSelectedRack(null);
            }
        }
    }
    
    if (selectedRackId) {
        loadRackDetails(selectedRackId);
    }
    
    // Setup SignalR connection
    signalRConnection = setupSignalRConnection();
    
    // Add modal cleanup event listeners
    const binModal = document.getElementById('binDetailModal');
    binModal.addEventListener('hidden.bs.modal', function() {
        currentBinDetails = null;
        cleanupModalState();
    });
});

// ===== Scanner Event Listeners =====
// Scan handling is now managed by CompactScanner - no custom handlers needed