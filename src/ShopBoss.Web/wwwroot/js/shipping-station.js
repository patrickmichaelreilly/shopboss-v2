// Shipping Station JavaScript

function updateOverallProgress() {
    $.get('/Shipping/GetShippingProgress')
        .done(function(response) {
            if (response.success && response.data) {
                const data = response.data;
                // Handle both camelCase (from JSON serialization) and PascalCase
                const totalItems = (data.products?.total || data.Products?.Total || 0) + 
                                 (data.hardware?.total || data.Hardware?.Total || 0) + 
                                 (data.detachedProducts?.total || data.DetachedProducts?.Total || 0);
                const shippedItems = (data.products?.shipped || data.Products?.Shipped || 0) + 
                                   (data.hardware?.shipped || data.Hardware?.Shipped || 0) + 
                                   (data.detachedProducts?.shipped || data.DetachedProducts?.Shipped || 0);
                
                const percentage = totalItems > 0 ? Math.round((shippedItems / totalItems) * 100) : 0;
                
                $('#overallProgressText').text(`${shippedItems}/${totalItems} (${percentage}%)`);
                
                const progressRing = $('#overallProgressRing');
                if (percentage === 100) {
                    progressRing.addClass('completed');
                    $('#shippingCompleteAlert').removeClass('d-none');
                } else {
                    progressRing.removeClass('completed');
                    $('#shippingCompleteAlert').addClass('d-none');
                }
            } else {
                console.error('Progress response not successful:', response);
                $('#overallProgressText').text('Error: ' + (response.message || 'Unknown error'));
            }
        })
        .fail(function(xhr, status, error) {
            console.error('Progress request failed:', status, error);
            $('#overallProgressText').text('Error loading progress');
        });
}

function showAlert(message, type) {
    const alertHtml = `
        <div class="alert alert-${type} alert-dismissible fade show" role="alert">
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `;
    
    // Remove existing alerts
    $('.alert').remove();
    
    // Add new alert at the top of the page
    $('main').prepend(alertHtml);
    
    // Auto-dismiss after 5 seconds
    setTimeout(() => {
        $('.alert').fadeOut();
    }, 5000);
}

// Ship product function
function shipProduct(productId) {
    if (!confirm('Are you sure you want to ship this product?')) {
        return;
    }
    
    $.ajax({
        url: '/Shipping/ShipProduct',
        method: 'POST',
        data: { productId: productId },
        dataType: 'json'
    })
    .done(function(response) {
        if (response.success) {
            showAlert(response.message, 'success');
            updateOverallProgress();
            // Hide the ship button and update UI for this product
            updateProductShippedUI(productId);
        } else {
            showAlert(response.message || 'Error shipping product', 'danger');
        }
    })
    .fail(function() {
        showAlert('Error shipping product', 'danger');
    });
}

// Ship hardware function
function shipHardware(hardwareId) {
    if (!confirm('Are you sure you want to ship this hardware item?')) {
        return;
    }
    
    $.ajax({
        url: '/Shipping/ScanHardware',
        method: 'POST',
        data: { barcode: hardwareId },
        dataType: 'json'
    })
    .done(function(response) {
        if (response.success) {
            showAlert(response.message, 'success');
            updateOverallProgress();
            // Hide the ship button and update UI for this hardware
            updateHardwareShippedUI(hardwareId);
        } else {
            showAlert(response.message || 'Error shipping hardware', 'danger');
        }
    })
    .fail(function() {
        showAlert('Error shipping hardware', 'danger');
    });
}

// Ship detached product function
function shipDetachedProduct(detachedProductId) {
    if (!confirm('Are you sure you want to ship this detached product?')) {
        return;
    }
    
    $.ajax({
        url: '/Shipping/ShipDetachedProduct',
        method: 'POST',
        data: { detachedProductId: detachedProductId },
        dataType: 'json'
    })
    .done(function(response) {
        if (response.success) {
            showAlert(response.message, 'success');
            updateOverallProgress();
            // Hide the ship button and update UI for this detached product
            updateDetachedProductShippedUI(detachedProductId);
        } else {
            showAlert(response.message || 'Error shipping detached product', 'danger');
        }
    })
    .fail(function() {
        showAlert('Error shipping detached product', 'danger');
    });
}

// Ship hardware group function
function shipHardwareGroup(groupName, hardwareIds) {
    if (!confirm(`Are you sure you want to ship all hardware items in group "${groupName}"?`)) {
        return;
    }
    
    const ids = hardwareIds.split(',');
    const totalCount = ids.length;
    
    showAlert(`🔄 Shipping ${totalCount} hardware items in group "${groupName}"...`, 'info');
    
    // Use the new bulk shipping endpoint to avoid database locking
    $.ajax({
        url: '/Shipping/ShipHardwareGroup',
        method: 'POST',
        data: { hardwareIds: hardwareIds },
        dataType: 'json'
    })
    .done(function(response) {
        if (response.success) {
            showAlert(response.message, 'success');
            // Refresh immediately to show updated hardware grouping
            location.reload();
        } else {
            showAlert(`❌ ${response.message}`, 'danger');
        }
    })
    .fail(function(xhr, status, error) {
        console.error('Error shipping hardware group:', error);
        showAlert(`❌ Error shipping hardware group "${groupName}"`, 'danger');
    });
}

// UI update functions to change styling when items are shipped
function updateProductShippedUI(productId) {
    // Find the product container
    const productButton = $(`button[onclick="shipProduct('${productId}')"]`);
    const productContainer = productButton.closest('.shipping-item');
    
    // Hide the ship button
    productButton.hide();
    
    // Update container styling
    productContainer.removeClass('ready not-ready').addClass('shipped');
    
    // Update status badge
    const statusBadge = productContainer.find('.status-badge');
    statusBadge.removeClass('ready not-ready').addClass('shipped');
    statusBadge.html('<i class="fas fa-check me-1"></i>Shipped');
}

function updateHardwareShippedUI(hardwareId) {
    // Find the hardware container
    const hardwareButton = $(`button[onclick="shipHardware('${hardwareId}')"]`);
    const hardwareContainer = hardwareButton.closest('.shipping-item');
    
    // Hide the ship button
    hardwareButton.hide();
    
    // Update container styling
    hardwareContainer.removeClass('ready').addClass('shipped');
    
    // Update status badge
    const statusBadge = hardwareContainer.find('.status-badge');
    statusBadge.removeClass('ready').addClass('shipped');
    statusBadge.html('<i class="fas fa-check me-1"></i>Shipped');
}

function updateDetachedProductShippedUI(detachedProductId) {
    // Find the detached product container
    const detachedButton = $(`button[onclick="shipDetachedProduct('${detachedProductId}')"]`);
    const detachedContainer = detachedButton.closest('.shipping-item');
    
    // Hide the ship button
    detachedButton.hide();
    
    // Update container styling
    detachedContainer.removeClass('ready').addClass('shipped');
    
    // Update status badge
    const statusBadge = detachedContainer.find('.status-badge');
    statusBadge.removeClass('ready').addClass('shipped');
    statusBadge.html('<i class="fas fa-check me-1"></i>Shipped');
}

// SignalR and Initialization
$(document).ready(function() {
    // Listen for active work order changes from the dropdown
    window.addEventListener('activeWorkOrderChanged', function(event) {
        const { workOrderId, workOrderName } = event.detail;
        
        // Update SignalR groups for real-time updates
        if (window.shippingConnection && window.shippingConnection.state === signalR.HubConnectionState.Connected) {
            // Leave previous work order group and join new one
            if (window.currentWorkOrderId) {
                window.shippingConnection.invoke("LeaveWorkOrderGroup", window.currentWorkOrderId);
            }
            if (workOrderId) {
                window.shippingConnection.invoke("JoinWorkOrderGroup", workOrderId);
                window.currentWorkOrderId = workOrderId;
            }
        }
        
        // Reload the page to show shipping items for the new work order
        location.reload();
    });

    // Initialize SignalR connection
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/status")
        .build();

    // Store connection globally for work order change handling
    window.shippingConnection = connection;

    // Start the connection
    connection.start().then(function () {
        // Join the shipping station group
        connection.invoke("JoinGroup", "shipping-station");
        
        // Join work order group if active work order is set
        const activeWorkOrderIdElement = document.querySelector('[data-active-work-order-id]');
        const activeWorkOrderId = activeWorkOrderIdElement ? activeWorkOrderIdElement.dataset.activeWorkOrderId : null;
        if (activeWorkOrderId) {
            connection.invoke("JoinWorkOrderGroup", activeWorkOrderId);
            window.currentWorkOrderId = activeWorkOrderId;
        }
    }).catch(function (err) {
        console.error("Error connecting to SignalR hub:", err);
    });

    // Listen for assembly completions
    connection.on("ProductReadyForShipping", function (data) {
        // Update the UI without full page reload
        updateOverallProgress();
    });

    // Listen for general status updates
    connection.on("StatusUpdate", function (data) {
        if (data.type === "product-assembled") {
            // Update the UI without full page reload
            updateOverallProgress();
        }
    });

    // Initialize page
    setTimeout(updateOverallProgress, 500); // Delay to ensure page is fully loaded

    // Make functions global
    window.shipProduct = shipProduct;
    window.shipHardware = shipHardware;
    window.shipDetachedProduct = shipDetachedProduct;
    window.shipHardwareGroup = shipHardwareGroup;
});