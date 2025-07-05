/**
 * Unified Work Order Tree View Component
 * Supports multiple modes: import, modify, view
 * Based on successful Import interface pattern with modification capabilities
 */
class WorkOrderTreeView {
    constructor(containerId, options = {}) {
        this.container = document.getElementById(containerId);
        if (!this.container) {
            throw new Error(`Container element with ID '${containerId}' not found`);
        }

        // Configuration options
        this.options = {
            mode: options.mode || 'view', // 'import', 'modify', 'view'
            workOrderId: options.workOrderId || null,
            enableSelection: options.enableSelection !== false, // Default true
            enableModification: options.mode === 'modify',
            enableBulkActions: options.enableBulkActions !== false,
            pageSize: options.pageSize || 100,
            ...options
        };

        // State management
        this.selectionState = new Map();
        this.expandedNodes = new Set();
        this.loadedNodes = new Set();
        this.statusOptions = ['Pending', 'Cut', 'Sorted', 'Assembled', 'Shipped'];

        // Event handlers
        this.eventHandlers = {
            nodeSelected: options.onNodeSelected || (() => {}),
            statusChanged: options.onStatusChanged || (() => {}),
            bulkAction: options.onBulkAction || (() => {})
        };

        this.initialize();
    }

    async initialize() {
        this.setupEventListeners();
        
        if (this.options.workOrderId) {
            await this.loadWorkOrderData();
        }
    }

    setupEventListeners() {
        // Tree toggle events
        this.container.addEventListener('click', (e) => {
            if (e.target.closest('.tree-toggle')) {
                e.preventDefault();
                this.handleTreeToggle(e.target.closest('.tree-toggle'));
            }
        });

        // Selection events
        if (this.options.enableSelection) {
            this.container.addEventListener('change', (e) => {
                if (e.target.classList.contains('tree-checkbox')) {
                    this.handleNodeSelection(e.target);
                }
            });
        }

        // Status change events
        if (this.options.enableModification) {
            this.container.addEventListener('change', (e) => {
                if (e.target.classList.contains('status-select')) {
                    this.handleStatusChange(e.target);
                }
            });
        }
    }

    async loadWorkOrderData(page = 0) {
        try {
            this.showLoading(true);
            
            const response = await fetch(`/api/workorder/${this.options.workOrderId}/tree?page=${page}&size=${this.options.pageSize}`);
            if (!response.ok) {
                throw new Error('Failed to load work order data');
            }

            const data = await response.json();
            if (data.success) {
                this.renderTree(data.data);
            } else {
                throw new Error(data.message || 'Unknown error loading data');
            }
        } catch (error) {
            console.error('Error loading work order data:', error);
            this.showError('Failed to load work order data: ' + error.message);
        } finally {
            this.showLoading(false);
        }
    }

    renderTree(data) {
        this.container.innerHTML = '';
        
        if (!data || !data.workOrder) {
            this.container.innerHTML = '<div class="text-muted p-3">No work order data available</div>';
            return;
        }

        // Create tree structure
        const treeContent = document.createElement('div');
        treeContent.className = 'tree-content';

        // Work order header
        const workOrderNode = this.createWorkOrderNode(data.workOrder);
        treeContent.appendChild(workOrderNode);

        // Create work order children container
        const workOrderChildren = this.createNodesContainer('work-order-children');

        // Products section
        if (data.productNodes && data.productNodes.length > 0) {
            const productsHeader = this.createSectionHeader('Products', '📦', data.productNodes.length);
            workOrderChildren.appendChild(productsHeader);

            const productsContainer = this.createNodesContainer('products-container');
            data.productNodes.forEach(productNode => {
                const productElement = this.createProductNode(productNode);
                productsContainer.appendChild(productElement);
            });
            workOrderChildren.appendChild(productsContainer);
        }

        // Hardware section
        if (data.workOrder.hardware && data.workOrder.hardware.length > 0) {
            const hardwareHeader = this.createSectionHeader('Hardware', '🔧', data.workOrder.hardware.length);
            workOrderChildren.appendChild(hardwareHeader);

            const hardwareContainer = this.createNodesContainer('hardware-container');
            data.workOrder.hardware.forEach(hardware => {
                const hardwareElement = this.createHardwareNode(hardware);
                hardwareContainer.appendChild(hardwareElement);
            });
            workOrderChildren.appendChild(hardwareContainer);
        }

        // Detached products section
        if (data.workOrder.detachedProducts && data.workOrder.detachedProducts.length > 0) {
            const detachedHeader = this.createSectionHeader('Detached Products', '📄', data.workOrder.detachedProducts.length);
            workOrderChildren.appendChild(detachedHeader);

            const detachedContainer = this.createNodesContainer('detached-products-container');
            data.workOrder.detachedProducts.forEach(detached => {
                const detachedElement = this.createDetachedProductNode(detached);
                detachedContainer.appendChild(detachedElement);
            });
            workOrderChildren.appendChild(detachedContainer);
        }

        // Append the work order children to the tree
        treeContent.appendChild(workOrderChildren);

        this.container.appendChild(treeContent);
        this.initializeNodeStates();
    }

    createWorkOrderNode(workOrder) {
        const node = document.createElement('div');
        node.className = 'tree-node level-0 work-order-node';
        node.dataset.type = 'work-order';
        node.dataset.itemId = workOrder.id;

        node.innerHTML = `
            <div class="tree-item-content">
                <span class="tree-toggle" data-target="work-order-children">
                    <i class="fas fa-chevron-down"></i>
                </span>
                <i class="fas fa-briefcase item-type-icon text-primary"></i>
                <div class="item-info">
                    <div class="item-name">${workOrder.name}</div>
                    <div class="item-details">
                        Work Order ${workOrder.id} • Imported ${new Date(workOrder.importedDate).toLocaleDateString()}
                    </div>
                </div>
                ${this.createNodeControls('work-order', workOrder)}
            </div>
        `;

        return node;
    }

    createSectionHeader(title, icon, count) {
        const header = document.createElement('div');
        header.className = 'tree-node level-1 section-header';
        header.dataset.type = 'section-header';
        header.dataset.itemId = `${title.toLowerCase()}-header`;

        const targetId = title === 'Detached Products' ? 'detached-products-container' : `${title.toLowerCase()}-container`;
        
        header.innerHTML = `
            <div class="tree-item-content">
                <span class="tree-toggle" data-target="${targetId}">
                    <i class="fas fa-chevron-down"></i>
                </span>
                <i class="${icon} item-type-icon text-secondary"></i>
                <div class="item-info">
                    <div class="item-name">${title}</div>
                    <div class="item-details">${count} items</div>
                </div>
                ${this.createNodeControls('section-header', { title, count })}
            </div>
        `;

        return header;
    }

    createProductNode(productNode) {
        const product = productNode.product;
        const node = document.createElement('div');
        node.className = 'tree-node level-2 product-node';
        node.dataset.type = 'product';
        node.dataset.itemId = product.id;

        const effectiveStatus = productNode.effectiveStatus || 'Pending';
        
        node.innerHTML = `
            <div class="tree-item-content">
                <span class="tree-toggle" data-target="product-${product.id}">
                    <i class="fas fa-chevron-down"></i>
                </span>
                <i class="fas fa-cube item-type-icon text-primary"></i>
                <div class="item-info">
                    <div class="item-name">${product.name}</div>
                    <div class="item-details">
                        Product #${product.productNumber} • 
                        ${productNode.parts ? productNode.parts.length : 0} parts • 
                        ${productNode.subassemblies ? productNode.subassemblies.length : 0} subassemblies
                    </div>
                    <div class="product-status-summary">
                        Effective Status: <span class="badge status-${effectiveStatus.toLowerCase()}">${effectiveStatus}</span>
                    </div>
                </div>
                ${this.createNodeControls('product', product, effectiveStatus)}
            </div>
            <div id="product-${product.id}" class="tree-children">
                ${this.createProductChildren(productNode)}
            </div>
        `;

        return node;
    }

    createProductChildren(productNode) {
        let childrenHtml = '';

        // Direct parts
        if (productNode.parts && productNode.parts.length > 0) {
            productNode.parts.forEach(part => {
                childrenHtml += this.createPartNodeHtml(part, 3);
            });
        }

        // Subassemblies
        if (productNode.subassemblies && productNode.subassemblies.length > 0) {
            productNode.subassemblies.forEach(subassembly => {
                childrenHtml += this.createSubassemblyNodeHtml(subassembly, 3);
            });
        }

        return childrenHtml;
    }

    createPartNodeHtml(part, level) {
        return `
            <div class="tree-node level-${level} part-node" data-type="part" data-item-id="${part.id}">
                <div class="tree-item-content">
                    <span class="tree-toggle"></span>
                    <i class="fas fa-puzzle-piece item-type-icon text-success"></i>
                    <div class="item-info">
                        <div class="item-name">${part.name}</div>
                        <div class="item-details">
                            Qty: ${part.qty} • ${part.length}mm × ${part.width}mm × ${part.thickness}mm • ${part.material}
                        </div>
                    </div>
                    ${this.createNodeControls('part', part, part.status)}
                </div>
            </div>
        `;
    }

    createSubassemblyNodeHtml(subassembly, level) {
        let subassemblyHtml = `
            <div class="tree-node level-${level}" data-type="subassembly" data-item-id="${subassembly.id}">
                <div class="tree-item-content">
                    <span class="tree-toggle" data-target="sub-${subassembly.id}">
                        <i class="fas fa-chevron-down"></i>
                    </span>
                    <i class="fas fa-layer-group item-type-icon text-info"></i>
                    <div class="item-info">
                        <div class="item-name">${subassembly.name}</div>
                        <div class="item-details">
                            Subassembly • Qty: ${subassembly.qty} • ${subassembly.parts ? subassembly.parts.length : 0} parts
                        </div>
                    </div>
                    ${this.createNodeControls('subassembly', subassembly)}
                </div>
                <div id="sub-${subassembly.id}" class="tree-children">
        `;

        // Subassembly parts
        if (subassembly.parts && subassembly.parts.length > 0) {
            subassembly.parts.forEach(part => {
                subassemblyHtml += this.createPartNodeHtml(part, level + 1);
            });
        }

        subassemblyHtml += `
                </div>
            </div>
        `;

        return subassemblyHtml;
    }

    createHardwareNode(hardware) {
        const node = document.createElement('div');
        node.className = 'tree-node level-2 hardware-node';
        node.dataset.type = 'hardware';
        node.dataset.itemId = hardware.id;

        const status = hardware.isShipped ? 'Shipped' : 'Pending';

        node.innerHTML = `
            <div class="tree-item-content">
                <span class="tree-toggle"></span>
                <i class="fas fa-cog item-type-icon text-warning"></i>
                <div class="item-info">
                    <div class="item-name">${hardware.name}</div>
                    <div class="item-details">Qty: ${hardware.qty}</div>
                </div>
                ${this.createNodeControls('hardware', hardware, status)}
            </div>
        `;

        return node;
    }

    createDetachedProductNode(detached) {
        const node = document.createElement('div');
        node.className = 'tree-node level-2 detached-node';
        node.dataset.type = 'detachedproduct';
        node.dataset.itemId = detached.id;

        const status = detached.isShipped ? 'Shipped' : 'Pending';

        node.innerHTML = `
            <div class="tree-item-content">
                <span class="tree-toggle"></span>
                <i class="fas fa-box-open item-type-icon text-secondary"></i>
                <div class="item-info">
                    <div class="item-name">${detached.name}</div>
                    <div class="item-details">
                        Product #${detached.productNumber} • Qty: ${detached.qty} • 
                        ${detached.length}mm × ${detached.width}mm × ${detached.thickness}mm
                    </div>
                </div>
                ${this.createNodeControls('detachedproduct', detached, status)}
            </div>
        `;

        return node;
    }

    createNodeControls(type, item, status = null) {
        let controls = '';

        // Selection checkbox
        if (this.options.enableSelection) {
            controls += `
                <input type="checkbox" class="tree-checkbox form-check-input" 
                       value="${item.id}" data-item-type="${type}">
            `;
        }

        // Status controls for modification mode
        if (this.options.enableModification && status !== null) {
            const statusOptions = type === 'hardware' || type === 'detachedproduct' 
                ? ['Pending', 'Shipped']
                : this.statusOptions;

            controls += `
                <div class="status-controls">
                    <span class="badge status-${status.toLowerCase()} me-2">${status}</span>
                    <select class="form-select status-select" data-item-id="${item.id}" data-item-type="${type}">
                        ${statusOptions.map(opt => `
                            <option value="${opt}" ${opt === status ? 'selected' : ''}>${opt}</option>
                        `).join('')}
                    </select>
                    ${type === 'product' ? `
                        <div class="form-check cascade-option">
                            <input type="checkbox" class="form-check-input cascade-checkbox" 
                                   id="cascade-${item.id}" checked>
                            <label class="form-check-label" for="cascade-${item.id}">
                                <small>Cascade</small>
                            </label>
                        </div>
                    ` : ''}
                </div>
            `;
        }

        return controls ? `<div class="node-controls">${controls}</div>` : '';
    }

    createNodesContainer(id) {
        const container = document.createElement('div');
        container.id = id;
        container.className = 'tree-children';
        container.style.display = 'block';
        return container;
    }

    handleTreeToggle(toggleElement) {
        const targetId = toggleElement.dataset.target;
        if (!targetId) return;

        const targetElement = document.getElementById(targetId);
        if (!targetElement) return;

        const icon = toggleElement.querySelector('i');
        const isExpanded = targetElement.style.display !== 'none';

        if (isExpanded) {
            targetElement.style.display = 'none';
            icon.className = 'fas fa-chevron-right';
            this.expandedNodes.delete(targetId);
        } else {
            targetElement.style.display = 'block';
            icon.className = 'fas fa-chevron-down';
            this.expandedNodes.add(targetId);
        }
    }

    handleNodeSelection(checkbox) {
        const nodeId = checkbox.value;
        const itemType = checkbox.dataset.itemType;
        const isSelected = checkbox.checked;

        // Update selection state
        this.updateSelectionState(nodeId, itemType, isSelected);

        // Fire event
        this.eventHandlers.nodeSelected({
            nodeId,
            itemType,
            isSelected,
            selectionState: this.getSelectionSummary()
        });
    }

    async handleStatusChange(selectElement) {
        const itemId = selectElement.dataset.itemId;
        const itemType = selectElement.dataset.itemType;
        const newStatus = selectElement.value;
        const cascadeElement = selectElement.closest('.tree-node').querySelector('.cascade-checkbox');
        const cascadeToChildren = cascadeElement ? cascadeElement.checked : false;

        try {
            // Show loading state
            selectElement.disabled = true;

            // Call status update API
            const response = await fetch('/Admin/UpdateStatus', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: new URLSearchParams({
                    itemId,
                    itemType,
                    newStatus,
                    cascadeToChildren,
                    workOrderId: this.options.workOrderId
                })
            });

            const result = await response.json();

            if (result.success) {
                // Update UI to reflect changes
                this.updateStatusBadge(itemId, itemType, newStatus);
                if (cascadeToChildren && itemType === 'product') {
                    this.updateChildStatusBadges(itemId, newStatus);
                }

                // Fire event
                this.eventHandlers.statusChanged({
                    itemId,
                    itemType,
                    newStatus,
                    cascadeToChildren,
                    success: true,
                    message: result.message
                });
            } else {
                // Revert selection on failure
                this.revertStatusSelect(itemId, itemType);
                
                this.eventHandlers.statusChanged({
                    itemId,
                    itemType,
                    newStatus,
                    success: false,
                    message: result.message
                });
            }
        } catch (error) {
            console.error('Error updating status:', error);
            this.revertStatusSelect(itemId, itemType);
            
            this.eventHandlers.statusChanged({
                itemId,
                itemType,
                newStatus,
                success: false,
                message: 'Network error occurred'
            });
        } finally {
            selectElement.disabled = false;
        }
    }

    updateSelectionState(nodeId, itemType, isSelected) {
        this.selectionState.set(nodeId, {
            selected: isSelected,
            type: itemType,
            nodeId: nodeId
        });
    }

    getSelectionSummary() {
        const summary = {
            total: 0,
            selected: 0,
            byType: {}
        };

        this.selectionState.forEach((state, nodeId) => {
            summary.total++;
            if (state.selected) {
                summary.selected++;
            }
            
            if (!summary.byType[state.type]) {
                summary.byType[state.type] = { total: 0, selected: 0 };
            }
            summary.byType[state.type].total++;
            if (state.selected) {
                summary.byType[state.type].selected++;
            }
        });

        return summary;
    }

    updateStatusBadge(itemId, itemType, newStatus) {
        const node = this.container.querySelector(`[data-item-id="${itemId}"][data-type="${itemType}"]`);
        if (!node) return;

        const badge = node.querySelector('.badge');
        if (badge) {
            badge.className = `badge status-${newStatus.toLowerCase()} me-2`;
            badge.textContent = newStatus;
        }
    }

    updateChildStatusBadges(productId, newStatus) {
        const productNode = this.container.querySelector(`[data-item-id="${productId}"][data-type="product"]`);
        if (!productNode) return;

        const childBadges = productNode.querySelectorAll('.tree-children .badge');
        childBadges.forEach(badge => {
            badge.className = `badge status-${newStatus.toLowerCase()} me-2`;
            badge.textContent = newStatus;
        });
    }

    revertStatusSelect(itemId, itemType) {
        const select = this.container.querySelector(`[data-item-id="${itemId}"][data-item-type="${itemType}"]`);
        if (!select) return;

        const badge = select.closest('.tree-node').querySelector('.badge');
        if (badge) {
            const currentStatus = badge.textContent;
            select.value = currentStatus;
        }
    }

    initializeNodeStates() {
        // Initialize selection states for all checkboxes
        const checkboxes = this.container.querySelectorAll('.tree-checkbox');
        checkboxes.forEach(checkbox => {
            this.updateSelectionState(checkbox.value, checkbox.dataset.itemType, checkbox.checked);
        });
    }

    showLoading(show) {
        if (show) {
            this.container.innerHTML = `
                <div class="text-center p-4">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <div class="mt-2">Loading work order data...</div>
                </div>
            `;
        }
    }

    showError(message) {
        this.container.innerHTML = `
            <div class="alert alert-danger" role="alert">
                <i class="fas fa-exclamation-triangle me-2"></i>
                ${message}
            </div>
        `;
    }

    // Public API methods
    expandAll() {
        const toggles = this.container.querySelectorAll('.tree-toggle[data-target]');
        toggles.forEach(toggle => {
            const targetId = toggle.dataset.target;
            const targetElement = document.getElementById(targetId);
            if (targetElement && targetElement.style.display === 'none') {
                this.handleTreeToggle(toggle);
            }
        });
    }

    collapseAll() {
        const toggles = this.container.querySelectorAll('.tree-toggle[data-target]');
        toggles.forEach(toggle => {
            const targetId = toggle.dataset.target;
            const targetElement = document.getElementById(targetId);
            if (targetElement && targetElement.style.display !== 'none') {
                this.handleTreeToggle(toggle);
            }
        });
    }

    getSelectedItems() {
        const selected = [];
        this.selectionState.forEach((state, nodeId) => {
            if (state.selected) {
                selected.push(state);
            }
        });
        return selected;
    }

    selectAll() {
        const checkboxes = this.container.querySelectorAll('.tree-checkbox');
        checkboxes.forEach(checkbox => {
            if (!checkbox.checked) {
                checkbox.checked = true;
                this.handleNodeSelection(checkbox);
            }
        });
    }

    selectNone() {
        const checkboxes = this.container.querySelectorAll('.tree-checkbox');
        checkboxes.forEach(checkbox => {
            if (checkbox.checked) {
                checkbox.checked = false;
                this.handleNodeSelection(checkbox);
            }
        });
    }

    filterNodes(searchTerm) {
        const nodes = this.container.querySelectorAll('.tree-node');
        
        if (!searchTerm) {
            nodes.forEach(node => node.style.display = '');
            return;
        }

        const term = searchTerm.toLowerCase();
        
        nodes.forEach(node => {
            const text = node.querySelector('.item-name, .item-details')?.textContent?.toLowerCase() || '';
            const matches = text.includes(term);
            
            if (matches || node.classList.contains('level-0')) {
                node.style.display = '';
            } else {
                node.style.display = 'none';
            }
        });

        // Show parent nodes if children match
        const visibleNodes = this.container.querySelectorAll('.tree-node[style=""], .tree-node:not([style])');
        visibleNodes.forEach(node => {
            let parent = node.parentElement;
            while (parent && parent !== this.container) {
                if (parent.classList.contains('tree-node')) {
                    parent.style.display = '';
                }
                parent = parent.parentElement;
            }
        });
    }
}

// Export for use
if (typeof module !== 'undefined' && module.exports) {
    module.exports = WorkOrderTreeView;
} else if (typeof window !== 'undefined') {
    window.WorkOrderTreeView = WorkOrderTreeView;
}