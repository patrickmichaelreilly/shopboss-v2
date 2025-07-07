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
        this.onSelectionChange = options.onSelectionChange || (() => {});
        this.onStatusChange = options.onStatusChange || (() => {});
        this.onDataLoaded = options.onDataLoaded || (() => {});
        
        // State management
        this.data = null;
        this.selectionState = new Map();
        this.selectionCounts = {
            products: 0,
            parts: 0,
            subassemblies: 0,
            hardware: 0,
            nestSheets: 0,
            detachedProducts: 0
        };

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
                        <input type="text" id="treeSearch" class="form-control form-control-sm" 
                               placeholder="Search items..." style="width: 200px;">
                    </div>
                </div>
                <div class="tree-view-content">
                    <div id="treeViewContent" class="tree-view">
                        <div class="text-center py-4">
                            <i class="fas fa-spinner fa-spin"></i>
                            <p class="mt-2 text-muted">Loading tree data...</p>
                        </div>
                    </div>
                </div>
            </div>
        `;

        this.bindEvents();
    }

    createImportControls() {
        return `
            <button type="button" id="selectAllProducts" class="btn btn-outline-success btn-sm">
                <i class="fas fa-check-double me-1"></i>Select All Products
            </button>
            <button type="button" id="selectAllNestSheets" class="btn btn-outline-secondary btn-sm">
                <i class="fas fa-check-double me-1"></i>Select All Nest Sheets
            </button>
            <button type="button" id="clearAll" class="btn btn-outline-danger btn-sm">
                <i class="fas fa-times me-1"></i>Clear All
            </button>
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
            <button type="button" id="refreshTree" class="btn btn-outline-secondary btn-sm">
                <i class="fas fa-refresh me-1"></i>Refresh
            </button>
        `;
    }

    bindEvents() {
        // Control buttons
        const selectAllProducts = this.container.querySelector('#selectAllProducts');
        const selectAllNestSheets = this.container.querySelector('#selectAllNestSheets');
        const clearAll = this.container.querySelector('#clearAll');
        const expandAll = this.container.querySelector('#expandAll');
        const collapseAll = this.container.querySelector('#collapseAll');
        const refreshTree = this.container.querySelector('#refreshTree');
        const searchInput = this.container.querySelector('#treeSearch');

        if (selectAllProducts) {
            selectAllProducts.addEventListener('click', () => this.selectAllProducts());
        }
        if (selectAllNestSheets) {
            selectAllNestSheets.addEventListener('click', () => this.selectAllNestSheets());
        }
        if (clearAll) {
            clearAll.addEventListener('click', () => this.clearAllSelections());
        }
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
            const includeStatus = this.mode === 'modify';
            const response = await fetch(`${this.apiUrl}/${this.workOrderId}?includeStatus=${includeStatus}`);
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            this.data = await response.json();
            this.renderTree();
            this.onDataLoaded(this.data);
        } catch (error) {
            console.error('Error loading tree data:', error);
            this.showError('Failed to load tree data: ' + error.message);
        }
    }

    setData(data) {
        this.data = data;
        this.renderTree();
    }

    renderTree() {
        const treeContent = this.container.querySelector('#treeViewContent');
        if (!this.data || !this.data.items) {
            treeContent.innerHTML = '<p class="text-muted">No data available.</p>';
            return;
        }

        treeContent.innerHTML = '';
        this.selectionState.clear();

        this.data.items.forEach(item => {
            const itemNode = this.createTreeNode(item, 0);
            treeContent.appendChild(itemNode);
        });

        this.updateSelectionCounts();
        this.onSelectionChange(this.getSelectionSummary());
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

        // Initialize selection state
        if (!this.selectionState.has(nodeId)) {
            this.selectionState.set(nodeId, {
                selected: this.mode === 'import', // Default selected for import mode
                type: item.type,
                itemData: item,
                parentId: null,
                childIds: []
            });
        }

        const nodeState = this.selectionState.get(nodeId);
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
                    ${this.mode === 'import' ? 
                        `<input type="checkbox" class="tree-checkbox form-check-input me-2" ${nodeState.selected ? 'checked' : ''} data-node-id="${nodeId}">` :
                        ''
                    }
                    <span class="me-2">${icon}</span>
                    <span>${this.formatItemName(item)}</span>
                </div>
                ${this.mode === 'modify' && item.type !== 'category' ? 
                    `<div class="status-dropdown-container">
                        ${this.createStatusDropdown(item)}
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

            const childIds = [];
            item.children.forEach(child => {
                const childNode = this.createTreeNode(child, level + 1);
                childrenContainer.appendChild(childNode);
                childIds.push(child.id);

                // Set parent relationship
                const childState = this.selectionState.get(child.id);
                if (childState) {
                    childState.parentId = nodeId;
                }
            });

            // Update parent with child IDs
            nodeState.childIds = childIds;
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

        // Bind selection events
        if (this.mode === 'import') {
            const checkbox = node.querySelector('.tree-checkbox');
            if (checkbox) {
                checkbox.addEventListener('change', (e) => {
                    e.stopPropagation();
                    this.handleNodeSelection(nodeId, checkbox.checked);
                });
            }
        } else {
            const statusSelect = node.querySelector('.status-dropdown');
            if (statusSelect) {
                statusSelect.addEventListener('change', (e) => {
                    this.handleStatusChange(nodeId, e.target.value);
                });
            }
        }

        return node;
    }

    createStatusDropdown(item) {
        const statuses = ['Pending', 'Cut', 'Sorted', 'Assembled', 'Shipped'];
        const currentStatus = item.status || 'Pending';
        
        return `
            <select class="status-dropdown form-select form-select-sm" style="width: auto; min-width: 100px;" data-node-id="${item.id}">
                ${statuses.map(status => 
                    `<option value="${status}" ${status === currentStatus ? 'selected' : ''}>${status}</option>`
                ).join('')}
            </select>
        `;
    }

    getItemIcon(type) {
        const icons = {
            'product': '🚪',
            'subassembly': '📁',
            'part': '📄',
            'hardware': '🔧',
            'nestsheet': '📋',
            'detached': '📄',
            'category': '📂'
        };
        return icons[type] || '📄';
    }

    formatItemName(item) {
        let name = item.name;
        if (item.quantity && item.quantity > 1) {
            name += ` - Qty. ${item.quantity}`;
        }
        return name;
    }

    toggleNode(toggle, content) {
        const isExpanded = toggle.dataset.expanded === 'true';
        toggle.dataset.expanded = !isExpanded;
        
        const icon = toggle.querySelector('i');
        if (icon) {
            icon.className = `fas fa-chevron-${!isExpanded ? 'down' : 'right'}`;
        }
        
        content.style.display = !isExpanded ? 'block' : 'none';
    }

    handleNodeSelection(nodeId, isSelected) {
        if (this.mode !== 'import') return;

        const nodeState = this.selectionState.get(nodeId);
        if (!nodeState) return;

        nodeState.selected = isSelected;

        if (isSelected) {
            this.selectAllChildren(nodeId);
        } else {
            this.deselectAllChildren(nodeId);
        }

        this.updateParentState(nodeState.parentId);
        this.updateNodeVisualState(nodeId);
        this.updateSelectionCounts();
        this.onSelectionChange(this.getSelectionSummary());
    }

    handleStatusChange(nodeId, newStatus) {
        if (this.mode !== 'modify') return;
        
        this.onStatusChange(nodeId, newStatus);
    }

    selectAllChildren(parentId) {
        const parentState = this.selectionState.get(parentId);
        if (!parentState) return;

        parentState.childIds.forEach(childId => {
            const childState = this.selectionState.get(childId);
            if (childState && !childState.selected) {
                childState.selected = true;
                this.updateNodeVisualState(childId);
                this.selectAllChildren(childId);
            }
        });
    }

    deselectAllChildren(parentId) {
        const parentState = this.selectionState.get(parentId);
        if (!parentState) return;

        parentState.childIds.forEach(childId => {
            const childState = this.selectionState.get(childId);
            if (childState && childState.selected) {
                childState.selected = false;
                this.updateNodeVisualState(childId);
                this.deselectAllChildren(childId);
            }
        });
    }

    updateParentState(parentId) {
        if (!parentId) return;

        const parentState = this.selectionState.get(parentId);
        if (!parentState) return;

        const selectedChildren = parentState.childIds.filter(childId => {
            const childState = this.selectionState.get(childId);
            return childState && childState.selected;
        });

        const allSelected = selectedChildren.length === parentState.childIds.length;
        const noneSelected = selectedChildren.length === 0;

        if (allSelected) {
            parentState.selected = true;
            parentState.partiallySelected = false;
        } else if (noneSelected) {
            parentState.selected = false;
            parentState.partiallySelected = false;
        } else {
            parentState.selected = false;
            parentState.partiallySelected = true;
        }

        this.updateNodeVisualState(parentId);
        this.updateParentState(parentState.parentId);
    }

    updateNodeVisualState(nodeId) {
        const nodeState = this.selectionState.get(nodeId);
        if (!nodeState) return;

        const nodeElement = this.container.querySelector(`[data-item-id="${nodeId}"]`);
        if (!nodeElement) return;

        const checkbox = nodeElement.querySelector('.tree-checkbox');
        if (checkbox) {
            checkbox.checked = nodeState.selected;
            checkbox.indeterminate = nodeState.partiallySelected || false;
            if (nodeState.partiallySelected) {
                checkbox.classList.add('indeterminate');
            } else {
                checkbox.classList.remove('indeterminate');
            }
        }

        nodeElement.classList.remove('selected', 'partially-selected');
        if (nodeState.selected) {
            nodeElement.classList.add('selected');
        } else if (nodeState.partiallySelected) {
            nodeElement.classList.add('partially-selected');
        }
    }

    updateSelectionCounts() {
        this.selectionCounts = {
            products: 0,
            parts: 0,
            subassemblies: 0,
            hardware: 0,
            nestSheets: 0,
            detachedProducts: 0
        };

        this.selectionState.forEach(state => {
            if (state.selected) {
                switch (state.type) {
                    case 'product':
                        this.selectionCounts.products++;
                        break;
                    case 'part':
                        this.selectionCounts.parts++;
                        break;
                    case 'subassembly':
                        this.selectionCounts.subassemblies++;
                        break;
                    case 'hardware':
                        this.selectionCounts.hardware++;
                        break;
                    case 'nestsheet':
                        this.selectionCounts.nestSheets++;
                        break;
                    case 'detached_product':
                        this.selectionCounts.detachedProducts++;
                        break;
                }
            }
        });
    }

    getSelectionSummary() {
        return {
            counts: { ...this.selectionCounts },
            selectedItems: Array.from(this.selectionState.entries())
                .filter(([_, state]) => state.selected)
                .map(([id, state]) => ({ id, type: state.type, data: state.itemData }))
        };
    }

    // Control methods
    selectAllProducts() {
        this.selectionState.forEach((state, nodeId) => {
            if ((state.type === 'product' || state.type === 'hardware' || state.type === 'detached') && !state.selected) {
                state.selected = true;
                this.updateNodeVisualState(nodeId);
                this.selectAllChildren(nodeId);
            }
        });
        this.updateSelectionCounts();
        this.onSelectionChange(this.getSelectionSummary());
    }

    selectAllNestSheets() {
        this.selectionState.forEach((state, nodeId) => {
            if (state.type === 'nestsheet' && !state.selected) {
                state.selected = true;
                this.updateNodeVisualState(nodeId);
                this.selectAllChildren(nodeId);
            }
        });
        this.updateSelectionCounts();
        this.onSelectionChange(this.getSelectionSummary());
    }

    clearAllSelections() {
        this.selectionState.forEach((state, nodeId) => {
            if (state.selected) {
                state.selected = false;
                state.partiallySelected = false;
                this.updateNodeVisualState(nodeId);
            }
        });
        this.updateSelectionCounts();
        this.onSelectionChange(this.getSelectionSummary());
    }

    expandAll() {
        const toggles = this.container.querySelectorAll('.tree-toggle');
        toggles.forEach(toggle => {
            toggle.dataset.expanded = 'true';
            toggle.querySelector('i').className = 'fas fa-chevron-down';
            const content = toggle.closest('.tree-node').querySelector('.tree-content');
            if (content) content.style.display = 'block';
        });
    }

    collapseAll() {
        const toggles = this.container.querySelectorAll('.tree-toggle');
        toggles.forEach(toggle => {
            toggle.dataset.expanded = 'false';
            toggle.querySelector('i').className = 'fas fa-chevron-right';
            const content = toggle.closest('.tree-node').querySelector('.tree-content');
            if (content) content.style.display = 'none';
        });
    }

    filterTree(searchTerm) {
        const nodes = this.container.querySelectorAll('.tree-node');
        const lowerSearchTerm = searchTerm.toLowerCase();
        
        nodes.forEach(node => {
            const text = node.textContent.toLowerCase();
            const shouldShow = searchTerm === '' || text.includes(lowerSearchTerm);
            node.style.display = shouldShow ? 'block' : 'none';
        });
    }

    showError(message) {
        const treeContent = this.container.querySelector('#treeViewContent');
        treeContent.innerHTML = `
            <div class="alert alert-danger">
                <i class="fas fa-exclamation-triangle me-2"></i>
                ${message}
            </div>
        `;
    }

    // Public API methods
    getSelectedItems() {
        return Array.from(this.selectionState.entries())
            .filter(([_, state]) => state.selected)
            .map(([id, state]) => ({ id, type: state.type, data: state.itemData }));
    }

    setMode(mode) {
        this.mode = mode;
        this.createTreeContainer();
        if (this.data) {
            this.renderTree();
        }
    }

    refresh() {
        if (this.mode === 'modify' && this.apiUrl && this.workOrderId) {
            this.loadData();
        } else if (this.data) {
            this.renderTree();
        }
    }
}