// Assembly Station JavaScript

// Billboard toggle functionality
function toggleBillboard() {
    const isChecked = document.getElementById('billboardToggle').checked;
    const mainContainer = document.querySelector('main.container-fluid');
    const billboardContainer = document.getElementById('assembly-billboard');
    
    if (isChecked) {
        mainContainer.classList.remove('billboard-hidden');
        if (billboardContainer) {
            billboardContainer.style.display = '';
        }
    } else {
        mainContainer.classList.add('billboard-hidden');
        if (billboardContainer) {
            billboardContainer.style.display = 'none';
        }
    }
    
    // Save preference to localStorage
    ShopBossPreferences.Assembly.setShowBillboard(isChecked);
}

// Override the global showBillboard function for Assembly station to respect toggle
const originalShowBillboard = window.showBillboard;
window.showBillboard = function(containerId, message, type = 'info', title = 'Status') {
    // Check if this is the Assembly station billboard and if toggle is Off
    if (containerId === 'assembly-billboard') {
        const showBillboard = ShopBossPreferences.Assembly.getShowBillboard();
        if (!showBillboard) {
            // Billboard is toggled off - don't show it
            return;
        }
    }
    
    // Call the original function
    originalShowBillboard(containerId, message, type, title);
};

// Functions for dynamic updates
function updateProductStatus(productId, status) {
    const productCard = document.querySelector(`[data-product-id="${productId}"]`);
    if (productCard) {
        const statusCircle = productCard.querySelector('.product-id-circle');
        const cardBody = productCard.querySelector('.card-body');
        
        if (status === 'completed') {
            // Update product ID circle to completed
            if (statusCircle) {
                statusCircle.className = 'product-id-circle completed';
            }
            
            // Update card class
            productCard.className = productCard.className.replace(/\b(ready|waiting)\b/g, '').trim() + ' completed';
            
            // Update the drop button to show "Dropped"
            const dropButton = cardBody.querySelector('.btn-drop');
            if (dropButton) {
                dropButton.disabled = true;
                dropButton.classList.add('btn-dropped');
                dropButton.innerHTML = '<i class="fas fa-check me-2"></i>Dropped';
            }
        } else if (status === 'ready') {
            // Update product ID circle to ready
            if (statusCircle) {
                statusCircle.className = 'product-id-circle ready';
            }
            
            // Update card class
            productCard.className = productCard.className.replace(/\b(completed|waiting)\b/g, '').trim() + ' ready';
            
            // Fetch and update live bin location data
            updateBinLocations(productId);
        }
    }
}

// Function to fetch and update live bin location data
async function updateBinLocations(productId) {
    try {
        const response = await fetch(`/Assembly/GetProductBinLocations?productId=${productId}`);
        const data = await response.json();
        
        if (data.success) {
            const productCard = document.querySelector(`[data-product-id="${productId}"]`);
            if (productCard) {
                const standardBinBox = productCard.querySelector('.bin-location-box.standard');
                const doorBinBox = productCard.querySelector('.bin-location-box.doors');
                
                if (standardBinBox) {
                    standardBinBox.textContent = data.standardBin;
                    standardBinBox.className = `bin-location-box standard ${data.standardBin === '-' ? 'empty' : ''}`;
                }
                
                if (doorBinBox) {
                    doorBinBox.textContent = data.filteredBin;
                    doorBinBox.className = `bin-location-box doors ${data.filteredBin === '-' ? 'empty' : ''}`;
                }
            }
        }
    } catch (error) {
        console.error('Error updating bin locations:', error);
    }
}

// Function to immediately clear bin locations when product is assembled
function clearProductBinLocations(productId) {
    const productCard = document.querySelector(`[data-product-id="${productId}"]`);
    if (productCard) {
        const standardBinBox = productCard.querySelector('.bin-location-box.standard');
        const doorBinBox = productCard.querySelector('.bin-location-box.doors');
        
        if (standardBinBox) {
            standardBinBox.textContent = '-';
            standardBinBox.className = 'bin-location-box standard empty';
        }
        
        if (doorBinBox) {
            doorBinBox.textContent = '-';
            doorBinBox.className = 'bin-location-box doors empty';
        }
    }
}

function updateShippingReadinessStatus(data) {
    if (data.IsWorkOrderReadyForShipping) {
        showToast(`🚛 Work Order is now ready for shipping! (${data.ReadyForShippingProducts?.length || 0} products ready)`, 'info');
        showBillboard('assembly-billboard', `Work Order is now ready for shipping! (${data.ReadyForShippingProducts?.length || 0} products ready)`, 'info', 'Shipping Ready');
        
        // Add shipping readiness indicator
        const headerDiv = document.querySelector('h2').parentElement;
        let shippingIndicator = document.getElementById('shipping-ready-indicator');
        if (!shippingIndicator) {
            shippingIndicator = document.createElement('div');
            shippingIndicator.id = 'shipping-ready-indicator';
            shippingIndicator.className = 'alert alert-info mt-3';
            shippingIndicator.innerHTML = `
                <i class="fas fa-shipping-fast me-2"></i>
                <strong>Ready for Shipping!</strong> All products in this work order have been assembled.
            `;
            headerDiv.appendChild(shippingIndicator);
        }
    }
}

function showStationNotification(station, message) {
    const timestamp = new Date().toLocaleTimeString();
    const notificationHtml = `
        <div class="alert alert-info alert-dismissible fade show" role="alert">
            <i class="fas fa-info-circle me-2"></i>
            <strong>${station} Station:</strong> ${message}
            <small class="d-block text-muted mt-1">${timestamp}</small>
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `;
    
    // Find or create notifications container
    let notificationContainer = document.getElementById('station-notifications');
    if (!notificationContainer) {
        notificationContainer = document.createElement('div');
        notificationContainer.id = 'station-notifications';
        notificationContainer.className = 'mt-3';
        const mainContent = document.querySelector('.row.g-4');
        if (mainContent) {
            mainContent.parentElement.insertBefore(notificationContainer, mainContent);
        }
    }
    
    // Add notification
    notificationContainer.insertAdjacentHTML('afterbegin', notificationHtml);
    
    // Auto-remove after 10 seconds
    setTimeout(() => {
        const alerts = notificationContainer.querySelectorAll('.alert');
        if (alerts.length > 3) {
            alerts[alerts.length - 1].remove();
        }
    }, 10000);
}

// Assembly functions
function startAssembly(productId, productName) {
    const button = event.target.closest('button');
    const originalHtml = button.innerHTML;
    button.disabled = true;
    button.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Processing...';

    fetch('/Assembly/StartAssembly', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: new URLSearchParams({
            productId: productId
        })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            // Don't show toast here - SignalR will handle the notification
            // The page will reload when SignalR message is received
        } else {
            showToast(data.message, 'error');
            showBillboard('assembly-billboard', data.message, 'danger', 'Assembly Error');
            button.disabled = false;
            button.innerHTML = originalHtml;
        }
    })
    .catch(error => {
        console.error('Error starting assembly:', error);
        showToast('An error occurred while starting assembly', 'error');
        showBillboard('assembly-billboard', 'An error occurred while starting assembly', 'danger', 'Network Error');
        button.disabled = false;
        button.innerHTML = originalHtml;
    });
}

function showProductDetails(productId) {
    const modal = new bootstrap.Modal(document.getElementById('productDetailsModal'));
    const content = document.getElementById('productDetailsContent');
    
    content.innerHTML = '<div class="text-center"><i class="fas fa-spinner fa-spin fa-2x"></i><p>Loading...</p></div>';
    modal.show();

    fetch(`/Assembly/GetProductDetails?productId=${productId}`)
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                content.innerHTML = generateProductDetailsHtml(data.data);
            } else {
                content.innerHTML = `<div class="alert alert-danger">${data.message}</div>`;
            }
        })
        .catch(error => {
            console.error('Error loading product details:', error);
            content.innerHTML = '<div class="alert alert-danger">Error loading product details</div>';
        });
}

function generateProductDetailsHtml(product) {
    let html = `
        <h6>${product.ItemNumber} - ${product.ProductName}</h6>
        
        <h6 class="mt-3">Standard Parts</h6>
        <div class="table-responsive">
            <table class="table table-sm">
                <thead>
                    <tr>
                        <th>Part Name</th>
                        <th>Status</th>
                        <th>Location</th>
                        <th>Qty</th>
                        <th>Dimensions</th>
                    </tr>
                </thead>
                <tbody>
        `;

    product.StandardParts.forEach(part => {
        const statusBadge = part.Status === 'Sorted' ? 'bg-success' : 
                          part.Status === 'Assembled' ? 'bg-primary' : 'bg-secondary';
        html += `
            <tr>
                <td>${part.Name}</td>
                <td><span class="badge ${statusBadge}">${part.Status}</span></td>
                <td>${part.Location}</td>
                <td>${part.Quantity}</td>
                <td class="small">${part.Dimensions}</td>
            </tr>
        `;
    });

    html += `
                </tbody>
            </table>
        </div>
    `;

    return html;
}

function showToast(message, type) {
    const toastContainer = document.querySelector('.toast-container');
    const toastId = 'toast-' + Date.now();
    
    const bgColor = type === 'success' ? 'bg-success' : 
                   type === 'info' ? 'bg-info' : 
                   type === 'error' ? 'bg-danger' : 'bg-secondary';
    
    const icon = type === 'success' ? 'fa-check-circle' : 
                type === 'info' ? 'fa-info-circle' : 
                type === 'error' ? 'fa-exclamation-circle' : 'fa-bell';

    const toastHtml = `
        <div id="${toastId}" class="toast" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="toast-header ${bgColor} text-white">
                <i class="fas ${icon} me-2"></i>
                <strong class="me-auto">Assembly Station</strong>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
            </div>
            <div class="toast-body">
                ${message}
            </div>
        </div>
    `;

    toastContainer.insertAdjacentHTML('beforeend', toastHtml);
    
    const toastElement = document.getElementById(toastId);
    const toast = new bootstrap.Toast(toastElement, {
        autohide: type !== 'error',
        delay: type === 'success' ? 5000 : 8000
    });
    
    toast.show();
    
    // Remove toast element after it's hidden
    toastElement.addEventListener('hidden.bs.toast', () => {
        toastElement.remove();
    });
}

// SignalR and Initialization
document.addEventListener('DOMContentLoaded', function() {
    // Listen for active work order changes from the dropdown
    window.addEventListener('activeWorkOrderChanged', function(event) {
        const { workOrderId, workOrderName } = event.detail;
        
        // Update SignalR groups for real-time updates
        if (window.assemblyConnection && window.assemblyConnection.state === signalR.HubConnectionState.Connected) {
            // Leave previous work order group and join new one
            if (window.currentWorkOrderId) {
                window.assemblyConnection.invoke("LeaveWorkOrderGroup", window.currentWorkOrderId);
            }
            if (workOrderId) {
                window.assemblyConnection.invoke("JoinWorkOrderGroup", workOrderId);
                window.currentWorkOrderId = workOrderId;
            }
        }
        
        // Reload the page to show assembly queue for the new work order
        location.reload();
    });

    // Restore billboard preference on page load
    const showBillboard = ShopBossPreferences.Assembly.getShowBillboard();
    const billboardToggle = document.getElementById('billboardToggle');
    if (billboardToggle && !showBillboard) {
        billboardToggle.checked = false;
        toggleBillboard(); // Apply the restored state
    }

    // SignalR connection for real-time updates
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/status")
        .build();

    // Store connection globally for work order change handling
    window.assemblyConnection = connection;

    // Start SignalR connection
    connection.start().then(function () {
        // Join assembly station group
        connection.invoke("JoinGroup", "assembly-station");
        
        // Join all-stations group to receive product assembly broadcasts
        connection.invoke("JoinGroup", "all-stations");
        
        // Join work order group for notifications
        const activeWorkOrderIdElement = document.querySelector('[data-active-work-order-id]');
        const activeWorkOrderId = activeWorkOrderIdElement ? activeWorkOrderIdElement.dataset.activeWorkOrderId : null;
        if (activeWorkOrderId) {
            connection.invoke("JoinWorkOrderGroup", activeWorkOrderId);
            window.currentWorkOrderId = activeWorkOrderId;
        }
    }).catch(function (err) {
        console.error("Error starting SignalR connection:", err);
    });

    // Listen for product assembly notifications
    connection.on("ProductAssembled", function (data) {
        showToast(`Product "${data.ProductName}" has been assembled!`, 'success');
        showBillboard('assembly-billboard', `Product "${data.ProductName}" has been assembled!`, 'success', 'Assembly Complete');
        updateProductStatus(data.ProductId, 'completed');
        clearProductBinLocations(data.ProductId); // Clear bin locations immediately
        updateShippingReadinessStatus(data);
    });

    // Listen for barcode scan assembly notifications
    connection.on("ProductAssembledByScan", function (data) {
        showToast(`🔧 Product "${data.ProductName}" assembled via scan!`, 'success');
        showBillboard('assembly-billboard', `Product "${data.ProductName}" assembled via scan!`, 'success', 'Scan Assembly Complete');
        updateProductStatus(data.ProductId, 'completed');
        clearProductBinLocations(data.ProductId); // Clear bin locations immediately
        updateShippingReadinessStatus(data);
        
        // Refresh the page to show updated assembly queue
        setTimeout(() => {
            location.reload();
        }, 2000);
    });

    // Listen for new products ready for assembly (from sorting station)
    connection.on("ProductReadyForAssembly", function (data) {
        showToast(`Product "${data.productName}" is now ready for assembly!`, 'info');
        showBillboard('assembly-billboard', `Product "${data.productName}" is now ready for assembly!`, 'info', 'Ready for Assembly');
        updateProductStatus(data.productId, 'ready');
    });

    // Listen for products assembled via manual button
    connection.on("ProductAssembledManually", function (data) {
        // showToast(`Product "${data.productName}" assembled successfully!`, 'success');
        showBillboard('assembly-billboard', `Product "${data.productName}" assembled successfully!`, 'success', 'Manual Assembly Complete');
        updateProductStatus(data.productId, 'completed');
        clearProductBinLocations(data.productId); // Clear bin locations immediately
        updateShippingReadinessStatus(data);
    });

    // Listen for general status updates
    connection.on("StatusUpdate", function (data) {
        if (data.type === "product-assembled") {
            updateProductStatus(data.productId, 'completed');
            updateShippingReadinessStatus(data);
            showStationNotification(data.station, data.message);
        }
    });
});