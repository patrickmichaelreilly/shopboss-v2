// Project Management JavaScript (Project list + CRUD only)
let currentProjectId = null;

// SmartSheet Sync Functions
async function initializeSmartSheetSyncUI(projectId) {
    try {
        const response = await fetch('/smartsheet/auth/status');
        const data = await response.json();
        
        const syncBtn = document.getElementById(`smartsheet-sync-btn-${projectId}`);
        const syncFromBtn = document.getElementById(`smartsheet-sync-from-btn-${projectId}`);
        const syncStatus = document.getElementById(`smartsheet-sync-status-${projectId}`);
        
        if (data.isAuthenticated && !data.isExpired) {
            if (syncBtn) syncBtn.style.display = 'inline-block';
            if (syncFromBtn) syncFromBtn.style.display = 'inline-block';
            if (syncStatus) syncStatus.textContent = '';
        } else {
            if (syncStatus) syncStatus.textContent = 'Smartsheet not connected';
        }
    } catch (error) {
        console.error('Error checking SmartSheet auth status:', error);
    }
}

async function syncToSmartSheet(projectId) {
    const btn = document.getElementById(`smartsheet-sync-btn-${projectId}`);
    const status = document.getElementById(`smartsheet-sync-status-${projectId}`);
    
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Syncing...';
    status.textContent = 'Syncing events to Smartsheet...';
    
    try {
        const response = await fetch(`/api/smartsheet/sync/${projectId}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });
        
        const data = await response.json();
        
        if (data.success) {
            status.textContent = `Sync completed: ${data.created} created, ${data.updated} updated`;
            status.className = 'text-success small ms-2';
            
            // Refresh timeline to show row numbers
            if (typeof loadTimelineForProject === 'function') {
                loadTimelineForProject(projectId);
            }
        } else {
            status.textContent = `Sync failed: ${data.message}`;
            status.className = 'text-danger small ms-2';
        }
    } catch (error) {
        console.error('Error syncing to SmartSheet:', error);
        status.textContent = 'Error during sync. Please try again.';
        status.className = 'text-danger small ms-2';
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fas fa-upload me-1"></i>To Smartsheet';
    }
}

async function syncFromSmartSheet(projectId) {
    const btn = document.getElementById(`smartsheet-sync-from-btn-${projectId}`);
    const status = document.getElementById(`smartsheet-sync-status-${projectId}`);
    
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Syncing...';
    status.textContent = 'Syncing from Smartsheet...';
    
    try {
        const response = await fetch(`/api/smartsheet/sync-from/${projectId}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });
        
        const data = await response.json();
        
        if (data.success) {
            status.textContent = `Sync from Smartsheet completed: ${data.updated || 0} updated`;
            status.className = 'text-success small ms-2';
            
            // Refresh timeline to show updated data
            if (typeof loadTimelineForProject === 'function') {
                loadTimelineForProject(projectId);
            }
        } else {
            status.textContent = `Sync from Smartsheet failed: ${data.message}`;
            status.className = 'text-danger small ms-2';
        }
    } catch (error) {
        console.error('Error syncing from Smartsheet:', error);
        status.textContent = 'Error during sync from Smartsheet. Please try again.';
        status.className = 'text-danger small ms-2';
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fas fa-download me-1"></i>From Smartsheet';
    }
}
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

    (typeof apiPostJson === 'function' ? apiPostJson('/Project/Create', project) : fetch('/Project/Create', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(project) }).then(r => r.json()))
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
    const displayButtons = document.getElementById(`display-buttons-${projectId}`);
    const editButtons = document.getElementById(`edit-buttons-${projectId}`);
    const editableFields = document.querySelectorAll(`#project-display-${projectId} .editable-field`);
    
    // Store original values for cancel functionality
    window.originalValues = window.originalValues || {};
    window.originalValues[projectId] = {};
    
    // Convert each editable field to input
    editableFields.forEach(field => {
        const fieldName = field.dataset.field;
        const fieldType = field.dataset.type;
        const currentValue = field.dataset.value || field.textContent.trim();
        
        // Store original value and content
        window.originalValues[projectId][fieldName] = {
            value: currentValue === '-' ? '' : currentValue,
            html: field.innerHTML
        };
        
        // Create appropriate input element
        let inputElement;
        
        switch (fieldType) {
            case 'textarea':
                inputElement = document.createElement('textarea');
                inputElement.className = 'form-control form-control-sm';
                inputElement.rows = fieldName === 'Notes' ? 3 : 2;
                inputElement.value = currentValue === '-' ? '' : currentValue;
                break;
                
            case 'select':
                inputElement = document.createElement('select');
                inputElement.className = 'form-select form-select-sm';
                inputElement.innerHTML = `
                    <option value="0" ${currentValue == '0' ? 'selected' : ''}>Standard Products</option>
                    <option value="1" ${currentValue == '1' ? 'selected' : ''}>Custom Products</option>
                    <option value="2" ${currentValue == '2' ? 'selected' : ''}>Small Project</option>
                `;
                break;
                
            case 'date':
                inputElement = document.createElement('input');
                inputElement.type = 'date';
                inputElement.className = 'form-control form-control-sm';
                inputElement.value = currentValue === '-' ? '' : currentValue;
                break;
                
            default:
                inputElement = document.createElement('input');
                inputElement.type = fieldType || 'text';
                inputElement.className = 'form-control form-control-sm';
                inputElement.value = currentValue === '-' ? '' : currentValue;
                break;
        }
        
        inputElement.dataset.field = fieldName;
        field.innerHTML = '';
        field.appendChild(inputElement);
    });
    
    // Switch buttons
    displayButtons.classList.add('d-none');
    editButtons.classList.remove('d-none');
}

function cancelProjectEdit(projectId) {
    const displayButtons = document.getElementById(`display-buttons-${projectId}`);
    const editButtons = document.getElementById(`edit-buttons-${projectId}`);
    const editableFields = document.querySelectorAll(`#project-display-${projectId} .editable-field`);
    
    // Restore original content for each field
    editableFields.forEach(field => {
        const fieldName = field.dataset.field;
        if (window.originalValues && window.originalValues[projectId] && window.originalValues[projectId][fieldName]) {
            field.innerHTML = window.originalValues[projectId][fieldName].html;
        }
    });
    
    // Clean up stored values
    if (window.originalValues && window.originalValues[projectId]) {
        delete window.originalValues[projectId];
    }
    
    // Switch buttons
    editButtons.classList.add('d-none');
    displayButtons.classList.remove('d-none');
}

function saveProjectEdit(projectId) {
    const editableFields = document.querySelectorAll(`#project-display-${projectId} .editable-field input, #project-display-${projectId} .editable-field textarea, #project-display-${projectId} .editable-field select`);
    
    // Collect values from inline inputs
    const project = {
        Id: projectId
    };
    
    editableFields.forEach(input => {
        const fieldName = input.dataset.field;
        let value = input.value.trim();
        
        // Handle different field types
        if (fieldName === 'ProjectCategory') {
            project[fieldName] = parseInt(value) || 0;
        } else if (fieldName === 'BidRequestDate') {
            project[fieldName] = value || null;
        } else {
            project[fieldName] = value || null;
        }
    });

    (typeof apiPostJson === 'function' ? apiPostJson('/Project/Update', project) : fetch('/Project/Update', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(project) }).then(r => r.json()))
    .then(data => {
        if (data.success) {
            showNotification('Project updated successfully', 'success');
            
            // Update display values for each field
            const editableFields = document.querySelectorAll(`#project-display-${projectId} .editable-field`);
            editableFields.forEach(field => {
                const input = field.querySelector('input, textarea, select');
                if (input) {
                    const fieldName = input.dataset.field;
                    const value = input.value.trim();
                    
                    // Update the field's display content
                    if (fieldName === 'ProjectCategory') {
                        const categoryNames = ['Standard Products', 'Custom Products', 'Small Project'];
                        const categoryName = categoryNames[parseInt(value)] || 'Unknown';
                        field.innerHTML = `<span class="badge bg-secondary">${categoryName}</span>`;
                    } else if (fieldName === 'BidRequestDate' && value) {
                        const date = new Date(value);
                        const displayDate = date.toLocaleDateString('en-US', {month: '2-digit', day: '2-digit', year: '2-digit'});
                        field.innerHTML = displayDate;
                        field.dataset.value = value;
                    } else {
                        field.innerHTML = value || '-';
                    }
                }
            });
            
            // Clean up stored values
            if (window.originalValues && window.originalValues[projectId]) {
                delete window.originalValues[projectId];
            }
            
            // Switch buttons back
            const displayButtons = document.getElementById(`display-buttons-${projectId}`);
            const editButtons = document.getElementById(`edit-buttons-${projectId}`);
            editButtons.classList.add('d-none');
            displayButtons.classList.remove('d-none');
        } else {
            showNotification(data.message || 'Error updating project', 'error');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showNotification('Network error occurred', 'error');
    });
}

function archiveProject(projectId) {
    if (confirm('Are you sure you want to archive this project?')) {
        (typeof apiPostForm === 'function' ? apiPostForm('/Project/Archive', new URLSearchParams({ id: projectId })) : fetch('/Project/Archive', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: `id=${projectId}` }).then(r => r.json()))
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
    (typeof apiPostForm === 'function' ? apiPostForm('/Project/Unarchive', new URLSearchParams({ id: projectId })) : fetch('/Project/Unarchive', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: `id=${projectId}` }).then(r => r.json()))
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
        (typeof apiPostForm === 'function' ? apiPostForm('/Project/Delete', new URLSearchParams({ id: projectId })) : fetch('/Project/Delete', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: `id=${projectId}` }).then(r => r.json()))
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


// (deprecated placeholder removed)

// Notification system (kept here as it's used by multiple features)
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

// SmartSheet Integration functions are in /js/smartsheet.js

// Modify the existing toggleProject function to initialize SmartSheet status and load timeline
const originalToggleProject = toggleProject;
toggleProject = function(projectId) {
    const wasHidden = document.getElementById(`details-${projectId}`).classList.contains('d-none');
    originalToggleProject(projectId);
    
    // If project was just expanded (was hidden, now showing), initialize SmartSheet status and load timeline
    if (wasHidden) {
        // SmartSheet initialization is now handled by smartsheet.js
        if (typeof initializeSmartSheetStatus === 'function') {
            initializeSmartSheetStatus(projectId);
        }
        // Timeline loading is now handled by timeline.js
        if (typeof loadTimelineForProject === 'function') {
            loadTimelineForProject(projectId);
        }
    }
};

// ============== END OF FILE ==============
// Timeline management functions are in /js/timeline.js
