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