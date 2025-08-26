// Project Management JavaScript
// 
// ⚠️  WARNING: This file still contains 1,285+ lines and violates maintainability principles.
// ✅  Timeline functions have been extracted to timeline.js (350+ lines removed).
// ✅  SmartSheet functions have been extracted to smartsheet.js (295+ lines removed).
// ⚠️  TODO: Extract remaining features into separate modules:
//     - Purchase Order functions (200 lines) -> purchase-orders.js
//     - File management functions (145 lines) -> file-management.js
//     - Work Order functions (140 lines) -> work-orders.js
//     - Convert DOM building to server-rendered partials
//
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
            
            // Update the project ID and name in the table row if they changed
            const projectRow = document.getElementById(`expand-icon-${projectId}`).closest('tr');
            const projectIdCell = projectRow.querySelector('td:nth-child(2)');
            const projectNameCell = projectRow.querySelector('td:nth-child(3) strong');
            if (projectIdCell) {
                projectIdCell.textContent = project.ProjectId;
            }
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


function updatePurchaseOrderCountInTable(projectId, delta) {
    // TODO: Update purchase order count in table header if needed
    console.log('Update PO count', projectId, delta);
}

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

// SmartSheet Integration Functions have been moved to /js/smartsheet.js

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
// Timeline management functions have been moved to /js/timeline.js