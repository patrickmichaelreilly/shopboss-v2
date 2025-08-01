// Project Management JavaScript
let currentProjectId = null;

// Toggle project expansion
function toggleProject(projectId) {
    const details = document.getElementById(`details-${projectId}`);
    const icon = document.getElementById(`expand-icon-${projectId}`);
    const table = document.getElementById('projects-table');
    
    if (details.classList.contains('d-none')) {
        details.classList.remove('d-none');
        icon.classList.remove('fa-chevron-right');
        icon.classList.add('fa-chevron-down');
        
        // Remove hover effect from table when any project is expanded
        table.classList.remove('table-hover');
    } else {
        details.classList.add('d-none');
        icon.classList.remove('fa-chevron-down');
        icon.classList.add('fa-chevron-right');
        
        // Check if any other projects are still expanded
        const anyExpanded = document.querySelector('tr[id^="details-"]:not(.d-none)');
        if (!anyExpanded) {
            // Re-add hover effect if no projects are expanded
            table.classList.add('table-hover');
        }
    }
}

// Filter functions
function toggleArchiveFilter() {
    const checkbox = document.getElementById('includeArchivedToggle');
    const url = new URL(window.location);
    url.searchParams.set('includeArchived', checkbox.checked);
    window.location.href = url.toString();
}

function filterByCategory(category) {
    const url = new URL(window.location);
    if (category) {
        url.searchParams.set('projectCategory', category);
    } else {
        url.searchParams.delete('projectCategory');
    }
    window.location.href = url.toString();
}

// Clear project form
function clearProjectForm() {
    const form = document.getElementById('createProjectForm');
    if (form) {
        form.reset();
    }
}

// Project CRUD operations
function saveProject() {
    const form = document.getElementById('createProjectForm');
    const formData = new FormData(form);
    
    const projectCategoryValue = formData.get('ProjectCategory');
    const project = {
        Id: formData.get('Id') || '',
        ProjectId: formData.get('ProjectId'),
        ProjectName: formData.get('ProjectName'),
        ProjectCategory: projectCategoryValue ? parseInt(projectCategoryValue) : 0,
        BidRequestDate: formData.get('BidRequestDate') || null,
        ProjectAddress: formData.get('ProjectAddress') || null,
        ProjectContact: formData.get('ProjectContact') || null,
        ProjectContactPhone: formData.get('ProjectContactPhone') || null,
        ProjectContactEmail: formData.get('ProjectContactEmail') || null,
        GeneralContractor: formData.get('GeneralContractor') || null,
        ProjectManager: formData.get('ProjectManager') || null,
        TargetInstallDate: formData.get('TargetInstallDate') || null,
        Installer: formData.get('Installer') || null,
        Notes: formData.get('Notes') || null
    };

    fetch('/Project/Create', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(project)
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            // Clear the form
            document.getElementById('createProjectForm').reset();
            // Hide the modal
            bootstrap.Modal.getInstance(document.getElementById('createProjectModal')).hide();
            location.reload();
        } else {
            showNotification(data.message || 'Error creating project', 'error');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showNotification('Network error occurred', 'error');
    });
}

function editProject(projectId) {
    const displayContent = document.getElementById(`project-display-${projectId}`);
    const editContent = document.getElementById(`project-edit-${projectId}`);
    const displayButtons = document.getElementById(`display-buttons-${projectId}`);
    const editButtons = document.getElementById(`edit-buttons-${projectId}`);
    
    // Hide display, show edit
    displayContent.classList.add('d-none');
    editContent.classList.remove('d-none');
    
    // Switch buttons
    displayButtons.classList.add('d-none');
    editButtons.classList.remove('d-none');
}

function cancelProjectEdit(projectId) {
    const displayContent = document.getElementById(`project-display-${projectId}`);
    const editContent = document.getElementById(`project-edit-${projectId}`);
    const displayButtons = document.getElementById(`display-buttons-${projectId}`);
    const editButtons = document.getElementById(`edit-buttons-${projectId}`);
    
    // Hide edit, show display
    editContent.classList.add('d-none');
    displayContent.classList.remove('d-none');
    
    // Switch buttons
    editButtons.classList.add('d-none');
    displayButtons.classList.remove('d-none');
}

function saveProjectEdit(projectId) {
    const form = document.getElementById(`projectForm-${projectId}`);
    const formData = new FormData(form);
    
    const project = {
        Id: projectId,
        ProjectId: formData.get('ProjectId'),
        ProjectName: formData.get('ProjectName'),
        ProjectCategory: parseInt(formData.get('ProjectCategory')),
        BidRequestDate: formData.get('BidRequestDate') || null,
        ProjectAddress: formData.get('ProjectAddress') || null,
        ProjectContact: formData.get('ProjectContact') || null,
        ProjectContactPhone: formData.get('ProjectContactPhone') || null,
        ProjectContactEmail: formData.get('ProjectContactEmail') || null,
        GeneralContractor: formData.get('GeneralContractor') || null,
        ProjectManager: formData.get('ProjectManager') || null,
        TargetInstallDate: formData.get('TargetInstallDate') || null,
        Installer: formData.get('Installer') || null,
        Notes: formData.get('Notes') || null
    };

    fetch('/Project/Update', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(project)
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification('Project updated successfully', 'success');
            
            // Update the project name in the table row if it changed
            const projectRow = document.getElementById(`expand-icon-${projectId}`).closest('tr');
            const projectNameCell = projectRow.querySelector('td:nth-child(3) strong');
            if (projectNameCell) {
                projectNameCell.textContent = project.ProjectName;
            }
            
            // Update the display content with new values
            updateProjectDisplayContent(projectId, project);
            
            // Switch back to display mode
            cancelProjectEdit(projectId);
        } else {
            showNotification(data.message || 'Error updating project', 'error');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showNotification('Network error occurred', 'error');
    });
}

// Helper function to update the display content after save
function updateProjectDisplayContent(projectId, project) {
    const displayContainer = document.getElementById(`project-display-${projectId}`);
    const categoryBadges = {0: 'Standard Products', 1: 'Custom Products', 2: 'Small Project'};
    
    // Update the display table with new values
    displayContainer.innerHTML = `
        <table class="table table-sm table-borderless mb-0">
            <tbody>
                <!-- Left: Customer Info, Right: Internal Info -->
                <tr>
                    <td width="120" class="text-muted ps-3">Address:</td>
                    <td>${project.ProjectAddress || '-'}</td>
                    <td width="120" class="text-muted">Bid Date:</td>
                    <td>${project.BidRequestDate ? new Date(project.BidRequestDate).toLocaleDateString('en-US', {month: '2-digit', day: '2-digit', year: '2-digit'}) : '-'}</td>
                </tr>
                <tr>
                    <td class="text-muted ps-3">Contractor:</td>
                    <td>${project.GeneralContractor || '-'}</td>
                    <td class="text-muted">PM:</td>
                    <td>${project.ProjectManager || '-'}</td>
                </tr>
                <tr>
                    <td class="text-muted ps-3">Contact:</td>
                    <td>${project.ProjectContact || '-'}</td>
                    <td class="text-muted">Installer:</td>
                    <td>${project.Installer || '-'}</td>
                </tr>
                <tr>
                    <td class="text-muted ps-3">Email:</td>
                    <td>${project.ProjectContactEmail || '-'}</td>
                    <td class="text-muted">Category:</td>
                    <td><span class="badge bg-secondary">${categoryBadges[project.ProjectCategory] || 'Unknown'}</span></td>
                </tr>
                <tr>
                    <td class="text-muted ps-3">Phone:</td>
                    <td>${project.ProjectContactPhone || '-'}</td>
                    <td></td>
                    <td></td>
                </tr>
                
                ${project.Notes ? `
                <tr><td colspan="4" class="py-2"></td></tr>
                <tr>
                    <td class="text-muted align-top ps-3">Notes:</td>
                    <td colspan="3">${project.Notes}</td>
                </tr>
                ` : ''}
            </tbody>
        </table>
    `;
}

function archiveProject(projectId) {
    if (confirm('Are you sure you want to archive this project?')) {
        fetch('/Project/Archive', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: `id=${projectId}`
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                location.reload();
            } else {
                showNotification(data.message || 'Error archiving project', 'error');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showNotification('Network error occurred', 'error');
        });
    }
}

function unarchiveProject(projectId) {
    fetch('/Project/Unarchive', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: `id=${projectId}`
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            location.reload();
        } else {
            showNotification(data.message || 'Error unarchiving project', 'error');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showNotification('Network error occurred', 'error');
    });
}

function deleteProject(projectId, buttonElement) {
    const projectName = buttonElement.getAttribute('data-project-name');
    if (confirm(`Are you sure you want to delete project "${projectName}"? This action cannot be undone.`)) {
        fetch('/Project/Delete', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: `id=${projectId}`
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                location.reload();
            } else {
                showNotification(data.message || 'Error deleting project', 'error');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showNotification('Network error occurred', 'error');
        });
    }
}

// Work Order Association
function showAssociateWorkOrders(projectId) {
    currentProjectId = projectId;
    const modal = new bootstrap.Modal(document.getElementById('associateWorkOrdersModal'));
    modal.show();
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
            
            // Add work orders to DOM
            const workOrderContainer = document.getElementById(`work-orders-${currentProjectId}`);
            const noWoMsg = workOrderContainer.querySelector('.text-center.text-muted');
            if (noWoMsg) {
                noWoMsg.remove();
            }
            
            // Get selected work order details and add them
            checkboxes.forEach(checkbox => {
                const label = checkbox.nextElementSibling;
                const woName = label.querySelector('strong').textContent;
                const importDate = label.querySelector('small').textContent.replace('Imported: ', '');
                
                const woElement = document.createElement('div');
                woElement.className = 'd-flex justify-content-between align-items-center border-bottom py-1';
                woElement.innerHTML = `
                    <div>
                        <div style="font-size: 0.9em;">
                            <a href="/Admin/ModifyWorkOrder?id=${checkbox.value}" class="text-decoration-none fw-bold">${woName}</a>
                            <small class="text-muted ms-2">• Imported: ${importDate}</small>
                        </div>
                    </div>
                    <button type="button" class="btn btn-sm btn-link text-danger p-0" 
                            onclick="detachWorkOrder('${checkbox.value}', '${currentProjectId}')" title="Remove">
                        <i class="fas fa-times"></i>
                    </button>
                `;
                
                workOrderContainer.appendChild(woElement);
                
                // Remove from modal list
                checkbox.closest('.form-check').remove();
            });
            
            // Update work order count
            updateWorkOrderCountInTable(currentProjectId, workOrderIds.length);
            
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
                
                // Remove work order from DOM
                const detachButton = document.querySelector(`button[onclick*="detachWorkOrder('${workOrderId}'"`);
                if (detachButton) {
                    const workOrderElement = detachButton.closest('.d-flex.justify-content-between');
                    if (workOrderElement) {
                        workOrderElement.remove();
                        
                        // Update work order count
                        updateWorkOrderCountInTable(projectId, -1);
                        
                        // Check if no work orders left
                        const workOrderContainer = document.getElementById(`work-orders-${projectId}`);
                        if (workOrderContainer && workOrderContainer.children.length === 0) {
                            workOrderContainer.innerHTML = '<div class="text-center text-muted py-3"><small>No work orders associated</small></div>';
                        }
                    }
                }
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

// Helper function to update work order count in table
function updateWorkOrderCountInTable(projectId, delta) {
    // Find the row by looking for the expand icon with the project ID
    const expandIcon = document.getElementById(`expand-icon-${projectId}`);
    if (expandIcon) {
        const row = expandIcon.closest('tr');
        if (row) {
            const woCountBadge = row.querySelector('.badge.bg-info');
            if (woCountBadge) {
                const currentCount = parseInt(woCountBadge.textContent);
                const newCount = Math.max(0, currentCount + delta);
                woCountBadge.textContent = newCount;
            }
        }
    }
    
    // Also update count in section header
    const woHeader = document.querySelector(`#work-orders-${projectId}`)?.closest('.card')?.querySelector('small.fw-bold');
    if (woHeader) {
        const match = woHeader.textContent.match(/Work Orders \((\d+)\)/);
        if (match) {
            const currentCount = parseInt(match[1]);
            const newCount = Math.max(0, currentCount + delta);
            woHeader.textContent = `Work Orders (${newCount})`;
        }
    }
}

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
            
            // Add file to DOM instead of reloading
            const fileContainer = document.getElementById(`files-${projectId}`);
            const noFilesMsg = fileContainer.querySelector('.text-center.text-muted');
            if (noFilesMsg) {
                noFilesMsg.remove();
            }
            
            const attachment = data.attachment;
            const fileElement = document.createElement('div');
            fileElement.className = 'd-flex justify-content-between align-items-center border-bottom py-1';
            fileElement.innerHTML = `
                <div class="d-flex align-items-center flex-grow-1">
                    <i class="fas fa-file text-muted me-2" style="font-size: 0.9em;"></i>
                    <a href="/Project/DownloadFile?id=${attachment.id}" 
                       class="text-decoration-none me-2" style="font-size: 0.9em;">
                        ${attachment.originalFileName}
                    </a>
                    <select class="form-select form-select-sm d-inline-block w-auto" 
                            onchange="updateFileCategory('${attachment.id}', this.value)" 
                            style="font-size: 0.8em;">
                        <option value="Schematic">Schematic</option>
                        <option value="Correspondence">Correspondence</option>
                        <option value="Invoice">Invoice</option>
                        <option value="Proof">Proof</option>
                        <option value="Other" selected>Other</option>
                    </select>
                    <small class="text-muted ms-2">
                        ${Math.round(attachment.fileSize / 1024)} KB • ${new Date().toLocaleDateString('en-US', {month: '2-digit', day: '2-digit', year: '2-digit'})}
                    </small>
                </div>
                <button type="button" class="btn btn-sm btn-link text-danger p-0 ms-2" 
                        onclick="deleteFile('${attachment.id}', '${projectId}')" title="Delete">
                    <i class="fas fa-times"></i>
                </button>
            `;
            
            // Insert at the beginning
            fileContainer.insertBefore(fileElement, fileContainer.firstChild);
            
            // Update file count in table
            updateFileCountInTable(projectId, 1);
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

// File Management
function uploadFiles(projectId) {
    const fileInput = document.getElementById(`fileInput-${projectId}`);
    const categorySelect = document.getElementById(`categorySelect-${projectId}`);
    
    if (fileInput.files.length === 0) {
        showNotification('Please select files to upload', 'error');
        return;
    }

    const formData = new FormData();
    formData.append('projectId', projectId);
    formData.append('category', categorySelect.value);
    
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
            // Clear the file input and disable upload button
            fileInput.value = '';
            toggleUploadButton(projectId);
            
            // Add the new file to the file list dynamically
            const fileList = document.querySelector(`#files-${projectId} .file-list`);
            const attachment = data.attachment;
            
            // Remove "No files uploaded" message if it exists
            const noFilesMsg = fileList.querySelector('p.text-muted');
            if (noFilesMsg) {
                noFilesMsg.remove();
            }
            
            // Create new file element
            const fileElement = document.createElement('div');
            fileElement.className = 'd-flex justify-content-between align-items-center border-bottom py-2';
            fileElement.innerHTML = `
                <div class="flex-grow-1">
                    <div class="d-flex align-items-center">
                        <i class="fas fa-file me-2"></i>
                        <div>
                            <a href="/Project/DownloadFile/${attachment.id}" class="text-decoration-none">
                                ${attachment.originalFileName}
                            </a>
                            <br>
                            <small class="text-muted">
                                ${attachment.category} | ${Math.round(attachment.fileSize / 1024)} KB | ${new Date(attachment.uploadedDate).toLocaleDateString('en-US', {month: '2-digit', day: '2-digit', year: '2-digit'})}
                            </small>
                        </div>
                    </div>
                </div>
                <button type="button" class="btn btn-sm btn-outline-danger" onclick="deleteFile('${attachment.id}', '${projectId}')">
                    <i class="fas fa-trash"></i>
                </button>
            `;
            
            // Insert at the beginning of the file list
            fileList.insertBefore(fileElement, fileList.firstChild);
            
            // Update file count in header
            updateFileCount(projectId, 1);
        } else {
            showNotification(data.message || 'Error uploading files', 'error');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showNotification('Network error occurred', 'error');
    });
}

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
            
            // Remove file from DOM
            const deleteButton = document.querySelector(`button[onclick*="deleteFile('${fileId}'"`);
            if (deleteButton) {
                const fileElement = deleteButton.closest('.d-flex.justify-content-between');
                if (fileElement) {
                    fileElement.remove();
                    
                    // Update file count
                    updateFileCountInTable(projectId, -1);
                    
                    // Check if no files left
                    const fileContainer = document.getElementById(`files-${projectId}`);
                    if (fileContainer && fileContainer.children.length === 0) {
                        fileContainer.innerHTML = '<div class="text-center text-muted py-3"><small>No files uploaded</small></div>';
                    }
                }
            }
        } else {
            showNotification(data.message || 'Error deleting file', 'error');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showNotification('Network error occurred', 'error');
    });
}

// Helper function to update file count in table
function updateFileCountInTable(projectId, delta) {
    // Find the row by looking for the expand icon with the project ID
    const expandIcon = document.getElementById(`expand-icon-${projectId}`);
    if (expandIcon) {
        const row = expandIcon.closest('tr');
        if (row) {
            const fileCountBadge = row.querySelector('.badge.bg-success');
            if (fileCountBadge) {
                const currentCount = parseInt(fileCountBadge.textContent);
                const newCount = Math.max(0, currentCount + delta);
                fileCountBadge.textContent = newCount;
            }
        }
    }
    
    // Also update count in section header
    const filesHeader = document.querySelector(`#files-${projectId}`)?.closest('.card')?.querySelector('small.fw-bold');
    if (filesHeader) {
        const match = filesHeader.textContent.match(/Files \((\d+)\)/);
        if (match) {
            const currentCount = parseInt(match[1]);
            const newCount = Math.max(0, currentCount + delta);
            filesHeader.textContent = `Files (${newCount})`;
        }
    }
}

// Purchase Order Management
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
            
            // Add purchase order to DOM
            addPurchaseOrderToDOM(data.purchaseOrder, currentProjectId);
            
            // Update purchase order count
            updatePurchaseOrderCountInTable(currentProjectId, 1);
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
            
            // Update purchase order in DOM
            updatePurchaseOrderInDOM(data.purchaseOrder, currentProjectId);
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
                
                // Remove purchase order from DOM
                const deleteButton = document.querySelector(`button[onclick*="deletePurchaseOrder('${purchaseOrderId}'"]`);
                if (deleteButton) {
                    const purchaseOrderElement = deleteButton.closest('.d-flex.justify-content-between');
                    if (purchaseOrderElement) {
                        purchaseOrderElement.remove();
                        
                        // Update purchase order count
                        updatePurchaseOrderCountInTable(projectId, -1);
                        
                        // Check if no purchase orders left
                        const poContainer = document.getElementById(`purchase-orders-${projectId}`);
                        if (poContainer && poContainer.children.length === 0) {
                            poContainer.innerHTML = '<div class="text-center text-muted py-3"><small>No purchase orders created</small></div>';
                        }
                    }
                }
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

// Custom Work Order Management
let currentCustomWorkOrderId = null;

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
            
            // Add custom work order to DOM
            addCustomWorkOrderToDOM(data.customWorkOrder, currentProjectId);
            
            // Update work order count
            updateWorkOrderCountInTable(currentProjectId, 1);
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
            
            // Update custom work order in DOM
            updateCustomWorkOrderInDOM(data.customWorkOrder, currentProjectId);
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
                
                // Remove custom work order from DOM
                const deleteButton = document.querySelector(`button[onclick*="deleteCustomWorkOrder('${customWorkOrderId}'"]`);
                if (deleteButton) {
                    const customWorkOrderElement = deleteButton.closest('.d-flex.justify-content-between');
                    if (customWorkOrderElement) {
                        customWorkOrderElement.remove();
                        
                        // Update work order count
                        updateWorkOrderCountInTable(projectId, -1);
                        
                        // Check if no work orders left
                        const woContainer = document.getElementById(`work-orders-${projectId}`);
                        if (woContainer && woContainer.children.length === 0) {
                            woContainer.innerHTML = '<div class="text-center text-muted py-3"><small>No work orders associated</small></div>';
                        }
                    }
                }
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

// Helper functions for DOM manipulation
function addPurchaseOrderToDOM(purchaseOrder, projectId) {
    const poContainer = document.getElementById(`purchase-orders-${projectId}`);
    const noMsg = poContainer.querySelector('.text-center.text-muted');
    if (noMsg) {
        noMsg.remove();
    }
    
    const poElement = document.createElement('div');
    poElement.className = 'd-flex justify-content-between align-items-center border-bottom py-1';
    poElement.innerHTML = `
        <div class="d-flex align-items-center flex-grow-1">
            <i class="fas fa-file-invoice text-muted me-2" style="font-size: 0.9em;"></i>
            <div style="font-size: 0.9em;">
                <strong>${purchaseOrder.purchaseOrderNumber}</strong>
                <small class="text-muted ms-2">• ${purchaseOrder.vendorName}</small>
                <small class="text-muted ms-2">• <span class="badge bg-secondary" style="font-size: 0.7em;">${purchaseOrder.status}</span></small>
                <small class="text-muted ms-2">• ${new Date(purchaseOrder.orderDate).toLocaleDateString('en-US', {month: '2-digit', day: '2-digit', year: '2-digit'})}</small>
            </div>
        </div>
        <div class="d-flex">
            <button type="button" class="btn btn-sm btn-link text-primary p-0 me-2" 
                    onclick="editPurchaseOrder('${purchaseOrder.id}', '${projectId}')" title="Edit">
                <i class="fas fa-edit"></i>
            </button>
            <button type="button" class="btn btn-sm btn-link text-danger p-0" 
                    onclick="deletePurchaseOrder('${purchaseOrder.id}', '${projectId}')" title="Delete">
                <i class="fas fa-times"></i>
            </button>
        </div>
    `;
    
    poContainer.insertBefore(poElement, poContainer.firstChild);
}

function updatePurchaseOrderInDOM(purchaseOrder, projectId) {
    // TODO: Implement update logic for existing purchase order in DOM
    console.log('Update purchase order in DOM', purchaseOrder);
}

function addCustomWorkOrderToDOM(customWorkOrder, projectId) {
    const woContainer = document.getElementById(`work-orders-${projectId}`);
    const noMsg = woContainer.querySelector('.text-center.text-muted');
    if (noMsg) {
        noMsg.remove();
    }
    
    const woElement = document.createElement('div');
    woElement.className = 'd-flex justify-content-between align-items-center border-bottom py-1';
    woElement.innerHTML = `
        <div class="d-flex align-items-center">
            <i class="fas fa-wrench text-info me-2" style="font-size: 0.8em;" title="Custom Work Order"></i>
            <div style="font-size: 0.9em;">
                <span class="fw-bold">${customWorkOrder.name}</span>
                <small class="text-muted ms-2">• ${customWorkOrder.workOrderType}</small>
                <small class="text-muted ms-2">• <span class="badge bg-secondary" style="font-size: 0.7em;">${customWorkOrder.status}</span></small>
                <small class="text-muted ms-2">• Created: ${new Date(customWorkOrder.createdDate).toLocaleDateString('en-US', {month: '2-digit', day: '2-digit', year: '2-digit'})}</small>
            </div>
        </div>
        <div class="d-flex">
            <button type="button" class="btn btn-sm btn-link text-primary p-0 me-2" 
                    onclick="editCustomWorkOrder('${customWorkOrder.id}', '${projectId}')" title="Edit">
                <i class="fas fa-edit"></i>
            </button>
            <button type="button" class="btn btn-sm btn-link text-danger p-0" 
                    onclick="deleteCustomWorkOrder('${customWorkOrder.id}', '${projectId}')" title="Delete">
                <i class="fas fa-times"></i>
            </button>
        </div>
    `;
    
    woContainer.appendChild(woElement);
}

function updateCustomWorkOrderInDOM(customWorkOrder, projectId) {
    // TODO: Implement update logic for existing custom work order in DOM
    console.log('Update custom work order in DOM', customWorkOrder);
}

function updatePurchaseOrderCountInTable(projectId, delta) {
    // TODO: Update purchase order count in table header if needed
    console.log('Update PO count', projectId, delta);
}

// SmartSheet Import Functions
function showSmartSheetImport() {
    // Reset modal state
    document.getElementById('smartSheetSelection').classList.remove('d-none');
    document.getElementById('smartSheetLoadingState').classList.add('d-none');
    document.getElementById('smartSheetImporting').classList.add('d-none');
    document.getElementById('smartSheetResults').classList.add('d-none');
    document.getElementById('startImportBtn').disabled = false; // Enable immediately
    
    // Show modal
    const modal = new bootstrap.Modal(document.getElementById('smartSheetImportModal'));
    modal.show();
}


function startSmartSheetImport() {
    
    // Show importing state
    document.getElementById('smartSheetSelection').classList.add('d-none');
    document.getElementById('smartSheetImporting').classList.remove('d-none');
    document.getElementById('startImportBtn').disabled = true;
    
    // Start import
    fetch('/Project/ImportFromSmartSheet', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        // No body needed - the service will find the Master Project List sheet directly
    })
    .then(response => response.json())
    .then(data => {
        document.getElementById('smartSheetImporting').classList.add('d-none');
        
        if (data.success) {
            document.getElementById('smartSheetResults').classList.remove('d-none');
            document.getElementById('importResultsText').innerHTML = `
                <div><strong>Import Summary:</strong></div>
                <ul class="mb-0">
                    <li>Projects Created: <strong>${data.result.projectsCreated}</strong></li>
                    <li>Projects Skipped (already exist): <strong>${data.result.projectsSkipped}</strong></li>
                    <li>Projects with Errors: <strong>${data.result.projectsWithErrors}</strong></li>
                    <li>Total Rows Processed: <strong>${data.result.totalRowsProcessed}</strong></li>
                </ul>
                ${data.result.projectsCreated > 0 ? '<p class="mt-2 mb-0"><em>Page will refresh to show imported projects...</em></p>' : ''}
            `;
            
            showNotification(data.message, 'success');
            
            // Refresh page after a short delay if projects were created
            if (data.result.projectsCreated > 0) {
                setTimeout(() => {
                    location.reload();
                }, 3000);
            }
        } else {
            document.getElementById('smartSheetSelection').classList.remove('d-none');
            document.getElementById('startImportBtn').disabled = false;
            showNotification(data.message || 'Error during SmartSheet import', 'error');
        }
    })
    .catch(error => {
        console.error('Error during SmartSheet import:', error);
        document.getElementById('smartSheetImporting').classList.add('d-none');
        document.getElementById('smartSheetSelection').classList.remove('d-none');
        document.getElementById('startImportBtn').disabled = false;
        showNotification('Network error during SmartSheet import', 'error');
    });
}

// Notification system
function showNotification(message, type) {
    const alertClass = type === 'success' ? 'alert-success' : 'alert-danger';
    const icon = type === 'success' ? 'fa-check-circle' : 'fa-exclamation-circle';
    
    // Remove any existing notifications
    const existingAlert = document.querySelector('.project-notification');
    if (existingAlert) {
        existingAlert.remove();
    }
    
    // Create new notification
    const alertHtml = `
        <div class="alert ${alertClass} alert-dismissible fade show project-notification" role="alert" style="position: fixed; top: 80px; right: 20px; z-index: 1050; min-width: 300px;">
            <i class="fas ${icon} me-2"></i>
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `;
    
    document.body.insertAdjacentHTML('beforeend', alertHtml);
    
    // Auto-remove after 4 seconds
    setTimeout(() => {
        const notification = document.querySelector('.project-notification');
        if (notification) {
            notification.remove();
        }
    }, 4000);
}