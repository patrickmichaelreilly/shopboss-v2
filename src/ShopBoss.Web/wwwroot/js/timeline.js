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

    // Initialize delegated event handlers (no inline JS)
    initializeDelegatedTimelineEvents(projectId);
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
    
    (typeof apiPostJson === 'function' ? apiPostJson('/Timeline/CreateBlock', requestData) : fetch('/Timeline/CreateBlock', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(requestData) }).then(r => r.json()))
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

// Create a nested TaskBlock
function createNestedTaskBlock(projectId, parentBlockId) {
    const blockName = prompt('Enter name for new nested block:');
    if (blockName && blockName.trim()) {
        const requestData = { ProjectId: projectId, Name: blockName.trim(), Description: null };
        
        (typeof apiPostJson === 'function' ? apiPostJson('/Timeline/CreateBlock', requestData) : fetch('/Timeline/CreateBlock', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(requestData) }).then(r => r.json()))
        .then(data => {
            if (data.success) {
                // Nest the new block under the parent
                return fetch('/Timeline/NestTaskBlock', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ ChildBlockId: data.block.Id, ParentBlockId: parentBlockId })
                }).then(r => r.json());
            } else {
                throw new Error(data.message || 'Error creating block');
            }
        })
        .then(data => {
            if (data.success) {
                showNotification('Nested block created successfully', 'success');
                loadTimelineForProject(projectId);
            } else {
                showNotification(data.message || 'Error nesting block', 'error');
            }
        })
        .catch(error => {
            console.error('Error creating nested task block:', error);
            showNotification('Network error occurred', 'error');
        });
    }
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
    
    return (typeof apiPostJson === 'function' ? apiPostJson('/Timeline/AssignEvents', requestData) : fetch('/Timeline/AssignEvents', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(requestData) }).then(r => r.json()))
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

// Delegated event handlers for timeline interactions
function initializeDelegatedTimelineEvents(projectId) {
    const timelineContainer = document.getElementById(`timeline-container-${projectId}`);
    if (!timelineContainer) return;

    // Save attachment comment on change/blur without full timeline reload
    const saveComment = (target) => {
        const eventId = target.getAttribute('data-event-id');
        const description = target.value.trim();
        if (!eventId) return;

        updateEventDescription(eventId, description)
            .then((ok) => {
                if (ok) {
                    showNotification('Comment updated', 'success');
                } else {
                    showNotification('Error updating comment', 'error');
                }
            })
            .catch(() => showNotification('Network error occurred', 'error'));
    };

    // Use input "change" (fires on blur) and also catch explicit focusout
    timelineContainer.addEventListener('change', (e) => {
        const target = e.target;
        if (target && target.matches('input[data-action="attachment-comment-input"]')) {
            saveComment(target);
        }
    });

    timelineContainer.addEventListener('focusout', (e) => {
        const target = e.target;
        if (target && target.matches('input[data-action="attachment-comment-input"]')) {
            // In case some browsers don’t fire change
            saveComment(target);
        }
    });

    // Optional: Save on Enter without blurring
    timelineContainer.addEventListener('keydown', (e) => {
        const target = e.target;
        if (target && target.matches('input[data-action="attachment-comment-input"]')) {
            if (e.key === 'Enter') {
                e.preventDefault();
                saveComment(target);
                target.blur();
            }
        }
    });
}

// Ensure a single global delegated action handler is registered
if (!window.__globalActionDelegationInitialized) {
    window.__globalActionDelegationInitialized = true;

    // Global click delegation for elements with data-action
    document.addEventListener('click', function(e) {
        const el = e.target.closest('[data-action]');
        if (!el) return;

        const action = el.getAttribute('data-action');
        const projectId = el.getAttribute('data-project-id');
        const blockId = el.getAttribute('data-block-id');
        if (!action) return;

        switch (action) {
            // Project Index actions
            case 'clear-project-form':
                if (typeof clearProjectForm === 'function') clearProjectForm();
                break;
            case 'open-smartsheet-import':
                if (typeof openSmartSheetImportModal === 'function') openSmartSheetImportModal();
                break;
            case 'toggle-project':
                if (typeof toggleProject === 'function') toggleProject(projectId);
                break;
            case 'unarchive-project':
                if (typeof unarchiveProject === 'function') unarchiveProject(projectId);
                break;
            case 'archive-project':
                if (typeof archiveProject === 'function') archiveProject(projectId);
                break;
            case 'delete-project':
                if (typeof deleteProject === 'function') deleteProject(projectId, el);
                break;
            case 'save-project':
                if (typeof saveProject === 'function') saveProject();
                break;
            case 'associate-selected-work-orders':
                if (window.Timeline?.WorkOrders?.associateSelectedWorkOrders) Timeline.WorkOrders.associateSelectedWorkOrders();
                break;
            case 'save-purchase-order':
                if (window.Timeline?.Purchases?.savePurchaseOrder) Timeline.Purchases.savePurchaseOrder();
                break;
            case 'save-purchase-order-edit':
                if (window.Timeline?.Purchases?.savePurchaseOrderEdit) Timeline.Purchases.savePurchaseOrderEdit();
                break;
            case 'save-custom-work-order':
                if (window.Timeline?.WorkOrders?.saveCustomWorkOrder) Timeline.WorkOrders.saveCustomWorkOrder();
                break;
            case 'save-custom-work-order-edit':
                if (window.Timeline?.WorkOrders?.saveCustomWorkOrderEdit) Timeline.WorkOrders.saveCustomWorkOrderEdit();
                break;
            case 'refresh-page':
                window.location.reload();
                break;
            case 'project-edit':
                if (typeof editProject === 'function') editProject(projectId);
                break;
            case 'project-save':
                if (typeof saveProjectEdit === 'function') saveProjectEdit(projectId);
                break;
            case 'project-cancel':
                if (typeof cancelProjectEdit === 'function') cancelProjectEdit(projectId);
                break;
            case 'unlink-smartsheet':
                if (typeof unlinkSmartSheet === 'function') unlinkSmartSheet(projectId);
                break;
            case 'link-smartsheet':
                if (typeof showSmartSheetLinking === 'function') showSmartSheetLinking(projectId);
                break;
            case 'show-upload-file-modal':
                if (window.Timeline?.Files?.showUploadFileModal) Timeline.Files.showUploadFileModal(projectId, blockId);
                break;
            case 'show-create-purchase-order':
                if (window.Timeline?.Purchases?.showCreatePurchaseOrder) Timeline.Purchases.showCreatePurchaseOrder(projectId, blockId);
                break;
            case 'show-associate-work-orders':
                if (window.Timeline?.WorkOrders?.showAssociateWorkOrders) Timeline.WorkOrders.showAssociateWorkOrders(projectId, blockId);
                break;
            case 'show-create-custom-work-order':
                if (window.Timeline?.WorkOrders?.showCreateCustomWorkOrder) Timeline.WorkOrders.showCreateCustomWorkOrder(projectId, blockId);
                break;
            case 'show-add-comment':
                if (typeof showAddComment === 'function') showAddComment(projectId, blockId);
                break;
            case 'show-create-task-block':
                if (typeof showCreateTaskBlock === 'function') showCreateTaskBlock(projectId);
                break;
            case 'save-comment':
                if (typeof saveComment === 'function') saveComment(projectId);
                break;
            case 'upload-files-with-comment':
                if (window.Timeline?.Files?.uploadFilesWithComment) Timeline.Files.uploadFilesWithComment(projectId);
                break;
            case 'save-attachment-comment':
                if (typeof saveAttachmentComment === 'function') saveAttachmentComment(projectId);
                break;
            default:
                break;
        }
    }, true);

    // Global change delegation for inputs/selects that need element reference
    document.addEventListener('change', function(e) {
        const el = e.target;
        if (!el || !el.matches('[data-action]')) return;
        const action = el.getAttribute('data-action');
        const projectId = el.getAttribute('data-project-id');
        if (action === 'upload-files-direct' && window.Timeline?.Files?.uploadFilesDirectly) {
            Timeline.Files.uploadFilesDirectly(projectId, el);
        }
        if (action === 'project-filter-category' && typeof filterByCategory === 'function') {
            filterByCategory(el.value);
        }
        if (action === 'project-toggle-archived' && typeof toggleArchiveFilter === 'function') {
            toggleArchiveFilter();
        }
    }, true);
}

// Helper to update event description via API
function updateEventDescription(eventId, description) {
    if (typeof apiPostJson === 'function') {
        return apiPostJson('/Project/UpdateEventDescription', {
            eventId: eventId,
            description: description
        }).then(res => !!res.success).catch(() => false);
    }
    // Fallback if http-utils.js not loaded
    return fetch(`/Project/UpdateEventDescription`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Requested-With': 'XMLHttpRequest'
        },
        body: JSON.stringify({
            eventId: eventId,
            description: description
        })
    })
    .then(r => r.json())
    .then(data => Boolean(data && data.success))
    .catch(() => false);
}

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
    
    // Initialize sortable for main timeline
    initializeSortableContainer(innerTimeline, projectId);
    
    // Initialize sortable for all TaskBlock content containers (now unified)
    const taskBlockContainers = innerTimeline.querySelectorAll('.task-block-content');
    taskBlockContainers.forEach(container => {
        initializeSortableContainer(container, projectId);
    });
}

// Initialize sortable functionality for a specific container
function initializeSortableContainer(container, projectId) {
    new Sortable(container, {
        draggable: '.task-block, .timeline-event', // Include all timeline events, not just unblocked ones
        handle: '.block-drag-handle, .event-drag-handle',
        ghostClass: 'sortable-ghost',
        chosenClass: 'sortable-chosen',
        dragClass: 'sortable-drag',
        animation: 150,
        group: {
            name: `timeline-${projectId}`, // Unified group for all containers
            pull: true,
            put: true
        },
        onEnd: function(evt) {
            const draggedElement = evt.item;
            const fromContainer = evt.from;
            const toContainer = evt.to;
            
            // Handle TaskBlock nesting
            if (draggedElement.classList.contains('task-block')) {
                const draggedBlockId = draggedElement.dataset.blockId;
                
                // Check if moved between different containers (nesting/unnesting)
                if (fromContainer !== toContainer) {
                    let targetParentId = null;
                    
                    // If moved to a task-block-content container, find the parent TaskBlock
                    if (toContainer.classList.contains('task-block-content')) {
                        const parentBlock = toContainer.closest('.task-block[data-block-id]');
                        if (parentBlock) {
                            targetParentId = parentBlock.dataset.blockId;
                        }
                    }
                    
                    // Nest or unnest the block
                    nestTaskBlock(draggedBlockId, targetParentId)
                        .then(() => {
                            const action = targetParentId ? 'nested' : 'unnested';
                            showNotification(`TaskBlock ${action} successfully`, 'success');
                            loadTimelineForProject(projectId);
                        })
                        .catch(error => {
                            console.error('Error nesting/unnesting TaskBlock:', error);
                            showNotification('Error moving TaskBlock', 'error');
                            loadTimelineForProject(projectId);
                        });
                }
            }
            
            // Handle Event movement
            if (draggedElement.classList.contains('timeline-event')) {
                const eventId = draggedElement.dataset.eventId;
                let targetBlockId = null;
                
                // Determine target block
                if (toContainer.classList.contains('task-block-content') || toContainer.closest('.task-block-content')) {
                    const parentBlock = toContainer.closest('.task-block[data-block-id]');
                    if (parentBlock) {
                        targetBlockId = parentBlock.dataset.blockId;
                    }
                }
                
                // Handle different event movement scenarios
                if (fromContainer !== toContainer) {
                    // Event moved between containers
                    if (targetBlockId) {
                        // Moved into a TaskBlock
                        assignEventsToBlock(targetBlockId, [eventId])
                            .then(() => {
                                showNotification('Event assigned to block successfully', 'success');
                                loadTimelineForProject(projectId);
                            })
                            .catch(error => {
                                console.error('Error assigning event to block:', error);
                                showNotification('Error assigning event to block', 'error');
                                loadTimelineForProject(projectId);
                            });
                    } else {
                        // Moved out of a TaskBlock to unblocked
                        unassignEventsFromBlocks([eventId])
                            .then(() => {
                                showNotification('Event moved to unblocked section', 'success');
                                loadTimelineForProject(projectId);
                            })
                            .catch(error => {
                                console.error('Error unassigning event:', error);
                                showNotification('Error moving event', 'error');
                                loadTimelineForProject(projectId);
                            });
                    }
                }
            }
            
            // Update mixed ordering if items stayed in same container
            if (fromContainer === toContainer) {
                // Handle mixed ordering within TaskBlocks or at root level
                if (toContainer.classList.contains('task-block-content')) {
                    // Reordering within a TaskBlock - handle mixed events and nested TaskBlocks
                    const parentBlock = toContainer.closest('.task-block[data-block-id]');
                    const parentBlockId = parentBlock?.dataset.blockId;
                    
                    if (parentBlockId) {
                        const allItems = Array.from(toContainer.children);
                        const reorderedItems = [];
                        
                        allItems.forEach((item, index) => {
                            if (item.classList.contains('task-block')) {
                                // Nested TaskBlock
                                reorderedItems.push({
                                    Type: 'TaskBlock',
                                    Id: item.dataset.blockId,
                                    Order: index + 1
                                });
                            } else if (item.classList.contains('timeline-event')) {
                                // Event within the TaskBlock
                                reorderedItems.push({
                                    Type: 'Event',
                                    Id: item.dataset.eventId,
                                    Order: index + 1
                                });
                            }
                        });
                        
                        if (reorderedItems.length > 0) {
                            reorderMixedTimelineItems(projectId, reorderedItems);
                        }
                    }
                } else {
                    // Reordering at root timeline level
                    const allItems = Array.from(toContainer.children);
                    const reorderedItems = [];
                    
                    allItems.forEach((item, index) => {
                        if (item.classList.contains('task-block')) {
                            reorderedItems.push({
                                Type: 'TaskBlock',
                                Id: item.dataset.blockId,
                                Order: index + 1
                            });
                        } else if (item.classList.contains('timeline-event')) {
                            reorderedItems.push({
                                Type: 'Event',
                                Id: item.dataset.eventId,
                                Order: index + 1
                            });
                        }
                    });
                    
                    if (reorderedItems.length > 0) {
                        reorderMixedTimelineItems(projectId, reorderedItems);
                    }
                }
            }
        }
    });
}

// Initialize event drag-drop reordering within TaskBlocks
// Note: This is now handled by the unified initializeSortableContainer function
// since events and TaskBlocks are mixed in the same containers
function initializeEventReordering(projectId) {
    // This function is deprecated - mixed ordering is now handled
    // by initializeSortableContainer which manages both events and TaskBlocks
    // in the unified .task-block-content containers
}


// API call to reorder TaskBlocks
function reorderTaskBlocks(projectId, blockIds) {
    const requestData = {
        ProjectId: projectId,
        BlockIds: blockIds
    };
    
    (typeof apiPostJson === 'function' ? apiPostJson('/Timeline/ReorderBlocks', requestData) : fetch('/Timeline/ReorderBlocks', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(requestData) }).then(r => r.json()))
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
    
    (typeof apiPostJson === 'function' ? apiPostJson('/Timeline/ReorderEventsInBlock', requestData) : fetch('/Timeline/ReorderEventsInBlock', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(requestData) }).then(r => r.json()))
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
    
    (typeof apiPostJson === 'function' ? apiPostJson('/Project/CreateComment', { projectId, description: commentText, eventDate: commentDate, createdBy: commentAuthor || null }) : fetch('/Project/CreateComment', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }, body: JSON.stringify({ projectId, description: commentText, eventDate: commentDate, createdBy: commentAuthor || null }) }).then(r => r.json()))
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
    
    (typeof apiPostJson === 'function' ? apiPostJson('/Project/UpdateEventDescription', { eventId: currentEditingEventId, description }) : fetch('/Project/UpdateEventDescription', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }, body: JSON.stringify({ eventId: currentEditingEventId, description }) }).then(r => r.json()))
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
    
    (typeof apiPostJson === 'function' ? apiPostJson('/Timeline/ReorderMixedItems', requestData) : fetch('/Timeline/ReorderMixedItems', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(requestData) }).then(r => r.json()))
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

// API call to nest a TaskBlock under another TaskBlock
function nestTaskBlock(childBlockId, parentBlockId) {
    const requestData = {
        ChildBlockId: childBlockId,
        ParentBlockId: parentBlockId
    };
    
    return (typeof apiPostJson === 'function' ? apiPostJson('/Timeline/NestTaskBlock', requestData) : fetch('/Timeline/NestTaskBlock', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(requestData) }).then(r => r.json()))
    .then(data => {
        if (!data.success) {
            throw new Error(data.message || 'Error nesting TaskBlock');
        }
        return data;
    });
}

// API call to unnest a TaskBlock (move to root level)
function unnestTaskBlock(blockId) {
    const requestData = {
        ChildBlockId: blockId,
        ParentBlockId: null // null means move to root
    };
    
    return (typeof apiPostJson === 'function' ? apiPostJson('/Timeline/NestTaskBlock', requestData) : fetch('/Timeline/NestTaskBlock', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(requestData) }).then(r => r.json()))
    .then(data => {
        if (data.success) {
            showNotification('TaskBlock moved to root level', 'success');
            // Reload timeline to reflect changes
            const timelineContainer = document.querySelector('.timeline-container');
            if (timelineContainer) {
                const projectId = timelineContainer.id.replace('timeline-', '');
                loadTimelineForProject(projectId);
            }
        } else {
            showNotification(data.message || 'Error unnesting TaskBlock', 'error');
        }
    })
    .catch(error => {
        console.error('Error unnesting TaskBlock:', error);
        showNotification('Network error occurred', 'error');
    });
}

// API call to unassign events from any TaskBlock (move to unblocked)
function unassignEventsFromBlocks(eventIds) {
    const requestData = {
        EventIds: eventIds
    };
    
    return (typeof apiPostJson === 'function' ? apiPostJson('/Timeline/UnassignEvents', requestData) : fetch('/Timeline/UnassignEvents', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(requestData) }).then(r => r.json()))
    .then(data => {
        if (!data.success) {
            throw new Error(data.message || 'Error unassigning events');
        }
        return data;
    });
}
