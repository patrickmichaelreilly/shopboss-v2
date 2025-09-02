// Timeline Purchase Orders Management Module
// Extracted from timeline.js for better maintainability

(function(Timeline) {
    Timeline.Purchases = Timeline.Purchases || {};

    let currentPurchaseOrderId = null;

    Timeline.Purchases.currentBlockId = null; // Store blockId for purchase order context
    
    Timeline.Purchases.showCreatePurchaseOrder = function(projectId, blockId = null) {
        currentProjectId = projectId;
        Timeline.Purchases.currentBlockId = blockId;
        
        // Clear the form
        const form = document.getElementById('purchaseOrderForm');
        if (form) {
            form.reset();
            form.querySelector('input[name="ProjectId"]').value = projectId;
            form.querySelector('input[name="Id"]').value = '';
            form.querySelector('input[name="OrderDate"]').value = new Date().toISOString().split('T')[0];
        }
        
        const modal = new bootstrap.Modal(document.getElementById('createPurchaseOrderModal'));
        modal.show();
    };

    Timeline.Purchases.editPurchaseOrder = function(purchaseOrderId, projectId) {
        currentPurchaseOrderId = purchaseOrderId;
        currentProjectId = projectId;
        
        // Load purchase order data
        (typeof apiGetJson === 'function' ? apiGetJson(`/Project/GetPurchaseOrder?id=${purchaseOrderId}`) : fetch(`/Project/GetPurchaseOrder?id=${purchaseOrderId}`).then(r => r.json()))
            .then(data => {
                if (data.success && data.purchaseOrder) {
                    const po = data.purchaseOrder;
                    const form = document.getElementById('editPurchaseOrderModal').querySelector('#purchaseOrderForm');
                    
                    // Populate form fields
                    form.querySelector('input[name="Id"]').value = po.id;
                    form.querySelector('input[name="ProjectId"]').value = po.projectId;
                    form.querySelector('input[name="PurchaseOrderNumber"]').value = po.purchaseOrderNumber || '';
                    form.querySelector('input[name="VendorName"]').value = po.vendorName || '';
                    form.querySelector('input[name="VendorContact"]').value = po.vendorContact || '';
                    form.querySelector('input[name="VendorPhone"]').value = po.vendorPhone || '';
                    form.querySelector('input[name="VendorEmail"]').value = po.vendorEmail || '';
                    form.querySelector('textarea[name="Description"]').value = po.description || '';
                    form.querySelector('input[name="OrderDate"]').value = po.orderDate ? po.orderDate.split('T')[0] : '';
                    form.querySelector('input[name="ExpectedDeliveryDate"]').value = po.expectedDeliveryDate ? po.expectedDeliveryDate.split('T')[0] : '';
                    form.querySelector('input[name="ActualDeliveryDate"]').value = po.actualDeliveryDate ? po.actualDeliveryDate.split('T')[0] : '';
                    form.querySelector('input[name="TotalAmount"]').value = po.totalAmount || '';
                    form.querySelector('select[name="Status"]').value = po.status || 0;
                    form.querySelector('textarea[name="Notes"]').value = po.notes || '';
                    
                    const modal = new bootstrap.Modal(document.getElementById('editPurchaseOrderModal'));
                    modal.show();
                } else {
                    showNotification('Error loading purchase order data', 'error');
                }
            })
            .catch(error => {
                console.error('Error:', error);
                showNotification('Network error occurred', 'error');
            });
    };

    Timeline.Purchases.savePurchaseOrder = function() {
        const form = document.getElementById('purchaseOrderForm');
        const formData = new FormData(form);
        
        const purchaseOrder = {
            Id: formData.get('Id') || '',
            ProjectId: formData.get('ProjectId'),
            PurchaseOrderNumber: formData.get('PurchaseOrderNumber'),
            VendorName: formData.get('VendorName'),
            VendorContact: formData.get('VendorContact') || null,
            VendorPhone: formData.get('VendorPhone') || null,
            VendorEmail: formData.get('VendorEmail') || null,
            Description: formData.get('Description'),
            OrderDate: formData.get('OrderDate'),
            ExpectedDeliveryDate: formData.get('ExpectedDeliveryDate') || null,
            ActualDeliveryDate: formData.get('ActualDeliveryDate') || null,
            TotalAmount: formData.get('TotalAmount') ? parseFloat(formData.get('TotalAmount')) : null,
            Status: parseInt(formData.get('Status')) || 0,
            Notes: formData.get('Notes') || null
        };

        const requestData = {
            PurchaseOrder: purchaseOrder,
            TaskBlockId: Timeline.Purchases.currentBlockId
        };

        (typeof apiPostJson === 'function' ? apiPostJson('/Project/CreatePurchaseOrder', requestData) : fetch('/Project/CreatePurchaseOrder', { method: 'POST', headers: { 'Content-Type': 'application/json', }, body: JSON.stringify(requestData) }).then(r => r.json()))
        .then(data => {
            if (data.success) {
                showNotification('Purchase order created successfully', 'success');
                bootstrap.Modal.getInstance(document.getElementById('createPurchaseOrderModal')).hide();
                
                // Refresh timeline to show the new purchase order event
                loadTimelineForProject(currentProjectId);
            } else {
                showNotification(data.message || 'Error creating purchase order', 'error');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showNotification('Network error occurred', 'error');
        });
    };

    Timeline.Purchases.savePurchaseOrderEdit = function() {
        const form = document.getElementById('editPurchaseOrderModal').querySelector('#purchaseOrderForm');
        const formData = new FormData(form);
        
        const purchaseOrder = {
            Id: currentPurchaseOrderId,
            ProjectId: currentProjectId,
            PurchaseOrderNumber: formData.get('PurchaseOrderNumber'),
            VendorName: formData.get('VendorName'),
            VendorContact: formData.get('VendorContact') || null,
            VendorPhone: formData.get('VendorPhone') || null,
            VendorEmail: formData.get('VendorEmail') || null,
            Description: formData.get('Description'),
            OrderDate: formData.get('OrderDate'),
            ExpectedDeliveryDate: formData.get('ExpectedDeliveryDate') || null,
            ActualDeliveryDate: formData.get('ActualDeliveryDate') || null,
            TotalAmount: formData.get('TotalAmount') ? parseFloat(formData.get('TotalAmount')) : null,
            Status: parseInt(formData.get('Status')) || 0,
            Notes: formData.get('Notes') || null
        };

        (typeof apiPostJson === 'function' ? apiPostJson('/Project/UpdatePurchaseOrder', purchaseOrder) : fetch('/Project/UpdatePurchaseOrder', { method: 'POST', headers: { 'Content-Type': 'application/json', }, body: JSON.stringify(purchaseOrder) }).then(r => r.json()))
        .then(data => {
            if (data.success) {
                showNotification('Purchase order updated successfully', 'success');
                bootstrap.Modal.getInstance(document.getElementById('editPurchaseOrderModal')).hide();
                
                // Refresh timeline to show the updated purchase order event
                loadTimelineForProject(currentProjectId);
            } else {
                showNotification(data.message || 'Error updating purchase order', 'error');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showNotification('Network error occurred', 'error');
        });
    };

    Timeline.Purchases.deletePurchaseOrder = function(purchaseOrderId, projectId) {
        if (confirm('Are you sure you want to delete this purchase order?')) {
            (typeof apiPostForm === 'function' ? apiPostForm('/Project/DeletePurchaseOrder', new URLSearchParams({ id: purchaseOrderId })) : fetch('/Project/DeletePurchaseOrder', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: `id=${purchaseOrderId}` }).then(r => r.json()))
            .then(data => {
                if (data.success) {
                    showNotification('Purchase order deleted successfully', 'success');
                    
                    // Refresh timeline to show the purchase order deletion event
                    loadTimelineForProject(projectId);
                } else {
                    showNotification(data.message || 'Error deleting purchase order', 'error');
                }
            })
            .catch(error => {
                console.error('Error:', error);
                showNotification('Network error occurred', 'error');
            });
        }
    };

})(window.Timeline = window.Timeline || {});

// Backward compatibility - expose functions globally for existing code
function showCreatePurchaseOrder(projectId) {
    return Timeline.Purchases.showCreatePurchaseOrder(projectId);
}

function editPurchaseOrder(purchaseOrderId, projectId) {
    return Timeline.Purchases.editPurchaseOrder(purchaseOrderId, projectId);
}

function savePurchaseOrder() {
    return Timeline.Purchases.savePurchaseOrder();
}

function savePurchaseOrderEdit() {
    return Timeline.Purchases.savePurchaseOrderEdit();
}

function deletePurchaseOrder(purchaseOrderId, projectId) {
    return Timeline.Purchases.deletePurchaseOrder(purchaseOrderId, projectId);
}
