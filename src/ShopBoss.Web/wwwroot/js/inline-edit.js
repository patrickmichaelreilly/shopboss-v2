// Inline Editing System
// Provides click-to-edit functionality without Edit/Save/Cancel buttons

(function() {
    let currentEditor = null; // Track the currently active editor to prevent multiple edits

    // Initialize inline editing on page load
    function initializeInlineEditing() {
        // Handle attachment label editing
        document.addEventListener('click', handleLabelClick);
        document.addEventListener('keydown', handleKeyDown);
    }

    // Handle clicks on editable labels and fields
    function handleLabelClick(event) {
        const target = event.target;
        
        // Check if clicked element is an editable label or field
        if (target.classList.contains('editable-label') || target.classList.contains('editable-field')) {
            event.preventDefault();
            event.stopPropagation();
            
            // Don't create new editor if one is already active
            if (currentEditor) {
                return;
            }
            
            startEditing(target);
        }
    }

    // Handle keyboard events during editing (backup handler for cases where input handler doesn't fire)
    function handleKeyDown(event) {
        if (!currentEditor) return;
        
        // Only handle if the event didn't come from our input element (backup)
        if (event.target === currentEditor.editor) return;

        switch(event.key) {
            case 'Enter':
                event.preventDefault();
                saveEdit();
                break;
            case 'Escape':
                event.preventDefault();
                cancelEdit();
                break;
        }
    }


    // Start editing an element
    function startEditing(element) {
        const originalValue = element.dataset.originalValue || element.dataset.value || element.textContent.trim();
        const attachmentId = element.dataset.attachmentId;
        const projectId = element.dataset.projectId;
        const eventId = element.dataset.eventId;
        const blockId = element.dataset.blockId;
        const purchaseOrderId = element.dataset.purchaseOrderId;
        const customWorkOrderId = element.dataset.customWorkOrderId;
        const fieldName = element.dataset.field;
        const fieldType = element.dataset.type || 'text';
        
        // Create appropriate input based on field type
        let input;
        if (fieldType === 'textarea') {
            input = document.createElement('textarea');
            input.rows = 3;
        } else if (fieldType === 'select') {
            input = document.createElement('select');
            setupSelectOptions(input, fieldName);
        } else {
            input = document.createElement('input');
            input.type = fieldType;
        }
        
        input.value = originalValue;
        input.className = 'inline-editor';
        
        // Get element's computed styles and position
        const elementStyles = window.getComputedStyle(element);
        const rect = element.getBoundingClientRect();
        
        // Position input to exactly overlay the original element
        input.style.position = 'absolute';
        input.style.left = rect.left + window.scrollX + 'px';
        input.style.top = rect.top + window.scrollY + 'px';
        input.style.width = rect.width + 'px';
        input.style.height = rect.height + 'px';
        input.style.fontSize = elementStyles.fontSize;
        input.style.fontFamily = elementStyles.fontFamily;
        input.style.fontWeight = elementStyles.fontWeight;
        input.style.lineHeight = elementStyles.lineHeight;
        input.style.textAlign = elementStyles.textAlign;
        input.style.zIndex = '1000';
        
        // For textareas, ensure they match the original element size exactly
        if (fieldType === 'textarea') {
            // Override CSS min-height to match original element exactly
            input.style.minHeight = rect.height + 'px';
            // Find the containing card to constrain maximum growth
            const card = element.closest('.card');
            if (card) {
                const cardRect = card.getBoundingClientRect();
                const maxHeight = cardRect.bottom - rect.top - 20; // 20px padding from bottom
                input.style.maxHeight = Math.max(maxHeight, rect.height) + 'px';
            }
        }
        
        // Add blur event handler to save when focus is lost
        input.addEventListener('blur', function() {
            if (currentEditor) {
                saveEdit();
            }
        });
        
        // Add keydown handler specifically for this input to handle Alt+Enter
        input.addEventListener('keydown', function(event) {
            switch(event.key) {
                case 'Enter':
                    // Alt+Enter allows line breaks in textarea fields
                    if (event.altKey && fieldType === 'textarea') {
                        // Allow the default behavior (insert line break)
                        return;
                    }
                    event.preventDefault();
                    saveEdit();
                    break;
                case 'Escape':
                    event.preventDefault();
                    cancelEdit();
                    break;
            }
        });
        
        // Store editor info
        currentEditor = {
            element: element,
            editor: input,
            originalValue: originalValue,
            attachmentId: attachmentId,
            projectId: projectId,
            eventId: eventId,
            blockId: blockId,
            purchaseOrderId: purchaseOrderId,
            customWorkOrderId: customWorkOrderId,
            fieldName: fieldName,
            fieldType: fieldType
        };
        
        // Make original element invisible but keep its space
        element.style.visibility = 'hidden';
        
        // Add input to document body (positioned absolutely)
        document.body.appendChild(input);
        
        // Focus and select the input
        setTimeout(() => {
            input.focus();
            input.select();
        }, 0);
        
        // Add visual feedback
        input.classList.add('editing');
    }

    // Save the edit
    async function saveEdit() {
        if (!currentEditor) return;
        
        // Prevent multiple saves by temporarily storing and clearing currentEditor
        const editor = currentEditor;
        currentEditor = null;
        
        const newValue = editor.editor.value.trim();
        const originalValue = editor.originalValue;
        
        // Check if value actually changed
        if (newValue === originalValue) {
            finishEditWithEditor(editor);
            return;
        }
        
        // Validate input based on field type
        if (editor.attachmentId && newValue.length === 0) {
            // Attachment labels cannot be empty
            if (typeof showNotification === 'function') {
                showNotification('Label cannot be empty', 'error');
            }
            // Restore editor and focus
            currentEditor = editor;
            editor.editor.focus();
            return;
        }
        
        // Show saving state
        editor.editor.disabled = true;
        editor.editor.classList.add('saving');
        
        try {
            let success = false;
            
            // Call appropriate update function based on field type
            if (editor.attachmentId) {
                success = await updateAttachmentLabel(editor.attachmentId, newValue);
            } else if (editor.eventId && editor.fieldName) {
                success = await updateEventField(editor.eventId, editor.fieldName, newValue);
            } else if (editor.blockId && editor.fieldName) {
                success = await updateTaskBlockField(editor.blockId, editor.fieldName, newValue);
            } else if (editor.purchaseOrderId && editor.fieldName) {
                success = await updatePurchaseOrderField(editor.purchaseOrderId, editor.fieldName, newValue);
            } else if (editor.customWorkOrderId && editor.fieldName) {
                success = await updateCustomWorkOrderField(editor.customWorkOrderId, editor.fieldName, newValue);
            } else if (editor.projectId && editor.fieldName) {
                success = await updateProjectField(editor.projectId, editor.fieldName, newValue);
            }
            
            if (success) {
                // Update the original element based on field type
                updateElementDisplay(editor.element, newValue, editor.fieldType);
                editor.element.dataset.originalValue = newValue;
                
                // Show success feedback
                showSuccessFeedback(editor.element);
                
                // Clean up editor
                finishEditWithEditor(editor);
            } else {
                // Re-enable editor on failure and restore currentEditor
                currentEditor = editor;
                editor.editor.disabled = false;
                editor.editor.classList.remove('saving');
                editor.editor.focus();
            }
        } catch (error) {
            console.error('Error saving label:', error);
            if (typeof showNotification === 'function') {
                showNotification('Error saving label', 'error');
            }
            // Re-enable editor on failure and restore currentEditor
            currentEditor = editor;
            editor.editor.disabled = false;
            editor.editor.classList.remove('saving');
            editor.editor.focus();
        }
    }

    // Cancel the edit
    function cancelEdit() {
        if (!currentEditor) return;
        finishEdit();
    }

    // Clean up the editor
    function finishEdit() {
        if (!currentEditor) return;
        finishEditWithEditor(currentEditor);
        currentEditor = null;
    }
    
    // Clean up the editor with specific editor object
    function finishEditWithEditor(editor) {
        if (!editor) return;
        
        // Remove the input field if it still exists in the DOM
        if (editor.editor && editor.editor.parentNode) {
            editor.editor.remove();
        }
        
        // Show the original element
        if (editor.element) {
            editor.element.style.visibility = '';
        }
    }

    // Show success feedback
    function showSuccessFeedback(element) {
        // Add success class
        element.classList.add('edit-success');
        
        // Remove the class after animation
        setTimeout(() => {
            element.classList.remove('edit-success');
        }, 2000);
    }

    // Setup select options for dropdown fields
    function setupSelectOptions(select, fieldName) {
        if (fieldName === 'ProjectCategory') {
            // Add ProjectCategory options (from enum)
            const options = [
                { value: 0, text: 'Residential' },
                { value: 1, text: 'Commercial' },
                { value: 2, text: 'Industrial' },
                { value: 3, text: 'Other' }
            ];
            
            options.forEach(option => {
                const optionElement = document.createElement('option');
                optionElement.value = option.value;
                optionElement.textContent = option.text;
                select.appendChild(optionElement);
            });
        }
    }

    // Update element display after successful save
    function updateElementDisplay(element, value, fieldType) {
        const displayValue = value || "-";
        
        if (fieldType === 'select' && element.dataset.field === 'ProjectCategory') {
            // Update category badge
            const categoryNames = ['Residential', 'Commercial', 'Industrial', 'Other'];
            const categoryIndex = parseInt(value) || 0;
            const categoryName = categoryNames[categoryIndex] || 'Unknown';
            element.innerHTML = `<span class="badge bg-secondary">${categoryName}</span>`;
        } else if (fieldType === 'date' && value) {
            // Format date for display
            try {
                const date = new Date(value);
                const formattedDate = date.toLocaleDateString('en-US', { 
                    month: '2-digit', 
                    day: '2-digit', 
                    year: '2-digit' 
                });
                element.textContent = formattedDate;
            } catch (e) {
                element.textContent = displayValue;
            }
        } else {
            element.textContent = displayValue;
        }
    }

    // Update project field via API
    async function updateProjectField(projectId, fieldName, value) {
        try {
            const response = await fetch('/Project/UpdateProjectField', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    projectId: projectId,
                    fieldName: fieldName,
                    value: value
                })
            });

            const data = await response.json();
            
            if (data.success) {
                return true;
            } else {
                if (typeof showNotification === 'function') {
                    showNotification(data.message || 'Error updating field', 'error');
                }
                return false;
            }
        } catch (error) {
            console.error('Error updating project field:', error);
            if (typeof showNotification === 'function') {
                showNotification('Network error occurred', 'error');
            }
            return false;
        }
    }

    // Update event field via API
    async function updateEventField(eventId, fieldName, value) {
        try {
            const response = await fetch('/Project/UpdateEventField', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    eventId: eventId,
                    fieldName: fieldName,
                    value: value
                })
            });

            const data = await response.json();
            
            if (data.success) {
                return true;
            } else {
                if (typeof showNotification === 'function') {
                    showNotification(data.message || 'Error updating event field', 'error');
                }
                return false;
            }
        } catch (error) {
            console.error('Error updating event field:', error);
            if (typeof showNotification === 'function') {
                showNotification('Network error occurred', 'error');
            }
            return false;
        }
    }

    // Update task block field via API
    async function updateTaskBlockField(blockId, fieldName, value) {
        try {
            const response = await fetch('/Timeline/UpdateTaskBlockField', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    blockId: blockId,
                    fieldName: fieldName,
                    value: value
                })
            });

            const data = await response.json();
            
            if (data.success) {
                return true;
            } else {
                if (typeof showNotification === 'function') {
                    showNotification(data.message || 'Error updating task block field', 'error');
                }
                return false;
            }
        } catch (error) {
            console.error('Error updating task block field:', error);
            if (typeof showNotification === 'function') {
                showNotification('Network error occurred', 'error');
            }
            return false;
        }
    }

    // Update purchase order field via API
    async function updatePurchaseOrderField(purchaseOrderId, fieldName, value) {
        try {
            const response = await fetch('/Project/UpdatePurchaseOrderField', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    purchaseOrderId: purchaseOrderId,
                    fieldName: fieldName,
                    value: value
                })
            });

            const data = await response.json();
            
            if (data.success) {
                return true;
            } else {
                if (typeof showNotification === 'function') {
                    showNotification(data.message || 'Error updating purchase order field', 'error');
                }
                return false;
            }
        } catch (error) {
            console.error('Error updating purchase order field:', error);
            if (typeof showNotification === 'function') {
                showNotification('Network error occurred', 'error');
            }
            return false;
        }
    }

    // Update custom work order field via API
    async function updateCustomWorkOrderField(customWorkOrderId, fieldName, value) {
        try {
            const response = await fetch('/Project/UpdateCustomWorkOrderField', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    customWorkOrderId: customWorkOrderId,
                    fieldName: fieldName,
                    value: value
                })
            });

            const data = await response.json();
            
            if (data.success) {
                return true;
            } else {
                if (typeof showNotification === 'function') {
                    showNotification(data.message || 'Error updating custom work order field', 'error');
                }
                return false;
            }
        } catch (error) {
            console.error('Error updating custom work order field:', error);
            if (typeof showNotification === 'function') {
                showNotification('Network error occurred', 'error');
            }
            return false;
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeInlineEditing);
    } else {
        initializeInlineEditing();
    }
})();