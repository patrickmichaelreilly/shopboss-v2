// Project Management JavaScript
let currentProjectId = null;

// Toggle project expansion
function toggleProject(projectId) {
    const details = document.getElementById(`details-${projectId}`);
    const icon = document.getElementById(`expand-icon-${projectId}`);
    
    if (details.classList.contains('d-none')) {
        details.classList.remove('d-none');
        icon.classList.remove('fa-chevron-right');
        icon.classList.add('fa-chevron-down');
    } else {
        details.classList.add('d-none');
        icon.classList.remove('fa-chevron-down');
        icon.classList.add('fa-chevron-right');
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
    const display = document.getElementById(`display-${projectId}`);
    const edit = document.getElementById(`edit-${projectId}`);
    
    display.classList.add('d-none');
    edit.classList.remove('d-none');
}

function cancelProjectEdit(projectId) {
    const display = document.getElementById(`display-${projectId}`);
    const edit = document.getElementById(`edit-${projectId}`);
    
    edit.classList.add('d-none');
    display.classList.remove('d-none');
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
            location.reload();
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
                // Remove the project card from the DOM
                const projectCard = document.querySelector(`[data-project-id="${projectId}"]`);
                if (projectCard) {
                    projectCard.remove();
                }
                showNotification('Project deleted successfully', 'success');
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
            location.reload();
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
                location.reload();
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

// Toggle upload button based on file selection
function toggleUploadButton(projectId) {
    const fileInput = document.getElementById(`fileInput-${projectId}`);
    const uploadBtn = document.getElementById(`uploadBtn-${projectId}`);
    
    if (fileInput.files.length > 0) {
        uploadBtn.disabled = false;
        uploadBtn.classList.remove('btn-secondary');
        uploadBtn.classList.add('btn-primary');
    } else {
        uploadBtn.disabled = true;
        uploadBtn.classList.remove('btn-primary');
        uploadBtn.classList.add('btn-secondary');
    }
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
    if (confirm('Are you sure you want to delete this file?')) {
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
                
                // Find and remove the file element from DOM
                const deleteButton = document.querySelector(`button[onclick*="deleteFile('${fileId}'"]`);
                if (deleteButton) {
                    const fileElement = deleteButton.closest('.d-flex.justify-content-between');
                    if (fileElement) {
                        fileElement.remove();
                        
                        // Update file count in header
                        updateFileCount(projectId, -1);
                        
                        // Check if no files left, show "No files uploaded" message
                        const fileList = document.querySelector(`#files-${projectId} .file-list`);
                        if (fileList && fileList.children.length === 0) {
                            const noFilesMsg = document.createElement('p');
                            noFilesMsg.className = 'text-muted';
                            noFilesMsg.textContent = 'No files uploaded';
                            fileList.appendChild(noFilesMsg);
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
}

// Helper function to update file count badges
function updateFileCount(projectId, delta) {
    const fileCountBadge = document.querySelector(`[data-project-id="${projectId}"] .badge.bg-success`);
    if (fileCountBadge) {
        const currentCount = parseInt(fileCountBadge.textContent.match(/\d+/)[0]);
        const newCount = Math.max(0, currentCount + delta);
        fileCountBadge.textContent = `${newCount} Files`;
    }
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