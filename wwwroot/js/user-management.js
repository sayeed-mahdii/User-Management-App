document.addEventListener('DOMContentLoaded', function () {
    // 1. Initialize Bootstrap Tooltips
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    const selectAllCheckbox = document.getElementById('selectAllCheckbox');
    const userCheckboxes = document.querySelectorAll('.user-checkbox');
    const btnBlock = document.getElementById('btnBlock');
    const btnUnblock = document.getElementById('btnUnblock');
    const btnDelete = document.getElementById('btnDelete');
    const btnDeleteUnverified = document.getElementById('btnDeleteUnverified');
    const tableFilter = document.getElementById('tableFilter');
    const alertContainer = document.getElementById('statusAlertContainer');

    // 2. Function to show status messages in UI (No browser alert popups)
    function showStatusMessage(message, type = 'success') {
        alertContainer.innerHTML = `
            <div class="alert alert-${type} alert-dismissible fade show shadow-sm" role="alert">
                <i class="bi ${type === 'success' ? 'bi-check-circle-fill' : 'bi-exclamation-triangle-fill'} me-2"></i>
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
            </div>
        `;
    }

    // 3. Update Toolbar button states based on selection
    function updateToolbarState() {
        const selectedCount = document.querySelectorAll('.user-checkbox:checked').length;
        const hasSelection = selectedCount > 0;

        if (btnBlock) btnBlock.disabled = !hasSelection;
        if (btnUnblock) btnUnblock.disabled = !hasSelection;
        if (btnDelete) btnDelete.disabled = !hasSelection;

        // Update indeterminate state on header checkbox
        if (selectAllCheckbox && userCheckboxes.length > 0) {
            if (selectedCount === userCheckboxes.length) {
                selectAllCheckbox.checked = true;
                selectAllCheckbox.indeterminate = false;
            } else if (selectedCount > 0) {
                selectAllCheckbox.checked = false;
                selectAllCheckbox.indeterminate = true;
            } else {
                selectAllCheckbox.checked = false;
                selectAllCheckbox.indeterminate = false;
            }
        }
    }

    // 4. Header Checkbox: Select / Deselect All
    if (selectAllCheckbox) {
        selectAllCheckbox.addEventListener('change', function () {
            const isChecked = this.checked;
            userCheckboxes.forEach(cb => {
                // Only select rows that are visible after filter
                if (cb.closest('tr').style.display !== 'none') {
                    cb.checked = isChecked;
                }
            });
            updateToolbarState();
        });
    }

    // 5. Individual Checkbox changes
    userCheckboxes.forEach(cb => {
        cb.addEventListener('change', updateToolbarState);
    });

    // 6. Filter / Search functionality
    if (tableFilter) {
        tableFilter.addEventListener('keyup', function () {
            const query = this.value.toLowerCase();
            const rows = document.querySelectorAll('#usersTable tbody tr:not(#noRecordsRow)');
            rows.forEach(row => {
                const text = row.innerText.toLowerCase();
                row.style.display = text.includes(query) ? '' : 'none';
            });
        });
    }

    // 7. Interactive Column Sorting (Sensible order adjustable by end-users)
    let sortDirections = {};
    document.querySelectorAll('.sortable-th').forEach(header => {
        header.addEventListener('click', function () {
            const colIndex = parseInt(this.getAttribute('data-col'));
            const currentDir = sortDirections[colIndex] || 'desc';
            const nextDir = currentDir === 'asc' ? 'desc' : 'asc';
            sortDirections[colIndex] = nextDir;

            // Reset all header icons
            document.querySelectorAll('.sortable-th i').forEach(icon => {
                icon.className = 'bi bi-arrow-down-up text-muted small ms-1';
            });

            // Update current header icon
            const icon = this.querySelector('i');
            if (icon) {
                icon.className = nextDir === 'asc' ? 'bi bi-sort-up text-primary small ms-1' : 'bi bi-sort-down text-primary small ms-1';
            }

            const tbody = document.querySelector('#usersTable tbody');
            const rows = Array.from(tbody.querySelectorAll('tr:not(#noRecordsRow)'));

            rows.sort((a, b) => {
                let cellA = a.children[colIndex];
                let cellB = b.children[colIndex];
                let valA = cellA.querySelector('[data-timestamp]') ? parseInt(cellA.querySelector('[data-timestamp]').getAttribute('data-timestamp')) : cellA.innerText.trim();
                let valB = cellB.querySelector('[data-timestamp]') ? parseInt(cellB.querySelector('[data-timestamp]').getAttribute('data-timestamp')) : cellB.innerText.trim();

                if (typeof valA === 'number' && typeof valB === 'number') {
                    return nextDir === 'asc' ? valA - valB : valB - valA;
                }

                return nextDir === 'asc' 
                    ? valA.toString().localeCompare(valB.toString()) 
                    : valB.toString().localeCompare(valA.toString());
            });

            rows.forEach(row => tbody.appendChild(row));
        });
    });

    // 8. Get Selected User IDs
    function getSelectedUserIds() {
        const selected = [];
        document.querySelectorAll('.user-checkbox:checked').forEach(cb => {
            selected.push(parseInt(cb.value));
        });
        return selected;
    }

    // 9. Execute Toolbar Batch Action
    function executeAction(actionName) {
        const userIds = getSelectedUserIds();
        if (actionName !== 'deleteunverified' && userIds.length === 0) {
            showStatusMessage('Please select at least one user.', 'warning');
            return;
        }

        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput ? tokenInput.value : '';

        // Disable buttons during request
        if (btnBlock) btnBlock.disabled = true;
        if (btnUnblock) btnUnblock.disabled = true;
        if (btnDelete) btnDelete.disabled = true;
        if (btnDeleteUnverified) btnDeleteUnverified.disabled = true;

        fetch('/Home/ExecuteAction', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token,
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({
                userIds: userIds,
                action: actionName
            })
        })
        .then(res => {
            if (res.status === 401) {
                // Current user was blocked/deleted (Requirement #5) -> redirect to login
                window.location.href = '/login?reason=blocked';
                return null;
            }
            return res.json();
        })
        .then(data => {
            if (!data) return;

            if (data.success) {
                if (data.currentAffected) {
                    // User modified their own status or deleted themselves
                    window.location.href = '/login?reason=blocked';
                    return;
                }
                showStatusMessage(data.message, 'success');
                // Reload table seamlessly after small delay to show message
                setTimeout(() => {
                    window.location.reload();
                }, 600);
            } else {
                showStatusMessage(data.message || 'Operation failed.', 'danger');
                updateToolbarState();
                if (btnDeleteUnverified) btnDeleteUnverified.disabled = false;
            }
        })
        .catch(err => {
            console.error(err);
            showStatusMessage('An error occurred while processing the request.', 'danger');
            updateToolbarState();
            if (btnDeleteUnverified) btnDeleteUnverified.disabled = false;
        });
    }

    // 10. Attach click events to toolbar buttons
    if (btnBlock) btnBlock.addEventListener('click', () => executeAction('block'));
    if (btnUnblock) btnUnblock.addEventListener('click', () => executeAction('unblock'));
    if (btnDelete) btnDelete.addEventListener('click', () => executeAction('delete'));
    if (btnDeleteUnverified) btnDeleteUnverified.addEventListener('click', () => executeAction('deleteunverified'));
});
