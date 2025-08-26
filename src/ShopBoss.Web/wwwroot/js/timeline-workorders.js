// Timeline Work Orders Management Module
// Extracted from timeline.js for better maintainability

(function(Timeline) {
    Timeline.WorkOrders = Timeline.WorkOrders || {};

    let currentCustomWorkOrderId = null;

    // ==================== REGULAR WORK ORDER FUNCTIONS ====================

    Timeline.WorkOrders.showAssociateWorkOrders = function(projectId) {
        currentProjectId = projectId;
        
        // Fetch fresh unassigned work orders
        fetch('/Project/GetUnassignedWorkOrders')
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const unassignedList = document.getElementById('unassignedWorkOrdersList');
                
                // Clear existing content
                unassignedList.innerHTML = '';
                
                if (data.workOrders && data.workOrders.length > 0) {
                    // Populate with fresh work orders
                    data.workOrders.forEach(workOrder => {
                        const workOrderDiv = document.createElement('div');
                        workOrderDiv.className = 'form-check';
                        workOrderDiv.innerHTML = `
                            <input class="form-check-input" type="checkbox" value="${workOrder.id}" id="wo-${workOrder.id}">
                            <label class="form-check-label" for="wo-${workOrder.id}">
                                <strong>${workOrder.name}</strong>
                                <br><small class="text-muted">Imported: ${new Date(workOrder.importedDate).toLocaleDateString('en-US', {month: '2-digit', day: '2-digit', year: '2-digit'})}</small>
                            </label>
                        `;
                        unassignedList.appendChild(workOrderDiv);
                    });
                } else {
                    // No unassigned work orders
                    unassignedList.innerHTML = '<p class="text-muted">No unassigned work orders available</p>';
                }
                
                // Show the modal
                const modal = new bootstrap.Modal(document.getElementById('associateWorkOrdersModal'));
                modal.show();
            } else {
                showNotification(data.message || 'Error loading unassigned work orders', 'error');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showNotification('Network error occurred', 'error');
        });
    };

    Timeline.WorkOrders.associateSelectedWorkOrders = function() {
        const checkboxes = document.querySelectorAll('#unassignedWorkOrdersList input[type="checkbox"]:checked');
        const workOrderIds = Array.from(checkboxes).map(cb => cb.value);
        
        if (workOrderIds.length === 0) {
            showNotification('Please select at least one work order', 'error');
            return;
        }

        const request = {
            ProjectId: currentProjectId,
            WorkOrderIds: workOrderIds
        };

        fetch('/Project/AttachWorkOrders', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(request)
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                bootstrap.Modal.getInstance(document.getElementById('associateWorkOrdersModal')).hide();
                showNotification('Work orders associated successfully', 'success');
                
                // Refresh timeline to show new work order events
                loadTimelineForProject(currentProjectId);
                
                // Clear selections
                checkboxes.forEach(cb => cb.checked = false);
            } else {
                showNotification(data.message || 'Error associating work orders', 'error');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showNotification('Network error occurred', 'error');
        });
    };

    Timeline.WorkOrders.detachWorkOrder = function(workOrderId, projectId) {
        if (confirm('Are you sure you want to detach this work order from the project?')) {
            fetch('/Project/DetachWorkOrder', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: `workOrderId=${workOrderId}`
            })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    showNotification('Work order detached successfully', 'success');
                    
                    // Refresh timeline to remove the work order event
                    loadTimelineForProject(projectId);
                } else {
                    showNotification(data.message || 'Error detaching work order', 'error');
                }
            })
            .catch(error => {
                console.error('Error:', error);
                showNotification('Network error occurred', 'error');
            });
        }
    };

    // ==================== CUSTOM WORK ORDER FUNCTIONS ====================

    Timeline.WorkOrders.showCreateCustomWorkOrder = function(projectId) {
        currentProjectId = projectId;
        
        // Clear the form
        const form = document.getElementById('customWorkOrderForm');
        if (form) {
            form.reset();
            form.querySelector('input[name="ProjectId"]').value = projectId;
            form.querySelector('input[name="Id"]').value = '';
        }
        
        const modal = new bootstrap.Modal(document.getElementById('createCustomWorkOrderModal'));
        modal.show();
    };

    Timeline.WorkOrders.editCustomWorkOrder = function(customWorkOrderId, projectId) {
        currentCustomWorkOrderId = customWorkOrderId;
        currentProjectId = projectId;
        
        // Load custom work order data
        fetch(`/Project/GetCustomWorkOrder?id=${customWorkOrderId}`)
            .then(response => response.json())
            .then(data => {
                if (data.success && data.customWorkOrder) {
                    const cwo = data.customWorkOrder;
                    const form = document.getElementById('editCustomWorkOrderModal').querySelector('#customWorkOrderForm');
                    
                    // Populate form fields
                    form.querySelector('input[name="Id"]').value = cwo.id;
                    form.querySelector('input[name="ProjectId"]').value = cwo.projectId;
                    form.querySelector('input[name="Name"]').value = cwo.name || '';
                    form.querySelector('select[name="WorkOrderType"]').value = cwo.workOrderType || 0;
                    form.querySelector('textarea[name="Description"]').value = cwo.description || '';
                    form.querySelector('input[name="AssignedTo"]').value = cwo.assignedTo || '';
                    form.querySelector('input[name="EstimatedHours"]').value = cwo.estimatedHours || '';
                    form.querySelector('input[name="ActualHours"]').value = cwo.actualHours || '';
                    form.querySelector('select[name="Status"]').value = cwo.status || 0;
                    form.querySelector('input[name="StartDate"]').value = cwo.startDate ? cwo.startDate.split('T')[0] : '';
                    form.querySelector('input[name="CompletedDate"]').value = cwo.completedDate ? cwo.completedDate.split('T')[0] : '';
                    form.querySelector('textarea[name="Notes"]').value = cwo.notes || '';
                    
                    const modal = new bootstrap.Modal(document.getElementById('editCustomWorkOrderModal'));
                    modal.show();
                } else {
                    showNotification('Error loading custom work order data', 'error');
                }
            })
            .catch(error => {
                console.error('Error:', error);
                showNotification('Network error occurred', 'error');
            });
    };

    Timeline.WorkOrders.saveCustomWorkOrder = function() {
        const form = document.getElementById('customWorkOrderForm');
        const formData = new FormData(form);
        
        const customWorkOrder = {
            Id: formData.get('Id') || '',
            ProjectId: formData.get('ProjectId'),
            Name: formData.get('Name'),
            WorkOrderType: parseInt(formData.get('WorkOrderType')) || 0,
            Description: formData.get('Description'),
            AssignedTo: formData.get('AssignedTo') || null,
            EstimatedHours: formData.get('EstimatedHours') ? parseFloat(formData.get('EstimatedHours')) : null,
            ActualHours: formData.get('ActualHours') ? parseFloat(formData.get('ActualHours')) : null,
            Status: parseInt(formData.get('Status')) || 0,
            StartDate: formData.get('StartDate') || null,
            CompletedDate: formData.get('CompletedDate') || null,
            Notes: formData.get('Notes') || null
        };

        fetch('/Project/CreateCustomWorkOrder', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(customWorkOrder)
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showNotification('Custom work order created successfully', 'success');
                bootstrap.Modal.getInstance(document.getElementById('createCustomWorkOrderModal')).hide();
                
                // Refresh timeline to show new custom work order event
                loadTimelineForProject(currentProjectId);
            } else {
                showNotification(data.message || 'Error creating custom work order', 'error');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showNotification('Network error occurred', 'error');
        });
    };

    Timeline.WorkOrders.saveCustomWorkOrderEdit = function() {
        const form = document.getElementById('editCustomWorkOrderModal').querySelector('#customWorkOrderForm');
        const formData = new FormData(form);
        
        const customWorkOrder = {
            Id: currentCustomWorkOrderId,
            ProjectId: currentProjectId,
            Name: formData.get('Name'),
            WorkOrderType: parseInt(formData.get('WorkOrderType')) || 0,
            Description: formData.get('Description'),
            AssignedTo: formData.get('AssignedTo') || null,
            EstimatedHours: formData.get('EstimatedHours') ? parseFloat(formData.get('EstimatedHours')) : null,
            ActualHours: formData.get('ActualHours') ? parseFloat(formData.get('ActualHours')) : null,
            Status: parseInt(formData.get('Status')) || 0,
            StartDate: formData.get('StartDate') || null,
            CompletedDate: formData.get('CompletedDate') || null,
            Notes: formData.get('Notes') || null
        };

        fetch('/Project/UpdateCustomWorkOrder', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(customWorkOrder)
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showNotification('Custom work order updated successfully', 'success');
                bootstrap.Modal.getInstance(document.getElementById('editCustomWorkOrderModal')).hide();
                
                // Refresh timeline to show updated custom work order event
                loadTimelineForProject(currentProjectId);
            } else {
                showNotification(data.message || 'Error updating custom work order', 'error');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showNotification('Network error occurred', 'error');
        });
    };

    Timeline.WorkOrders.deleteCustomWorkOrder = function(customWorkOrderId, projectId) {
        if (confirm('Are you sure you want to delete this custom work order?')) {
            fetch('/Project/DeleteCustomWorkOrder', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: `id=${customWorkOrderId}`
            })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    showNotification('Custom work order deleted successfully', 'success');
                    
                    // Refresh timeline to remove the custom work order event
                    loadTimelineForProject(projectId);
                } else {
                    showNotification(data.message || 'Error deleting custom work order', 'error');
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
function showAssociateWorkOrders(projectId) {
    return Timeline.WorkOrders.showAssociateWorkOrders(projectId);
}

function associateSelectedWorkOrders() {
    return Timeline.WorkOrders.associateSelectedWorkOrders();
}

function detachWorkOrder(workOrderId, projectId) {
    return Timeline.WorkOrders.detachWorkOrder(workOrderId, projectId);
}

function showCreateCustomWorkOrder(projectId) {
    return Timeline.WorkOrders.showCreateCustomWorkOrder(projectId);
}

function editCustomWorkOrder(customWorkOrderId, projectId) {
    return Timeline.WorkOrders.editCustomWorkOrder(customWorkOrderId, projectId);
}

function saveCustomWorkOrder() {
    return Timeline.WorkOrders.saveCustomWorkOrder();
}

function saveCustomWorkOrderEdit() {
    return Timeline.WorkOrders.saveCustomWorkOrderEdit();
}

function deleteCustomWorkOrder(customWorkOrderId, projectId) {
    return Timeline.WorkOrders.deleteCustomWorkOrder(customWorkOrderId, projectId);
}