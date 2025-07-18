/**
 * WorkOrderTreeView - Reusable tree component for hierarchical work order data
 * Supports both Import Preview mode and Modify Status mode
 */
class WorkOrderTreeView {
    constructor(containerId, options = {}) {
        this.container = document.getElementById(containerId);
        if (!this.container) {
            throw new Error(`Container element with ID '${containerId}' not found`);
        }

        // Configuration
        this.mode = options.mode || 'import'; // 'import' or 'modify'
        this.apiUrl = options.apiUrl || null;
        this.workOrderId = options.workOrderId || null;
        this.onStatusChange = options.onStatusChange || (() => {});
        this.onCategoryChange = options.onCategoryChange || (() => {});
        this.onDataLoaded = options.onDataLoaded || (() => {});
        
        // State management
        this.data = null;

        // Initialize
        this.init();
    }

    init() {
        this.createTreeContainer();
        if (this.mode === 'modify' && this.apiUrl && this.workOrderId) {
            this.loadData();
        }
    }

    createTreeContainer() {
        this.container.innerHTML = `
            <div class="tree-view-container">
                <div class="tree-view-controls mb-3 d-flex justify-content-between align-items-center">
                    <div class="tree-controls">
                        ${this.mode === 'import' ? this.createImportControls() : this.createModifyControls()}
                    </div>
                    <div class="tree-search">
                        <input type="text" id="treeSearch" class="form-control form-control-sm" placeholder="Search items..." style="width: 200px;">
                    </div>
                </div>
                <div id="treeViewContent" class="tree-view-content">
                    <div class="text-center text-muted">
                        <i class="fas fa-spinner fa-spin me-2"></i>Loading...
                    </div>
                </div>
            </div>
        `;

        this.bindEvents();
    }

    createImportControls() {
        return `
            <button type="button" id="expandAll" class="btn btn-outline-primary btn-sm">
                <i class="fas fa-expand-alt me-1"></i>Expand All
            </button>
            <button type="button" id="collapseAll" class="btn btn-outline-secondary btn-sm">
                <i class="fas fa-compress-alt me-1"></i>Collapse All
            </button>
        `;
    }

    createModifyControls() {
        return `
            <button type="button" id="expandAll" class="btn btn-outline-primary btn-sm">
                <i class="fas fa-expand-alt me-1"></i>Expand All
            </button>
            <button type="button" id="collapseAll" class="btn btn-outline-secondary btn-sm">
                <i class="fas fa-compress-alt me-1"></i>Collapse All
            </button>
            <button type="button" id="refreshTree" class="btn btn-outline-info btn-sm">
                <i class="fas fa-sync me-1"></i>Refresh
            </button>
        `;
    }

    bindEvents() {
        // Control buttons
        const expandAll = this.container.querySelector('#expandAll');
        const collapseAll = this.container.querySelector('#collapseAll');
        const refreshTree = this.container.querySelector('#refreshTree');
        const searchInput = this.container.querySelector('#treeSearch');

        if (expandAll) {
            expandAll.addEventListener('click', () => this.expandAll());
        }
        if (collapseAll) {
            collapseAll.addEventListener('click', () => this.collapseAll());
        }
        if (refreshTree) {
            refreshTree.addEventListener('click', () => this.loadData());
        }
        if (searchInput) {
            searchInput.addEventListener('input', (e) => this.filterTree(e.target.value));
        }
    }

    async loadData() {
        if (!this.apiUrl || !this.workOrderId) {
            console.error('API URL and Work Order ID required for data loading');
            return;
        }

        try {
            const response = await fetch(`${this.apiUrl}/${this.workOrderId}`);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            const data = await response.json();
            this.setData(data);
        } catch (error) {
            console.error('Error loading tree data:', error);
            this.container.querySelector('#treeViewContent').innerHTML = 
                '<div class="alert alert-danger">Failed to load tree data</div>';
        }
    }

    setData(data) {
        this.data = data;
        this.renderTree();
        this.onDataLoaded(data);
    }

    renderTree() {
        const treeContent = this.container.querySelector('#treeViewContent');
        
        if (!this.data || !this.data.items || this.data.items.length === 0) {
            treeContent.innerHTML = '<p class="text-muted">No data available.</p>';
            return;
        }

        treeContent.innerHTML = '';

        this.data.items.forEach(item => {
            const itemNode = this.createTreeNode(item, 0);
            treeContent.appendChild(itemNode);
        });
    }

    createTreeNode(item, level = 0) {
        const node = document.createElement('div');
        node.className = `tree-node level-${level}`;
        node.dataset.itemId = item.id;
        node.dataset.itemType = item.type;

        const hasChildren = item.children && item.children.length > 0;
        const nodeId = item.id;

        // Determine if this should be expanded by default
        // Top-level categories (level 0 and type "category") should default to expanded
        const shouldDefaultExpand = level === 0 && item.type === 'category';

        const icon = this.getItemIcon(item.type);

        node.innerHTML = `
            <div class="d-flex align-items-center justify-content-between">
                <div class="d-flex align-items-center">
                    ${hasChildren ? 
                        `<span class="tree-toggle me-2" data-expanded="${shouldDefaultExpand}" style="width: 16px; display: inline-flex; align-items: center; justify-content: center; min-width: 16px; height: 16px; cursor: pointer; user-select: none; color: #6c757d;">
                            <i class="fas fa-chevron-${shouldDefaultExpand ? 'down' : 'right'}" style="font-size: 12px;"></i>
                        </span>` : 
                        '<span class="me-2" style="width: 16px; display: inline-block;"></span>'
                    }
                    <span class="me-2">${icon}</span>
                    <span>${this.formatItemName(item)}</span>
                </div>
                ${this.mode === 'modify' && item.type !== 'category' ? 
                    `<div class="dropdown-container d-flex gap-2">
                        ${this.createStatusDropdown(item)}
                        ${item.type === 'part' && item.category ? this.createCategoryDropdown(item) : ''}
                    </div>` :
                    ''
                }
            </div>
        `;

        // Add children container
        if (hasChildren) {
            const childrenContainer = document.createElement('div');
            childrenContainer.className = 'tree-content';
            childrenContainer.style.display = shouldDefaultExpand ? 'block' : 'none';

            item.children.forEach(child => {
                const childNode = this.createTreeNode(child, level + 1);
                childrenContainer.appendChild(childNode);
            });
            node.appendChild(childrenContainer);

            // Bind toggle event
            const toggle = node.querySelector('.tree-toggle');
            if (toggle) {
                toggle.addEventListener('click', (e) => {
                    e.stopPropagation();
                    this.toggleNode(toggle, childrenContainer);
                });
            }
        }

        // Bind status dropdown events for modify mode
        if (this.mode === 'modify') {
            const statusSelect = node.querySelector('.status-dropdown');
            if (statusSelect) {
                // Remove any existing event listeners to prevent duplicates
                statusSelect.replaceWith(statusSelect.cloneNode(true));
                const newStatusSelect = node.querySelector('.status-dropdown');
                
                newStatusSelect.addEventListener('change', (e) => {
                    e.stopPropagation();
                    const itemType = e.target.dataset.itemType;
                    const targetNodeId = e.target.dataset.nodeId;
                    this.handleStatusChange(targetNodeId, e.target.value, itemType);
                });
            }

            const categorySelect = node.querySelector('.category-dropdown');
            if (categorySelect) {
                // Remove any existing event listeners to prevent duplicates
                categorySelect.replaceWith(categorySelect.cloneNode(true));
                const newCategorySelect = node.querySelector('.category-dropdown');
                
                newCategorySelect.addEventListener('change', (e) => {
                    e.stopPropagation();
                    const itemType = e.target.dataset.itemType;
                    const targetNodeId = e.target.dataset.nodeId;
                    this.handleCategoryChange(targetNodeId, e.target.value, itemType);
                });
            }
        }

        return node;
    }

    createStatusDropdown(item) {
        const statuses = ['Pending', 'Cut', 'Sorted', 'Assembled', 'Shipped'];
        const currentStatus = item.status || 'Pending';
        
        return `
            <select class="status-dropdown form-select form-select-sm" style="width: auto; min-width: 100px;" data-node-id="${item.id}" data-item-type="${item.type}">
                ${statuses.map(status => 
                    `<option value="${status}" ${status === currentStatus ? 'selected' : ''}>${status}</option>`
                ).join('')}
            </select>
        `;
    }

    createCategoryDropdown(item) {
        const categories = ['Standard', 'Hardware', 'Edgeband', 'Veneer', 'Solid Wood', 'Laminate'];
        const currentCategory = item.category || 'Standard';
        
        return `
            <select class="category-dropdown form-select form-select-sm" style="width: auto; min-width: 100px;" data-node-id="${item.id}" data-item-type="${item.type}">
                ${categories.map(category => 
                    `<option value="${category}" ${category === currentCategory ? 'selected' : ''}>${category}</option>`
                ).join('')}
            </select>
        `;
    }

    handleStatusChange(nodeId, newStatus, itemType) {
        this.onStatusChange(nodeId, newStatus, itemType);
    }

    handleCategoryChange(nodeId, newCategory, itemType) {
        this.onCategoryChange(nodeId, newCategory, itemType);
    }

    formatItemName(item) {
        let name = item.name || 'Unnamed Item';
        if (item.quantity && item.quantity > 1) {
            name += ` (${item.quantity})`;
        }
        return name;
    }

    getItemIcon(type) {
        const icons = {
            'category': '<i class="fas fa-folder text-secondary"></i>',
            'product': '<i class="fas fa-box text-primary"></i>',
            'part': '<i class="fas fa-puzzle-piece text-success"></i>',
            'subassembly': '<i class="fas fa-layer-group text-info"></i>',
            'hardware': '<i class="fas fa-tools text-warning"></i>',
            'detached_product': '<i class="fas fa-th-large text-dark"></i>',
            'nestsheet': '<i class="fas fa-cut text-secondary"></i>'
        };
        return icons[type] || '<i class="fas fa-question text-muted"></i>';
    }

    toggleNode(toggle, childrenContainer) {
        const isExpanded = toggle.dataset.expanded === 'true';
        const icon = toggle.querySelector('i');
        
        if (isExpanded) {
            childrenContainer.style.display = 'none';
            icon.className = 'fas fa-chevron-right';
            toggle.dataset.expanded = 'false';
        } else {
            childrenContainer.style.display = 'block';
            icon.className = 'fas fa-chevron-down';
            toggle.dataset.expanded = 'true';
        }
    }

    // Control methods
    expandAll() {
        const allToggles = this.container.querySelectorAll('.tree-toggle');
        allToggles.forEach(toggle => {
            const childrenContainer = toggle.closest('.tree-node').querySelector('.tree-content');
            if (childrenContainer) {
                childrenContainer.style.display = 'block';
                toggle.querySelector('i').className = 'fas fa-chevron-down';
                toggle.dataset.expanded = 'true';
            }
        });
    }

    collapseAll() {
        const allToggles = this.container.querySelectorAll('.tree-toggle');
        allToggles.forEach(toggle => {
            const childrenContainer = toggle.closest('.tree-node').querySelector('.tree-content');
            if (childrenContainer) {
                childrenContainer.style.display = 'none';
                toggle.querySelector('i').className = 'fas fa-chevron-right';
                toggle.dataset.expanded = 'false';
            }
        });
    }

    filterTree(searchTerm) {
        const allNodes = this.container.querySelectorAll('.tree-node');
        const lowerSearchTerm = searchTerm.toLowerCase();
        
        if (!searchTerm) {
            allNodes.forEach(node => {
                node.style.display = 'block';
            });
            return;
        }

        allNodes.forEach(node => {
            const itemText = node.textContent.toLowerCase();
            const shouldShow = itemText.includes(lowerSearchTerm);
            node.style.display = shouldShow ? 'block' : 'none';
        });
    }

    refresh() {
        if (this.mode === 'modify') {
            this.loadData();
        } else {
            this.renderTree();
        }
    }
}

// Make it available globally
window.WorkOrderTreeView = WorkOrderTreeView;