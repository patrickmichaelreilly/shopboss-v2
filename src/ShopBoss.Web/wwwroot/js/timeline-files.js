// Timeline Files Management Module
// Extracted from timeline.js for better maintainability

(function(Timeline) {
    Timeline.Files = Timeline.Files || {};

    // Direct file upload
    Timeline.Files.currentBlockId = null; // Store blockId for file uploads
    
    Timeline.Files.triggerFileUpload = function(projectId, blockId = null) {
        Timeline.Files.currentBlockId = blockId; // Store for use in upload functions
        const fileInput = document.getElementById(`fileInputHidden-${projectId}`);
        if (fileInput) {
            fileInput.click();
        } else {
            console.warn('File input not found for project:', projectId);
        }
    };


    // Direct file upload without filename preview (keep for backward compatibility)
    Timeline.Files.uploadFilesDirectly = function(projectId, fileInput) {
        if (fileInput.files.length === 0) {
            return;
        }

        const formData = new FormData();
        formData.append('projectId', projectId);
        formData.append('label', 'Label'); // Set default label
        
        // Include the taskBlockId if one was specified
        if (Timeline.Files.currentBlockId) {
            formData.append('taskBlockId', Timeline.Files.currentBlockId);
        }
        
        for (let i = 0; i < fileInput.files.length; i++) {
            formData.append('file', fileInput.files[i]);
        }

        apiPostForm('/Project/UploadFile', formData).then(data => {
            if (data.success) {
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

    // Update attachment label
    Timeline.Files.updateAttachmentLabel = function(attachmentId, label) {
        return apiPostJson('/Project/UpdateAttachmentLabel', { id: attachmentId, label: label }).then(data => {
            if (data.success) {
                return true;
            } else {
                showNotification(data.message || 'Error updating label', 'error');
                return false;
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showNotification('Network error occurred', 'error');
            return false;
        });
    };

    // Delete file
    Timeline.Files.deleteFile = function(fileId, projectId) {
        apiPostForm('/Project/DeleteFile', new URLSearchParams({ id: fileId })).then(data => {
            if (data.success) {
                
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

function triggerFileUpload(projectId, blockId) {
    return Timeline.Files.triggerFileUpload(projectId, blockId);
}

function updateAttachmentLabel(attachmentId, label) {
    return Timeline.Files.updateAttachmentLabel(attachmentId, label);
}

function deleteFile(fileId, projectId) {
    return Timeline.Files.deleteFile(fileId, projectId);
}
