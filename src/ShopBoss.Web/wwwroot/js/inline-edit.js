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

    // Handle clicks on editable labels
    function handleLabelClick(event) {
        const target = event.target;
        
        // Check if clicked element is an editable label
        if (target.classList.contains('editable-label')) {
            event.preventDefault();
            event.stopPropagation();
            
            // Don't create new editor if one is already active
            if (currentEditor) {
                return;
            }
            
            startEditing(target);
        }
    }

    // Handle keyboard events during editing
    function handleKeyDown(event) {
        if (!currentEditor) return;

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
        const originalValue = element.dataset.originalValue || element.textContent.trim();
        const attachmentId = element.dataset.attachmentId;
        
        // Create input field
        const input = document.createElement('input');
        input.type = 'text';
        input.value = originalValue;
        input.className = 'form-control form-control-sm inline-editor';
        input.style.width = Math.max(100, element.offsetWidth) + 'px';
        input.style.fontSize = window.getComputedStyle(element).fontSize;
        
        // Add blur event handler to save when focus is lost
        input.addEventListener('blur', function() {
            if (currentEditor) {
                saveEdit();
            }
        });
        
        // Store editor info
        currentEditor = {
            element: element,
            editor: input,
            originalValue: originalValue,
            attachmentId: attachmentId
        };
        
        // Replace element with input
        element.style.display = 'none';
        element.parentNode.insertBefore(input, element.nextSibling);
        
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
        
        // Validate input
        if (newValue.length === 0) {
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
            // Call the update function
            const success = await updateAttachmentLabel(editor.attachmentId, newValue);
            
            if (success) {
                // Update the original element
                editor.element.textContent = newValue;
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
        
        // Remove the input field
        editor.editor.remove();
        
        // Show the original element
        editor.element.style.display = '';
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

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeInlineEditing);
    } else {
        initializeInlineEditing();
    }
})();