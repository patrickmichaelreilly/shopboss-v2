// Timeline Management JavaScript
// Extracted from project-management.js to reduce file size and improve maintainability

// Load timeline for a project
function loadTimelineForProject(projectId) {
    const timelineContainer = document.getElementById(`timeline-container-${projectId}`);
    if (!timelineContainer) return;
    
    fetch(`/Timeline/Get?projectId=${projectId}`)
        .then(response => {
            if (!response.ok) {
                throw new Error('Failed to load timeline');
            }
            return response.text();
        })
        .then(html => {
            timelineContainer.innerHTML = html;
            // Initialize interactions after HTML is loaded
            setTimeout(() => initializeTimelineInteractions(projectId), 100);
        })
        .catch(error => {
            console.error('Error loading timeline:', error);
            timelineContainer.innerHTML = `
                <div class="text-center text-danger py-3">
                    <i class="fas fa-exclamation-triangle me-1"></i>
                    Error loading timeline
                </div>
            `;
        });
}

// Initialize event handlers for timeline interactions
function initializeTimelineInteractions(projectId) {
    // Event selection handling
    const timelineContainer = document.getElementById(`timeline-container-${projectId}`);
    if (!timelineContainer) {
        return;
    }
    
    const eventSelectors = timelineContainer.querySelectorAll('.event-selector');
    
    eventSelectors.forEach(checkbox => {
        // Remove any existing listeners to avoid duplicates
        checkbox.removeEventListener('change', handleEventSelectionChange);
        // Add new listener
        checkbox.addEventListener('change', () => updateBulkActionsVisibility(projectId));
    });
    
    // Initial update of bulk actions visibility
    updateBulkActionsVisibility(projectId);
}

// Handler function for event selection changes
function handleEventSelectionChange(event) {
    // This function is for removing duplicate listeners
}

// Update bulk actions visibility based on selected events
function updateBulkActionsVisibility(projectId) {
    const selectedCheckboxes = document.querySelectorAll(`#timeline-container-${projectId} .event-selector:checked`);
    const bulkActions = document.getElementById(`bulk-actions-${projectId}`);
    const selectedCount = document.getElementById(`selected-count-${projectId}`);
    
    if (bulkActions) {
        if (selectedCheckboxes.length > 0) {
            bulkActions.classList.remove('d-none');
            if (selectedCount) {
                selectedCount.textContent = selectedCheckboxes.length;
            }
        } else {
            bulkActions.classList.add('d-none');
        }
    }
}

// Clear event selection
function clearSelection(projectId) {
    const checkboxes = document.querySelectorAll(`#timeline-container-${projectId} .event-selector`);
    checkboxes.forEach(cb => cb.checked = false);
    updateBulkActionsVisibility(projectId);
}

// Show create TaskBlock dialog
function showCreateTaskBlock(projectId) {
    const blockName = prompt('Enter name for new Task Block:');
    if (blockName && blockName.trim()) {
        createTaskBlock(projectId, blockName.trim());
    }
}

// Create a new TaskBlock
function createTaskBlock(projectId, name, description = null) {
    const requestData = {
        ProjectId: projectId,
        Name: name,
        Description: description
    };
    
    fetch('/Timeline/CreateBlock', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestData)
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification('Task block created successfully', 'success');
            loadTimelineForProject(projectId);
        } else {
            showNotification(data.message || 'Error creating task block', 'error');
        }
    })
    .catch(error => {
        console.error('Error creating task block:', error);
        showNotification('Network error occurred', 'error');
    });
}

// Edit TaskBlock
function editTaskBlock(blockId, currentName, currentDescription) {
    const newName = prompt('Enter new name for Task Block:', currentName);
    if (newName && newName.trim() && newName.trim() !== currentName) {
        const newDescription = prompt('Enter description (optional):', currentDescription || '');
        updateTaskBlock(blockId, newName.trim(), newDescription);
    }
}

// Update TaskBlock
function updateTaskBlock(blockId, name, description) {
    const requestData = {
        BlockId: blockId,
        Name: name,
        Description: description
    };
    
    fetch('/Timeline/UpdateBlock', {
        method: 'PUT',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestData)
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification('Task block updated successfully', 'success');
            // Find the project ID from the block element and reload timeline
            const blockElement = document.querySelector(`[data-block-id="${blockId}"]`);
            if (blockElement) {
                const timelineContainer = blockElement.closest('[id^="timeline-container-"]');
                if (timelineContainer) {
                    const projectId = timelineContainer.id.replace('timeline-container-', '');
                    loadTimelineForProject(projectId);
                }
            }
        } else {
            showNotification(data.message || 'Error updating task block', 'error');
        }
    })
    .catch(error => {
        console.error('Error updating task block:', error);
        showNotification('Network error occurred', 'error');
    });
}

// Delete TaskBlock
function deleteTaskBlock(blockId) {
    if (!confirm('Are you sure you want to delete this Task Block? Events will be moved back to the unblocked section.')) {
        return;
    }
    
    fetch(`/Timeline/DeleteBlock?blockId=${blockId}`, {
        method: 'DELETE'
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification('Task block deleted successfully', 'success');
            // Find the project ID and reload timeline
            const blockElement = document.querySelector(`[data-block-id="${blockId}"]`);
            if (blockElement) {
                const timelineContainer = blockElement.closest('[id^="timeline-container-"]');
                if (timelineContainer) {
                    const projectId = timelineContainer.id.replace('timeline-container-', '');
                    loadTimelineForProject(projectId);
                }
            }
        } else {
            showNotification(data.message || 'Error deleting task block', 'error');
        }
    })
    .catch(error => {
        console.error('Error deleting task block:', error);
        showNotification('Network error occurred', 'error');
    });
}

// Assign selected events to new block
function assignSelectedToNewBlock(projectId) {
    const selectedEventIds = getSelectedEventIds(projectId);
    if (selectedEventIds.length === 0) {
        showNotification('Please select at least one event', 'error');
        return;
    }
    
    const blockName = prompt('Enter name for new Task Block:');
    if (blockName && blockName.trim()) {
        // First create the block
        const requestData = {
            ProjectId: projectId,
            Name: blockName.trim()
        };
        
        fetch('/Timeline/CreateBlock', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(requestData)
        })
        .then(response => response.json())
        .then(data => {
            if (data.success && data.block) {
                // Now assign events to the block
                return assignEventsToBlock(data.block.id, selectedEventIds);
            } else {
                throw new Error(data.message || 'Error creating task block');
            }
        })
        .then(() => {
            showNotification('Task block created and events assigned successfully', 'success');
            loadTimelineForProject(projectId);
        })
        .catch(error => {
            console.error('Error creating block and assigning events:', error);
            showNotification(error.message || 'Network error occurred', 'error');
        });
    }
}

// Show dialog to assign to existing block
function showAssignToExistingBlock(projectId) {
    const selectedEventIds = getSelectedEventIds(projectId);
    if (selectedEventIds.length === 0) {
        showNotification('Please select at least one event', 'error');
        return;
    }
    
    // For now, use a simple approach - in the future we could create a proper modal
    const blocks = document.querySelectorAll(`#timeline-container-${projectId} .task-block`);
    if (blocks.length === 0) {
        showNotification('No existing blocks found. Create a new block instead.', 'error');
        return;
    }
    
    let blockOptions = '';
    blocks.forEach((block, index) => {
        const blockName = block.querySelector('.task-block-header h6').textContent.trim();
        const blockId = block.dataset.blockId;
        blockOptions += `${index + 1}. ${blockName} (ID: ${blockId})\n`;
    });
    
    const selection = prompt(`Select a block by entering its number:\n\n${blockOptions}`);
    const blockIndex = parseInt(selection) - 1;
    
    if (blockIndex >= 0 && blockIndex < blocks.length) {
        const selectedBlock = blocks[blockIndex];
        const blockId = selectedBlock.dataset.blockId;
        
        assignEventsToBlock(blockId, selectedEventIds)
            .then(() => {
                showNotification('Events assigned to block successfully', 'success');
                loadTimelineForProject(projectId);
            })
            .catch(error => {
                console.error('Error assigning events to block:', error);
                showNotification('Network error occurred', 'error');
            });
    }
}

// Assign events to a block
function assignEventsToBlock(blockId, eventIds) {
    const requestData = {
        BlockId: blockId,
        EventIds: eventIds
    };
    
    return fetch('/Timeline/AssignEvents', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestData)
    })
    .then(response => response.json())
    .then(data => {
        if (!data.success) {
            throw new Error(data.message || 'Error assigning events');
        }
        return data;
    });
}

// Unassign event from block
function unassignEventFromBlock(eventId) {
    const requestData = {
        EventIds: [eventId]
    };
    
    fetch('/Timeline/UnassignEvents', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestData)
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification('Event removed from block successfully', 'success');
            // Find the project ID and reload timeline
            const eventElement = document.querySelector(`[data-event-id="${eventId}"]`);
            if (eventElement) {
                const timelineContainer = eventElement.closest('[id^="timeline-container-"]');
                if (timelineContainer) {
                    const projectId = timelineContainer.id.replace('timeline-container-', '');
                    loadTimelineForProject(projectId);
                }
            }
        } else {
            showNotification(data.message || 'Error removing event from block', 'error');
        }
    })
    .catch(error => {
        console.error('Error unassigning event from block:', error);
        showNotification('Network error occurred', 'error');
    });
}

// Get selected event IDs
function getSelectedEventIds(projectId) {
    const selectedCheckboxes = document.querySelectorAll(`#timeline-container-${projectId} .event-selector:checked`);
    return Array.from(selectedCheckboxes).map(cb => cb.value);
}

// File management functions (moved from project-management.js)

// Direct file upload without filename preview
function uploadFilesDirectly(projectId, fileInput) {
    if (fileInput.files.length === 0) {
        return;
    }

    const formData = new FormData();
    formData.append('projectId', projectId);
    formData.append('category', 'Other'); // Auto-assign to 'Other' category
    
    for (let i = 0; i < fileInput.files.length; i++) {
        formData.append('file', fileInput.files[i]);
    }

    fetch('/Project/UploadFile', {
        method: 'POST',
        body: formData
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification('File uploaded successfully', 'success');
            fileInput.value = ''; // Clear the file input
            
            // Refresh timeline to show the new attachment event
            loadTimelineForProject(projectId);
        } else {
            showNotification(data.message || 'Error uploading files', 'error');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showNotification('Network error occurred', 'error');
    });
}

// Update file category
function updateFileCategory(fileId, category) {
    fetch('/Project/UpdateFileCategory', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ id: fileId, category: category })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification('Category updated', 'success');
        } else {
            showNotification(data.message || 'Error updating category', 'error');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showNotification('Network error occurred', 'error');
    });
}

// Delete file
function deleteFile(fileId, projectId) {
    fetch('/Project/DeleteFile', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: `id=${fileId}`
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification('File deleted successfully', 'success');
            
            // Refresh timeline to show the file deletion event
            loadTimelineForProject(projectId);
        } else {
            showNotification(data.message || 'Error deleting file', 'error');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showNotification('Network error occurred', 'error');
    });
}

// Purchase Order management functions (moved from project-management.js)

let currentPurchaseOrderId = null;

function showCreatePurchaseOrder(projectId) {
    currentProjectId = projectId;
    
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
}

function editPurchaseOrder(purchaseOrderId, projectId) {
    currentPurchaseOrderId = purchaseOrderId;
    currentProjectId = projectId;
    
    // Load purchase order data
    fetch(`/Project/GetPurchaseOrder?id=${purchaseOrderId}`)
        .then(response => response.json())
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
}

function savePurchaseOrder() {
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

    fetch('/Project/CreatePurchaseOrder', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(purchaseOrder)
    })
    .then(response => response.json())
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
}

function savePurchaseOrderEdit() {
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

    fetch('/Project/UpdatePurchaseOrder', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(purchaseOrder)
    })
    .then(response => response.json())
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
}

function deletePurchaseOrder(purchaseOrderId, projectId) {
    if (confirm('Are you sure you want to delete this purchase order?')) {
        fetch('/Project/DeletePurchaseOrder', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: `id=${purchaseOrderId}`
        })
        .then(response => response.json())
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
}

// ==================== WORK ORDER FUNCTIONS ====================

function showAssociateWorkOrders(projectId) {
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
}

function associateSelectedWorkOrders() {
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
}

function detachWorkOrder(workOrderId, projectId) {
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
}

function showCreateCustomWorkOrder(projectId) {
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
}

function editCustomWorkOrder(customWorkOrderId, projectId) {
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
}

function saveCustomWorkOrder() {
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
}

function saveCustomWorkOrderEdit() {
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
}

function deleteCustomWorkOrder(customWorkOrderId, projectId) {
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
}