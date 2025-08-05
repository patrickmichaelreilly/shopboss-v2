let selectedSheetId = null;
let selectedSheetName = null;
let sheetDetails = null;

function selectSheet(sheetId, sheetName) {
    // Update selected sheet
    selectedSheetId = sheetId;
    selectedSheetName = sheetName;
    
    // Update UI
    document.querySelectorAll('.list-group-item').forEach(item => {
        item.classList.remove('active');
    });
    
    document.querySelector(`[data-sheet-id="${sheetId}"]`).classList.add('active');
    document.getElementById('loadButton').disabled = false;
}

async function loadSheetDetails() {
    if (!selectedSheetId) {
        alert('Please select a sheet first');
        return;
    }
    
    console.log('Loading sheet details for sheet ID:', selectedSheetId);
    
    // Show loading state
    document.getElementById('sheetAnalysis').style.display = 'block';
    document.getElementById('sheetSummary').innerHTML = '<div class="text-center"><div class="spinner-border spinner-border-sm" role="status"></div> Loading...</div>';
    document.getElementById('attachmentList').innerHTML = '<div class="text-center"><div class="spinner-border spinner-border-sm" role="status"></div> Loading...</div>';
    document.getElementById('commentList').innerHTML = '<div class="text-center"><div class="spinner-border spinner-border-sm" role="status"></div> Loading...</div>';
    
    try {
        const url = `/SmartSheetMigration/GetSheetDetails?sheetId=${selectedSheetId}`;
        console.log('Fetching from URL:', url);
        
        const response = await fetch(url);
        console.log('Response status:', response.status);
        console.log('Response headers:', response.headers);
        
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        
        const data = await response.json();
        console.log('Received data:', data);
        
        if (data.error) {
            alert('Error loading sheet details: ' + data.error);
            console.error('API returned error:', data.error);
            return;
        }
        
        sheetDetails = data;
        
        // Display Sheet Summary
        console.log('Displaying summary:', data.summary);
        displaySheetSummary(data.summary);
        
        // Display Attachments
        console.log('Displaying attachments:', data.attachments);
        displayAttachments(data.attachments);
        
        // Display Comments
        console.log('Displaying comments:', data.comments);
        displayComments(data.comments);
        
        // Build Timeline
        buildTimeline(data);
        
        // Pre-fill project form
        prefillProjectForm(data);
        
    } catch (error) {
        console.error('Error in loadSheetDetails:', error);
        alert('Failed to load sheet details: ' + error.message);
        
        // Show error in the UI
        document.getElementById('sheetSummary').innerHTML = `<div class="alert alert-danger">Error: ${error.message}</div>`;
        document.getElementById('attachmentList').innerHTML = `<div class="alert alert-danger">Error: ${error.message}</div>`;
        document.getElementById('commentList').innerHTML = `<div class="alert alert-danger">Error: ${error.message}</div>`;
    }
}

function displaySheetSummary(summary) {
    const container = document.getElementById('sheetSummary');
    container.innerHTML = '';
    
    if (!summary || Object.keys(summary).length === 0) {
        container.innerHTML = '<p class="text-muted">No sheet summary data available</p>';
        return;
    }
    
    const table = document.createElement('table');
    table.className = 'table table-sm';
    
    for (const [key, value] of Object.entries(summary)) {
        const row = table.insertRow();
        const cellKey = row.insertCell(0);
        const cellValue = row.insertCell(1);
        
        cellKey.innerHTML = `<strong>${key}:</strong>`;
        cellValue.textContent = value || '(empty)';
        
        // Highlight Job ID
        if (key === 'Job ID' || key === 'JobID' || key === 'Job Number') {
            row.className = 'table-warning';
        }
    }
    
    container.appendChild(table);
}

function displayAttachments(attachments) {
    const container = document.getElementById('attachmentList');
    const count = document.getElementById('attachmentCount');
    
    container.innerHTML = '';
    count.textContent = attachments ? attachments.length : 0;
    
    if (!attachments || attachments.length === 0) {
        container.innerHTML = '<p class="text-muted">No attachments found</p>';
        return;
    }
    
    const list = document.createElement('ul');
    list.className = 'list-unstyled';
    
    attachments.forEach(attachment => {
        const li = document.createElement('li');
        li.className = 'mb-2';
        
        let rowInfo = '';
        if (attachment.rowNumber) {
            rowInfo = `<span class="badge bg-primary me-1">Row ${attachment.rowNumber}</span>`;
        }
        
        // Remove type badges - everything shows as FILE which is redundant
        
        li.innerHTML = `
            <i class="bi bi-paperclip"></i> ${attachment.name}<br>
            ${rowInfo}
            <small class="text-muted">
                ${attachment.sizeInKb.toFixed(1)} MB | 
                ${attachment.createdAt ? new Date(attachment.createdAt).toLocaleDateString() : 'Unknown date'} | 
                By: ${attachment.attachedBy}
            </small>
        `;
        list.appendChild(li);
    });
    
    container.appendChild(list);
}

function displayComments(comments) {
    const container = document.getElementById('commentList');
    const count = document.getElementById('commentCount');
    
    container.innerHTML = '';
    count.textContent = comments ? comments.length : 0;
    
    if (!comments || comments.length === 0) {
        container.innerHTML = '<p class="text-muted">No comments found</p>';
        return;
    }
    
    const list = document.createElement('div');
    
    comments.forEach(comment => {
        const div = document.createElement('div');
        div.className = 'mb-3 pb-2 border-bottom';
        
        let rowInfo = '';
        if (comment.rowNumber) {
            rowInfo = `<span class="badge bg-primary me-1">Row ${comment.rowNumber}</span>`;
        }
        
        div.innerHTML = `
            ${rowInfo}
            <p class="mb-1">${comment.text}</p>
            <small class="text-muted">
                ${comment.createdAt ? new Date(comment.createdAt).toLocaleDateString() : 'Unknown date'} | 
                By: ${comment.createdBy}
            </small>
        `;
        list.appendChild(div);
    });
    
    container.appendChild(list);
}

function buildTimeline(details) {
    const events = [];
    
    // Extract events from comments
    if (details.comments) {
        details.comments.forEach(comment => {
            events.push({
                date: comment.createdAt ? new Date(comment.createdAt) : null,
                type: 'comment',
                description: comment.text,
                user: comment.createdBy,
                rowNumber: comment.rowNumber
            });
        });
    }
    
    // Extract events from attachments
    if (details.attachments) {
        details.attachments.forEach(attachment => {
            events.push({
                date: attachment.createdAt ? new Date(attachment.createdAt) : null,
                type: 'attachment',
                description: `File uploaded: ${attachment.name} (${attachment.sizeInKb.toFixed(1)} MB)`,
                user: attachment.attachedBy,
                rowNumber: attachment.rowNumber
            });
        });
    }
    
    // Sort by date
    events.sort((a, b) => {
        if (!a.date) return 1;
        if (!b.date) return -1;
        return a.date - b.date;
    });
    
    const timeline = document.getElementById('timelinePreview');
    timeline.innerHTML = '';
    
    if (events.length === 0) {
        timeline.innerHTML = '<p class="text-muted">No timeline events found</p>';
        return;
    }
    
    events.forEach(event => {
        const div = document.createElement('div');
        div.className = 'timeline-event';
        
        let rowInfo = '';
        if (event.rowNumber) {
            rowInfo = `<span class="badge bg-primary me-1">Row ${event.rowNumber}</span>`;
        }
        
        div.innerHTML = `
            <span class="badge bg-${event.type === 'comment' ? 'info' : 'secondary'}">${event.type}</span>
            ${rowInfo}
            <small>${event.date ? event.date.toLocaleDateString() : 'Unknown date'}</small>
            <p class="mb-1">${event.description}</p>
            <small class="text-muted">by ${event.user}</small>
        `;
        timeline.appendChild(div);
    });
}

function prefillProjectForm(details) {
    const summary = details.summary || {};
    
    // Map SheetSummary fields to form fields
    const projectId = summary['Project ID'] || summary['Job ID'] || summary['JobID'] || '';
    const projectName = summary['Project Name'] || details.sheetName || '';
    const projectAddress = summary['Job Address'] || '';
    const generalContractor = summary['GC'] || '';
    const projectContact = summary['Job Contact'] || '';
    const projectContactPhone = summary['Job Contact Phone'] || '';
    const projectContactEmail = summary['Job Contact Email'] || '';
    const projectManager = summary['Project Manager'] || summary['PM'] || '';
    const installer = summary['Installer'] || '';
    
    // Pre-fill form fields - mapping to actual Project entity properties
    document.getElementById('projectId').value = projectId;
    document.getElementById('projectName').value = projectName;
    document.getElementById('projectAddress').value = projectAddress;
    document.getElementById('projectManager').value = projectManager;
    document.getElementById('projectContact').value = projectContact;
    document.getElementById('projectContactPhone').value = projectContactPhone;
    document.getElementById('projectContactEmail').value = projectContactEmail;
    document.getElementById('generalContractor').value = generalContractor;
    document.getElementById('installer').value = installer;
    
    console.log('Pre-filled form with values:', {
        projectId,
        projectName,
        projectAddress,
        projectManager,
        projectContact,
        projectContactPhone,
        projectContactEmail,
        generalContractor,
        installer
    });
}

function showProjectForm() {
    document.getElementById('projectCreation').style.display = 'block';
    document.getElementById('projectCreation').scrollIntoView({ behavior: 'smooth' });
}

async function importProject() {
    // Get form data - always download attachments and create timeline
    const projectData = {
        sheetId: selectedSheetId,
        projectId: document.getElementById('projectId').value,
        projectName: document.getElementById('projectName').value,
        projectManager: document.getElementById('projectManager').value,
        projectContact: document.getElementById('projectContact').value,
        projectContactPhone: document.getElementById('projectContactPhone').value,
        projectContactEmail: document.getElementById('projectContactEmail').value,
        projectAddress: document.getElementById('projectAddress').value,
        generalContractor: document.getElementById('generalContractor').value,
        installer: document.getElementById('installer').value,
        targetInstallDate: document.getElementById('targetInstallDate').value || null,
        projectCategory: parseInt(document.getElementById('projectCategory').value),
        downloadAttachments: true,  // Always true
        createTimeline: true        // Always true
    };
    
    // Validate required fields
    if (!projectData.projectId || !projectData.projectName) {
        alert('Project ID and Project Name are required');
        return;
    }
    
    // Show loading state
    const importButton = event.target;
    importButton.disabled = true;
    importButton.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span> Importing...';
    
    try {
        const response = await fetch('/SmartSheetMigration/ImportProject', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(projectData)
        });
        
        const result = await response.json();
        
        // Show results
        document.getElementById('importResults').style.display = 'block';
        const resultsContent = document.getElementById('resultsContent');
        
        if (result.success) {
            resultsContent.innerHTML = `
                <div class="alert alert-success">
                    <h5>Import Successful!</h5>
                    <p>${result.message}</p>
                </div>
                ${result.projectId ? `
                    <a href="/Project/Details/${result.projectId}" class="btn btn-primary">
                        View Imported Project
                    </a>
                    <button class="btn btn-secondary ms-2" onclick="location.reload()">
                        Import Another Project
                    </button>
                ` : ''}
            `;
        } else {
            resultsContent.innerHTML = `
                <div class="alert alert-danger">
                    <h5>Import Failed</h5>
                    <p>${result.message}</p>
                </div>
                <button class="btn btn-secondary" onclick="location.reload()">
                    Try Again
                </button>
            `;
        }
        
        document.getElementById('importResults').scrollIntoView({ behavior: 'smooth' });
        
    } catch (error) {
        alert('Failed to import project: ' + error.message);
        console.error(error);
    } finally {
        importButton.disabled = false;
        importButton.innerHTML = 'Import Project to ShopBoss';
    }
}