// SmartSheet Integration JavaScript
// Extracted from project-management.js to reduce file size and improve maintainability

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

// Initialize SmartSheet status checking when projects are expanded
function initializeSmartSheetStatus(projectId) {
    // Check session status
    checkSmartSheetSessionStatus(projectId);
    
    // Load SmartSheet info if project is linked
    const project = document.querySelector(`#details-${projectId}`);
    if (project) {
        const smartSheetInfo = project.querySelector(`#smartsheet-info-${projectId}`);
        if (smartSheetInfo) {
            loadSmartSheetInfo(projectId);
        }
    }
}

// Check SmartSheet session status
function checkSmartSheetSessionStatus(projectId) {
    fetch('/Project/GetSmartSheetSessionStatus')
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const statusDiv = document.getElementById(`smartsheet-session-status-${projectId}`);
                if (statusDiv) {
                    if (data.hasSession) {
                        statusDiv.innerHTML = `<span class="text-success"><i class="fas fa-check-circle me-1"></i>${data.userEmail || 'Connected'}</span>`;
                    } else {
                        statusDiv.innerHTML = `<span class="text-muted"><i class="fas fa-times-circle me-1"></i>Not Connected</span>`;
                    }
                }
            }
        })
        .catch(error => {
            console.error('Error checking SmartSheet session status:', error);
        });
}

// Load SmartSheet information
function loadSmartSheetInfo(projectId) {
    const project = document.querySelector(`#details-${projectId}`);
    if (!project) return;
    
    const smartSheetIdElement = project.querySelector('[data-smartsheet-id]');
    if (!smartSheetIdElement) return;
    
    const smartSheetId = smartSheetIdElement.dataset.smartsheetId;
    if (!smartSheetId || smartSheetId === '0') return;
    
    fetch(`/Project/GetSmartSheetInfo?sheetId=${smartSheetId}`)
        .then(response => response.json())
        .then(data => {
            const infoDiv = document.getElementById(`smartsheet-info-${projectId}`);
            if (infoDiv) {
                if (data.success && data.sheet) {
                    const sheet = data.sheet;
                    const lastSync = sheet.modifiedAt ? new Date(sheet.modifiedAt).toLocaleDateString() : 'Never';
                    infoDiv.innerHTML = `
                        <div class="d-flex justify-content-between align-items-center py-1 border-bottom">
                            <div>
                                <i class="fas fa-table text-success me-2"></i>
                                <strong style="font-size: 0.9em;">${sheet.name}</strong>
                                <small class="text-muted ms-2">• ${sheet.rowCount} rows</small>
                            </div>
                            <div>
                                <small class="text-muted">Modified: ${lastSync}</small>
                            </div>
                        </div>
                        ${sheet.permalink ? `
                        <div class="mt-2">
                            <a href="${sheet.permalink}" target="_blank" class="btn btn-sm btn-outline-secondary">
                                <i class="fas fa-external-link-alt me-1"></i>Open in SmartSheet
                            </a>
                        </div>
                        ` : ''}
                    `;
                } else {
                    infoDiv.innerHTML = `
                        <div class="text-center text-danger py-2">
                            <small><i class="fas fa-exclamation-triangle me-1"></i>Error loading SmartSheet: ${data.message}</small>
                        </div>
                    `;
                }
            }
        })
        .catch(error => {
            console.error('Error loading SmartSheet info:', error);
            const infoDiv = document.getElementById(`smartsheet-info-${projectId}`);
            if (infoDiv) {
                infoDiv.innerHTML = `
                    <div class="text-center text-danger py-2">
                        <small><i class="fas fa-exclamation-triangle me-1"></i>Network error loading SmartSheet</small>
                    </div>
                `;
            }
        });
}

// Show SmartSheet linking dialog
function showSmartSheetLinking(projectId) {
    currentProjectId = projectId;
    
    // Check if user has SmartSheet session first
    fetch('/Project/GetSmartSheetSessionStatus')
        .then(response => response.json())
        .then(data => {
            if (data.success && !data.hasSession) {
                // Offer to connect to SmartSheet
                if (confirm('You need to connect to SmartSheet first. Would you like to connect now?')) {
                    connectToSmartSheet(projectId);
                }
                return;
            }
            
            // Show the linking modal
            showSmartSheetLinkingModal(projectId);
        })
        .catch(error => {
            console.error('Error checking session:', error);
            showNotification('Error checking SmartSheet connection', 'error');
        });
}

// Connect to SmartSheet via OAuth
function connectToSmartSheet(projectId) {
    // Open OAuth popup
    const popup = window.open(
        '/smartsheet/auth/login', 
        'smartsheet-auth', 
        'width=600,height=700,scrollbars=yes,resizable=yes'
    );
    
    if (!popup) {
        showNotification('Popup blocked. Please allow popups for this site and try again.', 'warning');
        return;
    }
    
    // Listen for messages from the popup
    const messageListener = (event) => {
        // Ensure the message is from our OAuth popup
        if (event.source !== popup) return;
        
        if (event.data.type === 'smartsheet-auth-success') {
            showNotification('Successfully connected to SmartSheet!', 'success');
            // Update session status display
            checkSmartSheetSessionStatus(projectId);
            // Now show the linking modal
            setTimeout(() => {
                showSmartSheetLinkingModal(projectId);
            }, 1000);
        } else if (event.data.type === 'smartsheet-auth-error') {
            showNotification('SmartSheet connection failed: ' + (event.data.error || 'Unknown error'), 'error');
        }
        
        // Clean up listener
        window.removeEventListener('message', messageListener);
    };
    
    window.addEventListener('message', messageListener);
    
    // Check if popup was closed manually
    const checkClosed = setInterval(() => {
        if (popup.closed) {
            clearInterval(checkClosed);
            window.removeEventListener('message', messageListener);
        }
    }, 1000);
}

// Show the actual linking modal (would need HTML modal in view)
function showSmartSheetLinkingModal(projectId) {
    // For Phase 1, we'll use a simple prompt - in Phase 2 we'll add proper modal
    const sheetId = prompt('Enter SmartSheet ID to link to this project:');
    if (sheetId && !isNaN(sheetId)) {
        linkProjectToSmartSheet(projectId, parseInt(sheetId));
    }
}

// Link project to SmartSheet
function linkProjectToSmartSheet(projectId, sheetId) {
    const requestData = {
        ProjectId: projectId,
        SheetId: sheetId
    };
    
    fetch('/Project/LinkProjectToSmartSheet', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestData)
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification(data.message || 'Project linked to SmartSheet successfully!', 'success');
            // Reload the page to show updated state
            window.location.reload();
        } else {
            showNotification(data.message || 'Error linking to SmartSheet', 'error');
        }
    })
    .catch(error => {
        console.error('Error linking project:', error);
        showNotification('Network error occurred', 'error');
    });
}

// Unlink project from SmartSheet
function unlinkSmartSheet(projectId) {
    if (!confirm('Are you sure you want to unlink this project from SmartSheet?')) {
        return;
    }
    
    const requestData = {
        ProjectId: projectId
    };
    
    fetch('/Project/UnlinkProjectFromSmartSheet', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestData)
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification(data.message || 'Project unlinked from SmartSheet successfully!', 'success');
            // Reload the page to show updated state
            window.location.reload();
        } else {
            showNotification(data.message || 'Error unlinking from SmartSheet', 'error');
        }
    })
    .catch(error => {
        console.error('Error unlinking project:', error);
        showNotification('Network error occurred', 'error');
    });
}

// ==========================================
// Streamlined SmartSheet Import Functions
// ==========================================

function openSmartSheetImportModal() {
    // Reset modal state
    hideAllImportSections();
    document.getElementById('import-loading').style.display = 'block';
    document.getElementById('import-status').textContent = 'Checking SmartSheet authentication...';
    
    // Show modal
    const modal = new bootstrap.Modal(document.getElementById('smartSheetImportModal'));
    modal.show();
    
    // Check authentication and load active jobs
    checkAuthAndLoadActiveJobs();
}

function hideAllImportSections() {
    document.getElementById('import-loading').style.display = 'none';
    document.getElementById('import-sheet-selection').style.display = 'none';
    document.getElementById('import-complete').style.display = 'none';
    document.getElementById('import-error').style.display = 'none';
    document.getElementById('refresh-projects-btn').style.display = 'none';
}

function checkAuthAndLoadActiveJobs() {
    // Check SmartSheet auth status using the existing endpoint
    fetch('/smartsheet/auth/status')
        .then(response => response.json())
        .then(data => {
            if (data.isAuthenticated) {  // Use correct property name
                document.getElementById('import-status').textContent = 'Loading active jobs...';
                loadActiveJobs();
            } else {
                // Need to authenticate
                document.getElementById('import-status').textContent = 'SmartSheet authentication required...';
                setTimeout(() => {
                    triggerSmartSheetAuth().then(() => {
                        document.getElementById('import-status').textContent = 'Loading active jobs...';
                        loadActiveJobs();
                    }).catch(error => {
                        showImportError('Authentication failed: ' + error.message);
                    });
                }, 1000);
            }
        })
        .catch(error => {
            showImportError('Failed to check authentication: ' + error.message);
        });
}

function loadActiveJobs() {
    // Use the new endpoint that returns JSON directly
    fetch('/SmartSheetMigration/GetActiveJobs')
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                displayActiveJobs(data.activeJobs);
            } else {
                showImportError('Failed to load active jobs: ' + data.message);
            }
        })
        .catch(error => {
            showImportError('Failed to load active jobs: ' + error.message);
        });
}

function displayActiveJobs(jobs) {
    hideAllImportSections();
    document.getElementById('import-sheet-selection').style.display = 'block';
    
    const jobsList = document.getElementById('active-jobs-list');
    jobsList.innerHTML = '';
    
    if (jobs.length === 0) {
        jobsList.innerHTML = '<div class="alert alert-info">No active jobs found in the Active Jobs workspace.</div>';
        return;
    }
    
    jobs.forEach(job => {
        const jobItem = document.createElement('div');
        jobItem.className = 'list-group-item list-group-item-action d-flex justify-content-between align-items-center';
        jobItem.innerHTML = `
            <div>
                <h6 class="mb-1">${job.name}</h6>
                <small class="text-muted">Last modified: ${new Date(job.modifiedAt).toLocaleDateString()}</small>
            </div>
            <button class="btn btn-primary btn-sm" onclick="importSmartSheetJob(${job.id}, '${job.name.replace(/'/g, "\\'")}')">
                <i class="fas fa-download me-1"></i>Import
            </button>
        `;
        jobsList.appendChild(jobItem);
    });
}

function importSmartSheetJob(sheetId, sheetName) {
    // Show loading state
    hideAllImportSections();
    document.getElementById('import-loading').style.display = 'block';
    document.getElementById('import-status').textContent = 'Importing project from SmartSheet...';
    
    // Show progress bar
    const progressContainer = document.querySelector('#import-loading .progress');
    const progressBar = document.querySelector('.progress-bar');
    progressContainer.style.display = 'block';
    progressBar.style.width = '10%';
    
    // Update progress periodically
    const progressInterval = setInterval(() => {
        const currentWidth = parseInt(progressBar.style.width) || 10;
        if (currentWidth < 90) {
            progressBar.style.width = (currentWidth + 5) + '%';
        }
    }, 1000);
    
    // Generate project ID from sheet name
    const projectId = generateProjectIdFromName(sheetName);
    
    // Start import
    const importRequest = {
        sheetId: sheetId,
        projectId: projectId,
        projectName: sheetName,
        projectManager: null, // Will be auto-populated from summary
        importAttachments: true,
        importComments: true
    };
    
    fetch('/SmartSheetMigration/ImportProject', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(importRequest)
    })
    .then(response => response.json())
    .then(data => {
        clearInterval(progressInterval);
        progressBar.style.width = '100%';
        
        if (data.success) {
            showImportSuccess(data.message);
        } else {
            showImportError(data.message);
        }
    })
    .catch(error => {
        clearInterval(progressInterval);
        showImportError('Import failed: ' + error.message);
    });
}

function generateProjectIdFromName(sheetName) {
    // Extract numbers from sheet name for project ID, or use timestamp as fallback
    const numbers = sheetName.match(/\d+/);
    return numbers ? numbers[0] : Date.now().toString().slice(-6);
}

function showImportSuccess(message) {
    hideAllImportSections();
    document.getElementById('import-complete').style.display = 'block';
    document.getElementById('import-result-message').textContent = message;
    document.getElementById('refresh-projects-btn').style.display = 'inline-block';
}

function showImportError(message) {
    hideAllImportSections();
    document.getElementById('import-error').style.display = 'block';
    document.getElementById('import-error-message').textContent = message;
}

function triggerSmartSheetAuth() {
    return new Promise((resolve, reject) => {
        const popup = window.open('/smartsheet/auth/login', 'SmartSheetAuth', 'width=600,height=700,scrollbars=yes,resizable=yes');
        
        const messageListener = (event) => {
            if (event.origin !== window.location.origin) return;
            
            if (event.data.type === 'smartsheet-auth-success') {
                popup.close();
                resolve();
            } else if (event.data.type === 'smartsheet-auth-error') {
                popup.close();
                reject(new Error(event.data.error || 'Authentication failed'));
            }
        };
        
        window.addEventListener('message', messageListener);
        
        // Check if popup was closed manually
        const checkClosed = setInterval(() => {
            if (popup.closed) {
                clearInterval(checkClosed);
                window.removeEventListener('message', messageListener);
                reject(new Error('Authentication cancelled'));
            }
        }, 1000);
    });
}