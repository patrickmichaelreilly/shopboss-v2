// Timeline Files Management Module
// Extracted from timeline.js for better maintainability

(function(Timeline) {
    Timeline.Files = Timeline.Files || {};

    // Direct file upload without filename preview
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
    };

    // Delete file
    Timeline.Files.deleteFile = function(fileId, projectId) {
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
    };

})(window.Timeline = window.Timeline || {});

// Backward compatibility - expose functions globally for existing code
function uploadFilesDirectly(projectId, fileInput) {
    return Timeline.Files.uploadFilesDirectly(projectId, fileInput);
}

function updateFileCategory(fileId, category) {
    return Timeline.Files.updateFileCategory(fileId, category);
}

function deleteFile(fileId, projectId) {
    return Timeline.Files.deleteFile(fileId, projectId);
}