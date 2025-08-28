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
    // Initialize drag-drop functionality
    initializeTimelineDragDrop(projectId);
    
    // Initialize collapse functionality
    initializeTaskBlockCollapse(projectId);
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

// File management functions have been moved to timeline-files.js

// Purchase Order management functions have been moved to timeline-purchases.js

// Work Order management functions have been moved to timeline-workorders.js

// ==================== DRAG-DROP FUNCTIONALITY ====================

// Initialize drag-drop functionality for timeline reordering
function initializeTimelineDragDrop(projectId) {
    const timelineContainer = document.getElementById(`timeline-container-${projectId}`);
    if (!timelineContainer) return;
    
    // Initialize TaskBlock reordering and unblocked event handling
    initializeTaskBlockReordering(projectId);
    
    // Initialize event reordering within blocks
    initializeEventReordering(projectId);
}

// Initialize TaskBlock drag-drop reordering
function initializeTaskBlockReordering(projectId) {
    // Find the inner timeline container where TaskBlocks actually live
    const innerTimeline = document.getElementById(`timeline-${projectId}`);
    if (!innerTimeline) return;
    
    new Sortable(innerTimeline, {
        draggable: '.task-block, .unblocked-event',
        handle: '.block-drag-handle, .event-drag-handle',
        ghostClass: 'sortable-ghost',
        chosenClass: 'sortable-chosen',
        dragClass: 'sortable-drag',
        animation: 150,
        group: {
            name: `events-${projectId}`,
            pull: true,
            put: true
        },
        onEnd: function(evt) {
            // Handle mixed reordering - get all items in new order
            const allItems = Array.from(innerTimeline.children);
            const reorderedItems = [];
            
            allItems.forEach((item, index) => {
                if (item.classList.contains('task-block')) {
                    reorderedItems.push({
                        Type: 'TaskBlock',
                        Id: item.dataset.blockId,
                        Order: index + 1
                    });
                } else if (item.classList.contains('unblocked-event')) {
                    reorderedItems.push({
                        Type: 'Event',
                        Id: item.dataset.eventId,
                        Order: index + 1
                    });
                }
            });
            
            // Call API to update mixed ordering
            reorderMixedTimelineItems(projectId, reorderedItems);
            
            // Check if an unblocked event was dropped into a TaskBlock
            if (evt.item.classList.contains('unblocked-event')) {
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

// ==================== COLLAPSE FUNCTIONALITY ====================

// Initialize TaskBlock collapse functionality with localStorage persistence
function initializeTaskBlockCollapse(projectId) {
    const timelineContainer = document.getElementById(`timeline-container-${projectId}`);
    if (!timelineContainer) return;
    
    // Restore collapsed states from localStorage
    const taskBlocks = timelineContainer.querySelectorAll('.task-block[data-block-id]');
    taskBlocks.forEach(taskBlock => {
        const blockId = taskBlock.dataset.blockId;
        const collapseElement = taskBlock.querySelector(`#collapse-${blockId}`);
        const headerElement = taskBlock.querySelector('.task-block-header');
        
        if (!collapseElement || !headerElement) return;
        
        // Get saved state from localStorage
        const storageKey = `timeline-collapsed-${blockId}`;
        const isCollapsed = localStorage.getItem(storageKey) === 'true';
        
        if (isCollapsed) {
            collapseElement.classList.remove('show');
            headerElement.setAttribute('aria-expanded', 'false');
        }
    });
    
    // Add event listeners for collapse state changes
    timelineContainer.addEventListener('hidden.bs.collapse', function(event) {
        const collapseElement = event.target;
        const blockId = collapseElement.id.replace('collapse-', '');
        const storageKey = `timeline-collapsed-${blockId}`;
        localStorage.setItem(storageKey, 'true');
    });
    
    timelineContainer.addEventListener('shown.bs.collapse', function(event) {
        const collapseElement = event.target;
        const blockId = collapseElement.id.replace('collapse-', '');
        const storageKey = `timeline-collapsed-${blockId}`;
        localStorage.setItem(storageKey, 'false');
    });
}

// API call to reorder mixed timeline items (TaskBlocks and unblocked events)
function reorderMixedTimelineItems(projectId, items) {
    const requestData = {
        ProjectId: projectId,
        Items: items
    };
    
    fetch('/Timeline/ReorderMixedItems', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestData)
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification('Timeline reordered successfully', 'success');
        } else {
            showNotification(data.message || 'Error reordering timeline', 'error');
            // Reload timeline to reset state
            loadTimelineForProject(projectId);
        }
    })
    .catch(error => {
        console.error('Error reordering mixed timeline items:', error);
        showNotification('Network error occurred', 'error');
        // Reload timeline to reset state  
        loadTimelineForProject(projectId);
    });
}