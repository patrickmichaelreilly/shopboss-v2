// Admin Station JavaScript

// ===== Admin Index (Work Orders Management) =====

// Single delete function
function confirmDelete(id, name) {
    if (confirm(`Are you sure you want to delete work order "${name}"? This action cannot be undone.`)) {
        document.getElementById('deleteId').value = id;
        document.getElementById('deleteForm').action = '/Admin/DeleteWorkOrder';
        document.getElementById('deleteForm').submit();
    }
}

// Set active work order function
function setActiveWorkOrder(id) {
    document.getElementById('activeWorkOrderId').value = id;
    document.getElementById('setActiveForm').action = '/Admin/SetActiveWorkOrder';
    document.getElementById('setActiveForm').submit();
}

// Update bulk action buttons based on selection
function updateBulkActions() {
    const checkedBoxes = document.querySelectorAll('.work-order-checkbox:checked');
    const bulkDeleteBtn = document.getElementById('bulkDeleteBtn');
    
    if (bulkDeleteBtn) {
        if (checkedBoxes.length > 0) {
            bulkDeleteBtn.disabled = false;
            bulkDeleteBtn.innerHTML = `<i class="fas fa-trash me-2"></i>Delete Selected (${checkedBoxes.length})`;
        } else {
            bulkDeleteBtn.disabled = true;
            bulkDeleteBtn.innerHTML = '<i class="fas fa-trash me-2"></i>Delete Selected';
        }
    }

    // Update select all checkbox state
    const allCheckboxes = document.querySelectorAll('.work-order-checkbox');
    const selectAllCheckbox = document.getElementById('selectAll');
    
    if (selectAllCheckbox) {
        if (checkedBoxes.length === allCheckboxes.length && allCheckboxes.length > 0) {
            selectAllCheckbox.checked = true;
            selectAllCheckbox.indeterminate = false;
        } else if (checkedBoxes.length > 0) {
            selectAllCheckbox.checked = false;
            selectAllCheckbox.indeterminate = true;
        } else {
            selectAllCheckbox.checked = false;
            selectAllCheckbox.indeterminate = false;
        }
    }
}

// Bulk delete function
function performBulkDelete() {
    const checkedBoxes = document.querySelectorAll('.work-order-checkbox:checked');
    
    if (checkedBoxes.length === 0) {
        alert('Please select work orders to delete.');
        return;
    }

    const workOrderNames = Array.from(checkedBoxes).map(cb => {
        const row = cb.closest('tr');
        return row.cells[2].textContent.trim(); // Name column (now at index 2)
    });

    const confirmMessage = `Are you sure you want to delete ${checkedBoxes.length} work order(s)?\n\n${workOrderNames.join('\n')}\n\nThis action cannot be undone.`;
    
    if (confirm(confirmMessage)) {
        // Clear container and add selected IDs
        const container = document.getElementById('selectedIdsContainer');
        container.innerHTML = '';
        
        Array.from(checkedBoxes).forEach(checkbox => {
            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = 'selectedIds';
            input.value = checkbox.value;
            container.appendChild(input);
        });

        document.getElementById('bulkDeleteForm').action = '/Admin/BulkDeleteWorkOrders';
        document.getElementById('bulkDeleteForm').submit();
    }
}

// Archive work order function
function archiveWorkOrder(id, name) {
    if (confirm(`Are you sure you want to archive work order "${name}"?\n\nArchived work orders are hidden from the default view but can be restored later.`)) {
        fetch('/Admin/ArchiveWorkOrder', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
            },
            body: 'id=' + encodeURIComponent(id)
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                location.reload();
            } else {
                alert('Error: ' + data.message);
            }
        })
        .catch(error => {
            console.error('Error:', error);
            alert('An error occurred while archiving the work order.');
        });
    }
}

// Unarchive work order function
function unarchiveWorkOrder(id, name) {
    if (confirm(`Are you sure you want to restore work order "${name}" from archive?`)) {
        fetch('/Admin/UnarchiveWorkOrder', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
            },
            body: 'id=' + encodeURIComponent(id)
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                location.reload();
            } else {
                alert('Error: ' + data.message);
            }
        })
        .catch(error => {
            console.error('Error:', error);
            alert('An error occurred while unarchiving the work order.');
        });
    }
}

// Toggle archive filter
function toggleArchiveFilter() {
    const includeArchived = document.getElementById('includeArchivedToggle').checked;
    const currentUrl = new URL(window.location);
    currentUrl.searchParams.set('includeArchived', includeArchived);
    window.location.href = currentUrl.toString();
}

// ===== Admin Import Functions =====

// Global import variables
let currentSessionId = null;
let signalRConnection = null;

// Initialize import functionality
function initializeImportPage() {
    if (!document.getElementById('dropZone')) return; // Not on import page
    
    // DOM elements
    const dropZone = document.getElementById('dropZone');
    const fileInput = document.getElementById('sdfFile');
    const fileInfo = document.getElementById('fileInfo');
    const fileName = document.getElementById('fileName');
    const startImportBtn = document.getElementById('startImportBtn');
    
    // Steps
    const uploadStep = document.getElementById('uploadStep');
    const progressStep = document.getElementById('progressStep');
    const previewStep = document.getElementById('previewStep');
    
    // Progress elements
    const progressBar = document.getElementById('progressBar');
    const currentStage = document.getElementById('currentStage');
    const timeRemaining = document.getElementById('timeRemaining');
    
    // Preview elements
    const sessionIdSpan = document.getElementById('sessionId');
    const workOrderNameInput = document.getElementById('workOrderNameInput');
    const totalProducts = document.getElementById('totalProducts');
    const totalParts = document.getElementById('totalParts');
    const totalDetachedProducts = document.getElementById('totalDetachedProducts');
    const totalHardware = document.getElementById('totalHardware');
    const totalNestSheets = document.getElementById('totalNestSheets');
    const finalImportBtn = document.getElementById('finalImportBtn');
    const startOverBtn = document.getElementById('startOverBtn');

    // Initialize SignalR and file upload
    initializeSignalR();
    initializeFileUpload();
    
    // Set current date
    const previewDate = document.getElementById('preview-date');
    if (previewDate) {
        previewDate.textContent = new Date().toLocaleString();
    }

    // Initialize SignalR connection
    async function initializeSignalR() {
        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl("/importProgress")
            .build();

        signalRConnection.on("ImportProgress", function (data) {
            updateProgress(data.percentage, data.stage, data.estimatedTimeRemaining);
        });

        signalRConnection.on("ImportComplete", function (data) {
            handleImportComplete(data);
        });

        signalRConnection.on("ImportError", function (data) {
            handleImportError(data.error);
        });

        try {
            await signalRConnection.start();
        } catch (err) {
            console.error("SignalR connection failed: " + err);
        }
    }

    // Initialize file upload handlers
    function initializeFileUpload() {
        dropZone.addEventListener('click', () => fileInput.click());
        
        dropZone.addEventListener('dragover', (e) => {
            e.preventDefault();
            dropZone.classList.add('border-primary');
        });

        dropZone.addEventListener('dragleave', (e) => {
            e.preventDefault();
            dropZone.classList.remove('border-primary');
        });

        dropZone.addEventListener('drop', (e) => {
            e.preventDefault();
            dropZone.classList.remove('border-primary');
            
            const files = e.dataTransfer.files;
            if (files.length > 0 && files[0].name.toLowerCase().endsWith('.sdf')) {
                fileInput.files = files;
                showFileInfo(files[0]);
            } else {
                alert('Please select an SDF file.');
            }
        });

        fileInput.addEventListener('change', (e) => {
            if (e.target.files[0]) {
                showFileInfo(e.target.files[0]);
            }
        });
    }

    function showFileInfo(file) {
        fileName.textContent = file.name + ' (' + formatBytes(file.size) + ')';
        fileInfo.classList.remove('d-none');
        startImportBtn.disabled = false;
    }

    function formatBytes(bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }

    // Start import process
    startImportBtn.addEventListener('click', async () => {
        const file = fileInput.files[0];
        if (!file) {
            alert('Please select a file.');
            return;
        }

        // INSTANT UI FEEDBACK - Disable button and show progress immediately
        startImportBtn.disabled = true;
        startImportBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Processing...';
        
        // Show progress step immediately with upload stage
        uploadStep.classList.add('d-none');
        progressStep.classList.remove('d-none');
        updateProgress(0, 'Uploading file...', null);

        try {
            // Upload file to new import endpoint
            const formData = new FormData();
            formData.append('file', file);

            const uploadResponse = await fetch('/admin/import/upload', {
                method: 'POST',
                body: formData
            });

            if (!uploadResponse.ok) {
                const error = await uploadResponse.json();
                throw new Error(error.error || 'Upload failed');
            }

            const uploadResult = await uploadResponse.json();
            currentSessionId = uploadResult.sessionId;
            
            // Update progress after successful upload
            updateProgress(30, 'File uploaded, connecting...', null);

            // Join SignalR group
            if (signalRConnection) {
                await signalRConnection.invoke("JoinImportGroup", currentSessionId);
            }
            
            // Update progress after SignalR connection
            updateProgress(50, 'Starting import process...', null);

            // Start import with fast endpoint
            const startResponse = await fetch('/admin/import/faststart', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ sessionId: currentSessionId })
            });

            if (!startResponse.ok) {
                const error = await startResponse.json();
                throw new Error(error.error || 'Failed to start import');
            }
            
            // FastImportService will take over progress reporting from here
            updateProgress(70, 'Processing SDF file...', null);

        } catch (error) {
            console.error('Import error:', error);
            alert('Error: ' + error.message);
            resetToUpload();
        }
    });

    // Update progress display
    function updateProgress(percentage, stage, estimatedTime) {
        progressBar.style.width = percentage + '%';
        progressBar.textContent = percentage + '%';
        currentStage.textContent = stage || 'Processing...';
        
        if (estimatedTime && estimatedTime > 0) {
            const minutes = Math.floor(estimatedTime / 60);
            const seconds = Math.floor(estimatedTime % 60);
            timeRemaining.textContent = minutes + 'm ' + seconds + 's';
        } else {
            timeRemaining.textContent = 'Calculating...';
        }
    }

    // Handle import completion
    async function handleImportComplete(data) {
        progressStep.classList.add('d-none');
        previewStep.classList.remove('d-none');
        
        // Update statistics
        if (data.statistics) {
            totalProducts.textContent = data.statistics.totalProducts || 0;
            totalParts.textContent = data.statistics.totalParts || 0;
            totalDetachedProducts.textContent = data.statistics.totalDetachedProducts || 0;
            totalHardware.textContent = data.statistics.totalHardware || 0;
            totalNestSheets.textContent = data.statistics.totalNestSheets || 0;
        }
        
        // Get detailed data
        try {
            const statusResponse = await fetch('/admin/import/status?sessionId=' + currentSessionId);
            if (statusResponse.ok) {
                const statusData = await statusResponse.json();
                sessionIdSpan.textContent = statusData.sessionId;
                workOrderNameInput.value = statusData.workOrderName || 'New Import Work Order';
            }
            
            // Initialize tree view
            initializeTreeView();
        } catch (error) {
            console.error('Error fetching import details:', error);
        }
    }

    // Initialize tree view with imported data
    async function initializeTreeView() {
        // The tree is now handled by the _WorkOrderTreeView partial
        try {
            // Update the sessionId in the tree view partial
            if (window.importTreeView_treeView && window.importTreeView_treeView.updateSessionId) {
                window.importTreeView_treeView.updateSessionId(currentSessionId);
            }
            
            if (window.importTreeView_treeView && window.importTreeView_treeView.getInstance()) {
                const treeInstance = window.importTreeView_treeView.getInstance();
                
                // Load tree data from import session
                const response = await fetch(`/admin/import/tree?sessionId=${currentSessionId}`);
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
                
                const treeData = await response.json();
                treeInstance.setData(treeData);
            }
            
            // Enable final import button
            finalImportBtn.disabled = false;
        } catch (error) {
            console.error('Error loading tree data:', error);
            const treeContainer = document.getElementById('importTreeView');
            if (treeContainer) {
                treeContainer.innerHTML = `
                    <div class="alert alert-danger">
                        <i class="fas fa-exclamation-triangle me-2"></i>
                        Failed to load import data: ${error.message}
                    </div>`;
            }
        }
    }

    // Handle import error
    function handleImportError(error) {
        alert('Import failed: ' + error);
        resetToUpload();
    }

    // Reset to upload step
    function resetToUpload() {
        uploadStep.classList.remove('d-none');
        progressStep.classList.add('d-none');
        previewStep.classList.add('d-none');
        
        fileInput.value = '';
        fileInfo.classList.add('d-none');
        startImportBtn.disabled = true;
        startImportBtn.innerHTML = '<i class="fas fa-play me-2"></i>Start Import Process';
        
        currentSessionId = null;
    }

    // Start over button
    startOverBtn.addEventListener('click', () => {
        if (confirm('Are you sure you want to start over?')) {
            resetToUpload();
        }
    });

    // Final import button - Phase I4: Convert to database
    finalImportBtn.addEventListener('click', async () => {
        if (!confirm('Are you sure you want to import this data to the database?')) {
            return;
        }

        try {
            // Disable button and show loading state
            finalImportBtn.disabled = true;
            finalImportBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Importing...';

            // Get current work order name for import (from editable input)
            const workOrderName = workOrderNameInput.value || 'New Import Work Order';

            // Call the conversion endpoint
            const response = await fetch('/admin/import/convert', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    sessionId: currentSessionId,
                    workOrderName: workOrderName
                })
            });

            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.error || 'Import failed');
            } else {
                const result = await response.json();
                
                // Show success message
                showSuccessMessage(result);
                
                // Redirect to work orders page after a delay
                setTimeout(() => {
                    window.location.href = '/admin';
                }, 1000);
            }
        } catch (error) {
            console.error('Import error:', error);
            alert('Error during import: ' + error.message);
        } finally {
            // Re-enable button
            finalImportBtn.disabled = false;
            finalImportBtn.innerHTML = '<i class="fas fa-check me-2"></i>Import to Database';
        }
    });



    // Show success message
    function showSuccessMessage(result) {
        const successAlert = document.createElement('div');
        successAlert.className = 'alert alert-success alert-dismissible fade show';
        successAlert.innerHTML = `
            <i class="fas fa-check-circle me-2"></i>
            <strong>Import Successful!</strong> Work Order "${result.workOrderId}" has been imported to the database.
            <br><small>Products: ${result.statistics.convertedProducts}, Parts: ${result.statistics.convertedParts}, 
            Hardware: ${result.statistics.convertedHardware}</small>
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;
        
        // Insert at the top of the preview step
        previewStep.insertBefore(successAlert, previewStep.firstChild);
    }
}

// ===== Initialization =====

document.addEventListener('DOMContentLoaded', function() {
    // Initialize select all checkbox functionality
    const selectAllCheckbox = document.getElementById('selectAll');
    if (selectAllCheckbox) {
        selectAllCheckbox.addEventListener('change', function() {
            const checkboxes = document.querySelectorAll('.work-order-checkbox');
            checkboxes.forEach(checkbox => {
                checkbox.checked = this.checked;
            });
            updateBulkActions();
        });
    }

    // Initialize bulk actions on page load
    updateBulkActions();
    
    // Initialize import page if on import page
    initializeImportPage();
});