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
    
    // Initialize drag-drop functionality
    initializeTimelineDragDrop(projectId);
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

// File management functions have been moved to timeline-files.js

// Purchase Order management functions have been moved to timeline-purchases.js

// Work Order management functions have been moved to timeline-workorders.js

// ==================== DRAG-DROP FUNCTIONALITY ====================

// Initialize drag-drop functionality for timeline reordering
function initializeTimelineDragDrop(projectId) {
    const timelineContainer = document.getElementById(`timeline-container-${projectId}`);
    if (!timelineContainer) return;
    
    // Initialize TaskBlock reordering
    initializeTaskBlockReordering(projectId);
    
    // Initialize event reordering within blocks
    initializeEventReordering(projectId);
    
    // Initialize unblocked events drag-drop
    initializeUnblockedEventsDragDrop(projectId);
}

// Initialize TaskBlock drag-drop reordering
function initializeTaskBlockReordering(projectId) {
    // Find the inner timeline container where TaskBlocks actually live
    const innerTimeline = document.getElementById(`timeline-${projectId}`);
    if (!innerTimeline) return;
    
    new Sortable(innerTimeline, {
        draggable: '.task-block', // Only TaskBlocks are draggable
        handle: '.block-drag-handle',
        ghostClass: 'sortable-ghost',
        chosenClass: 'sortable-chosen',
        dragClass: 'sortable-drag',
        animation: 150,
        onEnd: function(evt) {
            // Get all TaskBlock IDs in new order
            const blockElements = innerTimeline.querySelectorAll('.task-block[data-block-id]');
            const blockIds = Array.from(blockElements).map(el => el.dataset.blockId);
            
            // Call API to persist the new order
            reorderTaskBlocks(projectId, blockIds);
        }
    });
}

// Initialize event drag-drop reordering within TaskBlocks
function initializeEventReordering(projectId) {
    // Find the inner timeline container where TaskBlocks actually live
    const innerTimeline = document.getElementById(`timeline-${projectId}`);
    if (!innerTimeline) return;
    
    // Initialize sortable for each TaskBlock's events container
    const taskBlocks = innerTimeline.querySelectorAll('.task-block[data-block-id]');
    taskBlocks.forEach(taskBlock => {
        const blockId = taskBlock.dataset.blockId;
        const eventsContainer = taskBlock.querySelector('.task-block-events');
        if (!eventsContainer) return;
        
        new Sortable(eventsContainer, {
            handle: '.event-drag-handle',
            ghostClass: 'sortable-ghost',
            chosenClass: 'sortable-chosen', 
            dragClass: 'sortable-drag',
            animation: 150,
            group: `events-${projectId}`, // Allow drag between blocks
            onEnd: function(evt) {
                // Get all event IDs in new order for this block
                const eventElements = eventsContainer.querySelectorAll('[data-event-id]');
                const eventIds = Array.from(eventElements).map(el => el.dataset.eventId);
                
                // If events were moved between blocks, handle assignment
                if (evt.from !== evt.to) {
                    const targetBlock = evt.to.closest('[data-block-id]');
                    const targetBlockId = targetBlock?.dataset.blockId;
                    
                    if (targetBlockId) {
                        // Move event to different block
                        const movedEventId = evt.item.dataset.eventId;
                        assignEventsToBlock(targetBlockId, [movedEventId])
                            .then(() => {
                                // After assignment, reorder events in the target block
                                const targetEventElements = evt.to.querySelectorAll('[data-event-id]');
                                const targetEventIds = Array.from(targetEventElements).map(el => el.dataset.eventId);
                                return reorderEventsInBlock(targetBlockId, targetEventIds);
                            })
                            .then(() => {
                                showNotification('Event moved and reordered successfully', 'success');
                            })
                            .catch(error => {
                                console.error('Error moving event between blocks:', error);
                                showNotification('Error moving event between blocks', 'error');
                                // Reload timeline to reset state
                                loadTimelineForProject(projectId);
                            });
                    }
                } else {
                    // Same block reordering
                    reorderEventsInBlock(blockId, eventIds);
                }
            }
        });
    });
}

// Initialize drag-drop for unblocked events
function initializeUnblockedEventsDragDrop(projectId) {
    const unblockedEventsContainer = document.querySelector(`#timeline-container-${projectId} .unblocked-events`);
    if (!unblockedEventsContainer) return;
    
    new Sortable(unblockedEventsContainer, {
        draggable: '.unblocked-event',
        handle: '.event-drag-handle',
        ghostClass: 'sortable-ghost',
        chosenClass: 'sortable-chosen',
        dragClass: 'sortable-drag',
        animation: 150,
        group: `events-${projectId}`, // Same group as blocked events to allow cross-container dragging
        onEnd: function(evt) {
            // Check if event was dropped into a TaskBlock
            const targetBlock = evt.to.closest('[data-block-id]');
            if (targetBlock) {
                const targetBlockId = targetBlock.dataset.blockId;
                const movedEventId = evt.item.dataset.eventId;
                
                // Assign the event to the target block
                assignEventsToBlock(targetBlockId, [movedEventId])
                    .then(() => {
                        showNotification('Event assigned to block successfully', 'success');
                        loadTimelineForProject(projectId);
                    })
                    .catch(error => {
                        console.error('Error assigning event to block:', error);
                        showNotification('Error assigning event to block', 'error');
                        loadTimelineForProject(projectId);
                    });
            }
        }
    });
}

// API call to reorder TaskBlocks
function reorderTaskBlocks(projectId, blockIds) {
    const requestData = {
        ProjectId: projectId,
        BlockIds: blockIds
    };
    
    fetch('/Timeline/ReorderBlocks', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestData)
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification('Task blocks reordered successfully', 'success');
        } else {
            showNotification(data.message || 'Error reordering task blocks', 'error');
            // Reload timeline to reset state
            loadTimelineForProject(projectId);
        }
    })
    .catch(error => {
        console.error('Error reordering task blocks:', error);
        showNotification('Network error occurred', 'error');
        // Reload timeline to reset state  
        loadTimelineForProject(projectId);
    });
}

// API call to reorder events within a TaskBlock
function reorderEventsInBlock(blockId, eventIds) {
    const requestData = {
        BlockId: blockId,
        EventIds: eventIds
    };
    
    fetch('/Timeline/ReorderEventsInBlock', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestData)
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification('Events reordered successfully', 'success');
        } else {
            showNotification(data.message || 'Error reordering events', 'error');
        }
    })
    .catch(error => {
        console.error('Error reordering events:', error);
        showNotification('Network error occurred', 'error');
    });
}

// Comment functionality
function showAddComment(projectId) {
    const modal = new bootstrap.Modal(document.getElementById(`addCommentModal-${projectId}`));
    modal.show();
}

function saveComment(projectId) {
    const commentText = document.getElementById(`commentText-${projectId}`).value.trim();
    const commentDate = document.getElementById(`commentDate-${projectId}`).value;
    const commentAuthor = document.getElementById(`commentAuthor-${projectId}`).value.trim();
    
    if (!commentText) {
        showNotification('Comment text is required', 'error');
        return;
    }
    
    if (!commentDate) {
        showNotification('Date is required', 'error');
        return;
    }
    
    const modal = bootstrap.Modal.getInstance(document.getElementById(`addCommentModal-${projectId}`));
    
    fetch(`/Project/CreateComment`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Requested-With': 'XMLHttpRequest'
        },
        body: JSON.stringify({
            projectId: projectId,
            description: commentText,
            eventDate: commentDate,
            createdBy: commentAuthor || null
        })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            modal.hide();
            // Clear form
            document.getElementById(`commentText-${projectId}`).value = '';
            document.getElementById(`commentDate-${projectId}`).value = new Date().toISOString().slice(0, 16);
            document.getElementById(`commentAuthor-${projectId}`).value = '';
            
            showNotification('Comment added successfully', 'success');
            loadTimelineForProject(projectId);
        } else {
            showNotification(data.message || 'Error adding comment', 'error');
        }
    })
    .catch(error => {
        console.error('Error saving comment:', error);
        showNotification('Network error occurred', 'error');
    });
}

// Attachment comment editing functionality
var currentEditingEventId = null;

function editAttachmentComment(eventId, currentDescription, projectId) {
    currentEditingEventId = eventId;
    
    // Set the current description in the textarea
    document.getElementById(`editAttachmentCommentText-${projectId}`).value = currentDescription || '';
    
    // Show the modal
    const modal = new bootstrap.Modal(document.getElementById(`editAttachmentCommentModal-${projectId}`));
    modal.show();
}

function saveAttachmentComment(projectId) {
    if (!currentEditingEventId) {
        showNotification('No event selected for editing', 'error');
        return;
    }
    
    const description = document.getElementById(`editAttachmentCommentText-${projectId}`).value.trim();
    const modal = bootstrap.Modal.getInstance(document.getElementById(`editAttachmentCommentModal-${projectId}`));
    
    fetch(`/Project/UpdateEventDescription`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Requested-With': 'XMLHttpRequest'
        },
        body: JSON.stringify({
            eventId: currentEditingEventId,
            description: description
        })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            modal.hide();
            // Clear form and reset state
            document.getElementById(`editAttachmentCommentText-${projectId}`).value = '';
            currentEditingEventId = null;
            
            showNotification('Comment updated successfully', 'success');
            loadTimelineForProject(projectId);
        } else {
            showNotification(data.message || 'Error updating comment', 'error');
        }
    })
    .catch(error => {
        console.error('Error updating attachment comment:', error);
        showNotification('Network error occurred', 'error');
    });
}