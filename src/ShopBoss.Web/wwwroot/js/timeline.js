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
            setTimeout(() => {
                initializeTimelineInteractions(projectId);
                // Initialize SmartSheet sync UI if function exists
                if (typeof initializeSmartSheetSyncUI === 'function') {
                    initializeSmartSheetSyncUI(projectId);
                }
            }, 100);
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
    
    apiPostJson('/Timeline/CreateBlock', requestData)
    .then(data => {
        if (data.success) {
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
        
        apiPostJson('/Timeline/CreateBlock', requestData)
        .then(res => {
            if (!res.success) {
                throw new Error(res.message || 'Error creating block');
            }
            const createdBlock = (res.data && res.data.block) || res.block || null;
            const newBlockId = createdBlock && (createdBlock.Id || createdBlock.id);
            if (!newBlockId) {
                throw new Error('CreateBlock response missing block Id');
            }
            // Nest the new block under the parent using existing helper
            return nestTaskBlock(newBlockId, parentBlockId);
        })
        .then(data => {
            if (data.success) {
                loadTimelineForProject(projectId);
                // Update visual nesting indicators after timeline reload
                setTimeout(() => updateTaskBlockNestingVisuals(projectId), 100);
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

// Collapse all blocks in project timeline
function collapseAllBlocks(projectId) {
    const timelineContainer = document.getElementById(`timeline-container-${projectId}`);
    if (!timelineContainer) return;
    
    const collapseElements = timelineContainer.querySelectorAll('.task-block .collapse.show');
    collapseElements.forEach(element => {
        const bsCollapse = new bootstrap.Collapse(element, { toggle: false });
        bsCollapse.hide();
    });
}

// Expand all blocks in project timeline  
function expandAllBlocks(projectId) {
    const timelineContainer = document.getElementById(`timeline-container-${projectId}`);
    if (!timelineContainer) return;
    
    const collapseElements = timelineContainer.querySelectorAll('.task-block .collapse:not(.show)');
    collapseElements.forEach(element => {
        const bsCollapse = new bootstrap.Collapse(element, { toggle: false });
        bsCollapse.show();
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
    
    apiPutJson('/Timeline/UpdateBlock', requestData)
    .then(data => {
        if (data.success) {
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
    
    apiDeleteJson(`/Timeline/DeleteBlock?blockId=${blockId}`)
    .then(data => {
        if (data.success) {
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
    
    return apiPostJson('/Timeline/AssignEvents', requestData)
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
                if (window.Timeline?.Files?.triggerFileUpload) Timeline.Files.triggerFileUpload(projectId, blockId);
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
            case 'create-nested-task-block':
                if (typeof createNestedTaskBlock === 'function') createNestedTaskBlock(projectId, blockId);
                break;
            case 'collapse-all-blocks':
                if (typeof collapseAllBlocks === 'function') collapseAllBlocks(projectId);
                break;
            case 'expand-all-blocks':
                if (typeof expandAllBlocks === 'function') expandAllBlocks(projectId);
                break;
            case 'edit-task-block': {
                const currentName = el.getAttribute('data-block-name') || (el.closest('.task-block')?.querySelector('h6')?.textContent?.trim()) || '';
                const currentDescription = el.getAttribute('data-block-description') || '';
                if (typeof editTaskBlock === 'function') editTaskBlock(blockId, currentName, currentDescription);
                break;
            }
            case 'delete-task-block':
                if (typeof deleteTaskBlock === 'function') deleteTaskBlock(blockId);
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
            case 'delete-event': {
                const eventId = el.getAttribute('data-event-id');
                const attachmentId = el.getAttribute('data-attachment-id');
                const poId = el.getAttribute('data-po-id');
                const woId = el.getAttribute('data-wo-id');
                const cwoId = el.getAttribute('data-cwo-id');
                if (!eventId) return;

                if (!confirm('Are you sure you want to delete this item?')) return;

                // Dispatch by available identifiers
                if (attachmentId && window.Timeline?.Files?.deleteFile) {
                    Timeline.Files.deleteFile(attachmentId, projectId);
                    break;
                }
                if (poId && window.Timeline?.Purchases?.deletePurchaseOrder) {
                    Timeline.Purchases.deletePurchaseOrder(poId, projectId);
                    break;
                }
                if (cwoId && window.Timeline?.WorkOrders?.deleteCustomWorkOrder) {
                    Timeline.WorkOrders.deleteCustomWorkOrder(cwoId, projectId);
                    break;
                }
                if (woId && window.Timeline?.WorkOrders?.detachWorkOrder) {
                    Timeline.WorkOrders.detachWorkOrder(woId, projectId);
                    break;
                }
                // Default: treat as comment delete
                deleteComment(eventId)
                    .then((ok) => {
                        if (ok) {
                            loadTimelineForProject(projectId);
                        } else {
                            showNotification('Error deleting item', 'error');
                        }
                    })
                    .catch(() => showNotification('Network error occurred', 'error'));
                break;
            }
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
    return apiPostJson('/Project/UpdateEventDescription', {
        eventId: eventId,
        description: description
    }).then(res => !!res.success).catch(() => false);
}

// Helper to delete a comment event
function deleteComment(eventId) {
    return apiPostJson('/Project/DeleteComment', { eventId })
        .then(res => !!res.success);
}

// Initialize drag-drop functionality for timeline reordering
function initializeTimelineDragDrop(projectId) {
    const timelineContainer = document.getElementById(`timeline-container-${projectId}`);
    if (!timelineContainer) return;
    
    // Initialize unified drag-drop for all sortable containers
    initializeUnifiedDragDrop(projectId);
}

// Initialize unified drag-drop for all timeline containers
function initializeUnifiedDragDrop(projectId) {
    // Find all sortable containers in the timeline
    const timelineContainer = document.getElementById(`timeline-container-${projectId}`);
    if (!timelineContainer) return;
    
    // Initialize sortable for all containers with the sortable-container class
    const sortableContainers = timelineContainer.querySelectorAll('.sortable-container');
    sortableContainers.forEach(container => {
        initializeUnifiedSortableContainer(container, projectId);
    });
}

// Initialize unified sortable functionality for a container
function initializeUnifiedSortableContainer(container, projectId) {
    const parentId = container.dataset.parentId || null; // null for root, TaskBlock ID for nested
    
    new Sortable(container, {
        draggable: '.task-block, .timeline-event',
        handle: '.block-icon, .event-icon',
        ghostClass: 'sortable-ghost',
        chosenClass: 'sortable-chosen',
        dragClass: 'sortable-drag',
        animation: 150,
        group: {
            name: `timeline-${projectId}`,
            pull: true,
            put: true
        },
        onEnd: function(evt) {
            const draggedElement = evt.item;
            const fromContainer = evt.from;
            const toContainer = evt.to;
            
            const fromParentId = fromContainer.dataset.parentId || null;
            const toParentId = toContainer.dataset.parentId || null;
            
            // Handle movement between different containers (cross-container drag)
            if (fromContainer !== toContainer) {
                if (draggedElement.classList.contains('task-block')) {
                    // TaskBlock moved to different container - update parent relationship
                    const draggedBlockId = draggedElement.dataset.blockId;
                    nestTaskBlock(draggedBlockId, toParentId)
                        .then(() => {
                            reorderItemsInContainer(toContainer, toParentId);
                            reorderItemsInContainer(fromContainer, fromParentId);
                            // Visual nesting indicators disabled - no longer needed
                        })
                        .catch(error => {
                            console.error('Error nesting TaskBlock:', error);
                            showNotification('Error moving TaskBlock', 'error');
                            loadTimelineForProject(projectId);
                        });
                    return;
                }
                
                if (draggedElement.classList.contains('timeline-event')) {
                    // Event moved to different container - update parent relationship
                    const eventId = draggedElement.dataset.eventId;
                    if (toParentId) {
                        // Assign to block
                        assignEventsToBlock(toParentId, [eventId])
                            .then(() => {
                                reorderItemsInContainer(toContainer, toParentId);
                            })
                            .catch(error => {
                                console.error('Error assigning event to block:', error);
                                showNotification('Error moving event', 'error');
                                loadTimelineForProject(projectId);
                            });
                    } else {
                        // Unassign from block (move to root)
                        unassignEventsFromBlocks([eventId])
                            .then(() => {
                                reorderItemsInContainer(toContainer, toParentId);
                            })
                            .catch(error => {
                                console.error('Error unassigning event:', error);
                                showNotification('Error moving event', 'error');
                                loadTimelineForProject(projectId);
                            });
                    }
                    return;
                }
            }
            
            // Handle reordering within the same container
            reorderItemsInContainer(toContainer, toParentId);
            // Visual nesting indicators disabled - no longer needed
        }
    });
}

// Helper function to reorder items within a container using the unified API
function reorderItemsInContainer(container, parentId) {
    const allItems = Array.from(container.children);
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
        const requestData = {
            ParentId: parentId,
            Items: reorderedItems
        };
        
        apiPostJson('/Timeline/ReorderItems', requestData)
        .then(data => {
            if (!data.success) {
                console.error('Error reordering items:', data.message);
                showNotification('Error reordering items', 'error');
            }
        })
        .catch(error => {
            console.error('Error reordering items:', error);
            showNotification('Network error occurred', 'error');
        });
    }
}

// Update TaskBlock visual nesting indicators after nesting changes
// Simplified version - removed color styling per user request due to complexity
function updateTaskBlockNestingVisuals(projectId) {
    // Function kept for compatibility but no longer applies visual styling
    // All TaskBlocks now have consistent appearance regardless of nesting level
    console.log(`TaskBlock nesting updated for project ${projectId} - visual styling disabled`);
}

// Comment functionality
let currentCommentBlockId = null; // Store blockId for comment creation

function showAddComment(projectId, blockId = null) {
    currentCommentBlockId = blockId; // Store for use in saveComment
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
    
    apiPostJson('/Project/CreateComment', { projectId, description: commentText, eventDate: commentDate, createdBy: commentAuthor || null, parentBlockId: currentCommentBlockId })
    .then(data => {
        if (data.success) {
            modal.hide();
            // Clear form
            document.getElementById(`commentText-${projectId}`).value = '';
            document.getElementById(`commentDate-${projectId}`).value = new Date().toISOString().slice(0, 16);
            document.getElementById(`commentAuthor-${projectId}`).value = '';
            
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
    
    apiPostJson('/Project/UpdateEventDescription', { eventId: currentEditingEventId, description })
    .then(data => {
        if (data.success) {
            modal.hide();
            // Clear form and reset state
            document.getElementById(`editAttachmentCommentText-${projectId}`).value = '';
            currentEditingEventId = null;
            
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


// API call to nest a TaskBlock under another TaskBlock
function nestTaskBlock(childBlockId, parentBlockId) {
    const requestData = {
        ChildBlockId: childBlockId,
        ParentBlockId: parentBlockId
    };
    
    return apiPostJson('/Timeline/NestTaskBlock', requestData)
    .then(data => {
        if (!data.success) {
            throw new Error(data.message || 'Error nesting TaskBlock');
        }
        return data;
    });
}


// API call to unassign events from any TaskBlock (move to unblocked)
function unassignEventsFromBlocks(eventIds) {
    const requestData = {
        EventIds: eventIds
    };
    
    return apiPostJson('/Timeline/UnassignEvents', requestData)
    .then(data => {
        if (!data.success) {
            throw new Error(data.message || 'Error unassigning events');
        }
        return data;
    });
}
