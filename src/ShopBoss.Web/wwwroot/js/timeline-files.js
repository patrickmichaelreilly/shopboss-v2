// Timeline Files Management Module
// Extracted from timeline.js for better maintainability

(function(Timeline) {
    Timeline.Files = Timeline.Files || {};

    // Show upload file modal
    Timeline.Files.currentBlockId = null; // Store blockId for file uploads
    
    Timeline.Files.showUploadFileModal = function(projectId, blockId = null) {
        Timeline.Files.currentBlockId = blockId; // Store for use in upload functions
        const modal = new bootstrap.Modal(document.getElementById(`uploadFileModal-${projectId}`));
        modal.show();
    };

    // Upload files with comment from modal
    Timeline.Files.uploadFilesWithComment = function(projectId) {
        const fileInput = document.getElementById(`fileInput-${projectId}`);
        const category = document.getElementById(`fileCategory-${projectId}`).value;
        const comment = document.getElementById(`fileComment-${projectId}`).value.trim();

        if (fileInput.files.length === 0) {
            showNotification('Please select at least one file', 'error');
            return;
        }

        const formData = new FormData();
        formData.append('projectId', projectId);
        formData.append('category', category);
        if (comment) {
            formData.append('comment', comment);
        }
        if (Timeline.Files.currentBlockId) {
            formData.append('taskBlockId', Timeline.Files.currentBlockId);
        }
        
        for (let i = 0; i < fileInput.files.length; i++) {
            formData.append('file', fileInput.files[i]);
        }

        const modal = bootstrap.Modal.getInstance(document.getElementById(`uploadFileModal-${projectId}`));

        fetch('/Project/UploadFile', {
            method: 'POST',
            body: formData
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                modal.hide();
                // Clear form
                fileInput.value = '';
                document.getElementById(`fileCategory-${projectId}`).value = 'Other';
                document.getElementById(`fileComment-${projectId}`).value = '';
                
                showNotification('File uploaded successfully', 'success');
                loadTimelineForProject(projectId);
            } else {
                showNotification(data.message || 'Error uploading files', 'error');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showNotification('Network error occurred', 'error');
        });
    };

    // Direct file upload without filename preview (keep for backward compatibility)
    Timeline.Files.uploadFilesDirectly = function(projectId, fileInput) {
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
    };

    // Update file category
    Timeline.Files.updateFileCategory = function(fileId, category) {
        (typeof apiPostJson === 'function' ? apiPostJson('/Project/UpdateFileCategory', { id: fileId, category: category }) : fetch('/Project/UpdateFileCategory', { method: 'POST', headers: { 'Content-Type': 'application/json', }, body: JSON.stringify({ id: fileId, category: category }) }).then(r => r.json()))
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
    };

    // Delete file
    Timeline.Files.deleteFile = function(fileId, projectId) {
        (typeof apiPostForm === 'function' ? apiPostForm('/Project/DeleteFile', new URLSearchParams({ id: fileId })) : fetch('/Project/DeleteFile', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: `id=${fileId}` }).then(r => r.json()))
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
    };


})(window.Timeline = window.Timeline || {});

// Backward compatibility - expose functions globally for existing code
function uploadFilesDirectly(projectId, fileInput) {
    return Timeline.Files.uploadFilesDirectly(projectId, fileInput);
}

function showUploadFileModal(projectId) {
    return Timeline.Files.showUploadFileModal(projectId);
}

function uploadFilesWithComment(projectId) {
    return Timeline.Files.uploadFilesWithComment(projectId);
}

function updateFileCategory(fileId, category) {
    return Timeline.Files.updateFileCategory(fileId, category);
}

function deleteFile(fileId, projectId) {
    return Timeline.Files.deleteFile(fileId, projectId);
}
