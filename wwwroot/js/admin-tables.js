/**
 * Admin tables: live search, optional filters, client-side pagination.
 * Markup: data-admin-table on .admin-table-card or .admin-table-group wrapper.
 */
(function () {
    'use strict';

    var DEFAULT_PAGE_SIZE = 10;
    var PAGE_SIZES = [5, 10, 25, 50];

    function norm(s) {
        return (s || '').toString().trim().toLowerCase();
    }

    function getFilterKey(btn) {
        if (btn.dataset.adminFilter) return btn.dataset.adminFilter;
        if (btn.dataset.kyc !== undefined) return 'kyc';
        if (btn.dataset.status !== undefined) return 'status';
        if (btn.dataset.level !== undefined) return 'level';
        return null;
    }

    function getFilterValue(btn) {
        if (btn.dataset.adminFilterValue !== undefined) return btn.dataset.adminFilterValue;
        if (btn.dataset.kyc !== undefined) return btn.dataset.kyc;
        if (btn.dataset.status !== undefined) return btn.dataset.status;
        if (btn.dataset.level !== undefined) return btn.dataset.level;
        return 'all';
    }

    function AdminTable(root) {
        this.root = root;
        this.pageSize = parseInt(root.dataset.pageSize, 10) || DEFAULT_PAGE_SIZE;
        this.currentPage = 1;
        this.filters = {};
        this.card = root.classList.contains('admin-table-card')
            ? root
            : root.querySelector('.admin-table-card') || root;
        this.scroll = this.card.querySelector('.admin-table-scroll');
        this.table = this.card.querySelector('table.admin-table');
        this.tbody = this.table && this.table.querySelector('tbody');
        this.countBadge = this.card.querySelector('[data-admin-table-count]')
            || this.card.querySelector('.admin-record-badge');
        this.noResultsEl = root.querySelector('[data-admin-table-empty]');
        this.searchInput = root.querySelector('[data-admin-table-search]');
        this.dataRows = [];
        this.detailByMain = new Map();
        this.init();
    }

    AdminTable.prototype.init = function () {
        if (!this.tbody) {
            if (!this.searchInput) return;
            if (!this.searchInput.dataset.bound) {
                this.searchInput.dataset.bound = '1';
                this.searchInput.addEventListener('input', this.refreshEmptyOnly.bind(this));
            }
            this.buildEmptyPanel();
            this.bindFilterButtons();
            this.bindClearButtons();
            return;
        }

        this.collectRows();
        if (!this.dataRows.length) {
            if (this.searchInput && !this.searchInput.dataset.bound) {
                this.searchInput.dataset.bound = '1';
                this.searchInput.addEventListener('input', this.refresh.bind(this));
            }
            return;
        }

        if (!this.searchInput) {
            this.injectSearch();
        } else {
            this.searchInput.setAttribute('autocomplete', 'off');
            if (!this.searchInput.dataset.bound) {
                this.searchInput.dataset.bound = '1';
                this.searchInput.addEventListener('input', this.refresh.bind(this));
            }
        }

        this.bindFilterButtons();
        this.buildFooter();
        this.buildEmptyPanel();
        this.bindClearButtons();
        this.refresh();
    };

    AdminTable.prototype.bindClearButtons = function () {
        var self = this;
        this.root.querySelectorAll('[data-admin-table-clear-static]').forEach(function (btn) {
            if (btn.dataset.adminTableBound) return;
            btn.dataset.adminTableBound = '1';
            btn.addEventListener('click', function () { self.clearFilters(); });
        });
    };

    AdminTable.prototype.clearFilters = function () {
        if (this.searchInput) this.searchInput.value = '';
        this.filters = {};
        this.root.querySelectorAll(
            '[data-admin-filter], .admin-filter-btn[data-kyc], .comm-filter-btn[data-level], .tickets-filter-btn[data-status]'
        ).forEach(function (btn) {
            var key = getFilterKey(btn);
            if (!key) return;
            btn.classList.remove('active');
            if (getFilterValue(btn) === 'all') btn.classList.add('active');
        });
        this.currentPage = 1;
        this.refreshEmptyOnly();
        if (this.dataRows && this.dataRows.length) this.refresh();
        if (this.searchInput) this.searchInput.focus();
    };

    AdminTable.prototype.refreshEmptyOnly = function () {
        var q = norm(this.searchInput && this.searchInput.value);
        var hasFilter = !!q || Object.keys(this.filters).some(function (k) {
            return this.filters[k] && this.filters[k] !== 'all';
        }, this);
        var queueEmpty = this.root.querySelector('[data-admin-table-empty-queue]');
        if (queueEmpty && this.emptyPanel && queueEmpty === this.emptyPanel) {
            return;
        }
        if (queueEmpty) queueEmpty.classList.toggle('d-none', hasFilter);
        if (this.emptyPanel) {
            this.emptyPanel.classList.toggle('d-none', !hasFilter);
        }
    };

    AdminTable.prototype.buildEmptyPanel = function () {
        if (this.emptyPanel) return;

        var existing = this.root.querySelector('[data-admin-table-empty-queue]');
        if (existing) {
            this.emptyPanel = existing.classList.contains('admin-table-no-results')
                ? existing
                : existing.closest('.admin-table-no-results');
            return;
        }

        var self = this;
        var hint = this.root.dataset.emptyHint || 'Try a different keyword or clear filters to see all records.';
        var panel = document.createElement('div');
        panel.className = 'admin-table-no-results d-none';
        panel.setAttribute('data-admin-table-no-results', '');
        panel.innerHTML =
            '<div class="admin-table-no-results-card">' +
            '<div class="admin-table-no-results-icon" aria-hidden="true">' +
            '<i class="bi bi-search"></i></div>' +
            '<h6 class="admin-table-no-results-title">No matching records</h6>' +
            '<p class="admin-table-no-results-text">' + hint.replace(/</g, '&lt;') + '</p>' +
            '<button type="button" class="btn admin-table-no-results-btn" data-admin-table-clear-static data-admin-table-clear>' +
            '<i class="bi bi-arrow-counterclockwise me-1"></i>Clear search &amp; filters</button>' +
            '</div>';

        if (this.scroll) {
            this.scroll.classList.add('admin-table-scroll-wrap');
            this.scroll.appendChild(panel);
        } else {
            this.card.appendChild(panel);
        }

        this.emptyPanel = panel;
        this.bindClearButtons();
    };

    AdminTable.prototype.collectRows = function () {
        var self = this;
        var all = Array.from(this.tbody.querySelectorAll(':scope > tr'));
        this.dataRows = [];
        this.detailByMain = new Map();

        all.forEach(function (tr) {
            if (tr.classList.contains('admin-table-detail-row')
                || tr.classList.contains('tickets-detail-row')
                || tr.classList.contains('collapse')) {
                var ticketId = tr.dataset.detailFor
                    || (tr.id && tr.id.indexOf('ticketDetails-') === 0 ? tr.id.replace('ticketDetails-', '') : null);
                if (ticketId) {
                    self.detailByMain.set(String(ticketId), tr);
                }
                return;
            }
            if (tr.classList.contains('admin-table-ignore')) return;
            self.dataRows.push(tr);
        });

        /* Link detail row immediately following main row (tickets pattern) */
        for (var i = 0; i < all.length - 1; i++) {
            var main = all[i];
            var next = all[i + 1];
            if (self.dataRows.indexOf(main) !== -1
                && (next.classList.contains('tickets-detail-row') || next.classList.contains('admin-table-detail-row'))) {
                var id = main.dataset.ticketId || main.dataset.rowId || String(i);
                self.detailByMain.set(String(id), next);
                next.dataset.detailFor = id;
            }
        }
    };

    AdminTable.prototype.injectSearch = function () {
        var placeholder = this.root.dataset.searchPlaceholder || 'Search table...';
        var wrap = document.createElement('div');
        wrap.className = 'admin-search-wrap admin-table-search-wrap';
        wrap.style.minWidth = '200px';
        wrap.style.maxWidth = '280px';
        wrap.innerHTML =
            '<i class="bi bi-search admin-search-icon"></i>' +
            '<input type="search" data-admin-table-search class="form-control admin-search-input" ' +
            'placeholder="' + placeholder.replace(/"/g, '&quot;') + '" autocomplete="off" />';

        var header = this.card.querySelector('.admin-table-header');
        if (header) {
            header.appendChild(wrap);
        } else {
            var panelHeader = this.card.querySelector('.admin-panel-header');
            var toolRow = document.createElement('div');
            toolRow.className = 'admin-table-toolbar-row px-0 mb-3';
            toolRow.appendChild(wrap);
            if (panelHeader) {
                panelHeader.insertAdjacentElement('afterend', toolRow);
            } else if (this.scroll) {
                this.scroll.parentNode.insertBefore(toolRow, this.scroll);
            } else {
                this.card.prepend(toolRow);
            }
        }

        this.searchInput = wrap.querySelector('[data-admin-table-search]');
        this.searchInput.addEventListener('input', this.refresh.bind(this));
    };

    AdminTable.prototype.bindFilterButtons = function () {
        var self = this;
        var buttons = Array.from(this.root.querySelectorAll(
            '[data-admin-filter], .admin-filter-btn[data-kyc], .comm-filter-btn[data-level], .tickets-filter-btn[data-status]'
        ));
        buttons.forEach(function (btn) {
            if (btn.dataset.adminTableBound) return;
            btn.dataset.adminTableBound = '1';
            btn.addEventListener('click', function () {
                var key = getFilterKey(btn);
                if (!key) return;
                buttons.forEach(function (b) {
                    if (getFilterKey(b) === key) b.classList.remove('active');
                });
                btn.classList.add('active');
                self.filters[key] = getFilterValue(btn);
                self.currentPage = 1;
                self.refresh();
            });
            if (btn.classList.contains('active')) {
                var k = getFilterKey(btn);
                if (k) self.filters[k] = getFilterValue(btn);
            }
        });
    };

    AdminTable.prototype.buildFooter = function () {
        if (this.card.querySelector('.admin-table-footer')) return;

        var self = this;
        var footer = document.createElement('div');
        footer.className = 'admin-table-footer';
        footer.innerHTML =
            '<div class="admin-table-footer-info" data-admin-table-info></div>' +
            '<div class="admin-table-footer-controls">' +
            '<label class="admin-table-page-size-label">' +
            '<span class="d-none d-sm-inline">Rows</span>' +
            '<select data-admin-table-page-size class="form-select form-select-sm admin-table-page-size">' +
            PAGE_SIZES.map(function (n) {
                var sel = n === self.pageSize ? ' selected' : '';
                return '<option value="' + n + '"' + sel + '>' + n + '</option>';
            }).join('') +
            '</select></label>' +
            '<nav class="admin-table-pagination" data-admin-table-pagination></nav>' +
            '</div>';
        this.card.appendChild(footer);

        this.footerInfo = footer.querySelector('[data-admin-table-info]');
        this.paginationEl = footer.querySelector('[data-admin-table-pagination]');
        this.pageSizeSelect = footer.querySelector('[data-admin-table-page-size]');

        this.pageSizeSelect.addEventListener('change', function () {
            self.pageSize = parseInt(this.value, 10) || DEFAULT_PAGE_SIZE;
            self.currentPage = 1;
            self.refresh();
        });
        this.paginationEl.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-admin-page]');
            if (!btn || btn.disabled) return;
            var page = parseInt(btn.dataset.adminPage, 10);
            if (!isNaN(page)) {
                self.currentPage = page;
                self.refresh();
            }
        });
    };

    AdminTable.prototype.rowMatches = function (row) {
        var q = norm(this.searchInput && this.searchInput.value);
        var text = norm(row.dataset.search) || norm(row.textContent);
        if (q && text.indexOf(q) === -1) return false;

        for (var key in this.filters) {
            if (!Object.prototype.hasOwnProperty.call(this.filters, key)) continue;
            var val = this.filters[key];
            if (val === 'all' || val === undefined) continue;
            var rowVal = row.dataset[key];
            if (rowVal === undefined) {
                rowVal = row.getAttribute('data-' + key);
            }
            if (String(rowVal) !== String(val)) return false;
        }
        return true;
    };

    AdminTable.prototype.setRowVisible = function (row, show) {
        row.style.display = show ? '' : 'none';
        row.classList.toggle('admin-table-row-hidden', !show);
        var id = row.dataset.ticketId || row.dataset.rowId;
        if (id && this.detailByMain.has(String(id))) {
            var detail = this.detailByMain.get(String(id));
            detail.style.display = show ? '' : 'none';
            if (!show && detail.classList.contains('show')) {
                try {
                    if (typeof bootstrap !== 'undefined' && bootstrap.Collapse) {
                        bootstrap.Collapse.getOrCreateInstance(detail).hide();
                    }
                } catch (e) { /* ignore */ }
            }
        }
    };

    AdminTable.prototype.refresh = function () {
        var matched = [];
        this.dataRows.forEach(function (row) {
            if (this.rowMatches(row)) matched.push(row);
            else this.setRowVisible(row, false);
        }, this);

        var total = matched.length;
        var totalPages = Math.max(1, Math.ceil(total / this.pageSize));
        if (this.currentPage > totalPages) this.currentPage = totalPages;
        if (this.currentPage < 1) this.currentPage = 1;

        var start = (this.currentPage - 1) * this.pageSize;
        var end = start + this.pageSize;
        var pageRows = matched.slice(start, end);
        var pageIds = new Set(pageRows);

        this.dataRows.forEach(function (row) {
            var match = matched.indexOf(row) !== -1;
            var onPage = pageIds.has(row);
            this.setRowVisible(row, match && onPage);
        }, this);

        if (this.noResultsEl) {
            this.noResultsEl.classList.toggle('d-none', total > 0);
        }
        if (this.emptyPanel) {
            var showEmpty = total === 0 && this.dataRows.length > 0;
            this.emptyPanel.classList.toggle('d-none', !showEmpty);
            if (this.scroll) {
                this.scroll.classList.toggle('is-empty', showEmpty);
            }
            if (this.table) {
                this.table.classList.toggle('d-none', showEmpty);
            }
        }
        if (this.countBadge) {
            var label = this.root.dataset.countLabel || 'records';
            this.countBadge.textContent = total + ' ' + label;
        }
        this.renderPagination(total, totalPages, start, end);
        this.renderInfo(total, totalPages, start, end);
    };

    AdminTable.prototype.renderInfo = function (total, totalPages, start, end) {
        if (!this.footerInfo) return;
        if (total === 0) {
            this.footerInfo.innerHTML =
                '<span class="admin-table-footer-empty">' +
                '<i class="bi bi-inbox"></i> No matches found</span>';
            return;
        }
        var from = start + 1;
        var to = Math.min(end, total);
        this.footerInfo.textContent =
            'Showing ' + from + '–' + to + ' of ' + total +
            (totalPages > 1 ? ' · Page ' + this.currentPage + ' of ' + totalPages : '');
    };

    AdminTable.prototype.renderPagination = function (total, totalPages) {
        if (!this.paginationEl) return;
        if (total === 0 || totalPages <= 1) {
            this.paginationEl.innerHTML = '';
            return;
        }

        var pages = [];
        var cur = this.currentPage;
        pages.push(1);
        if (cur > 3) pages.push('…');
        for (var p = Math.max(2, cur - 1); p <= Math.min(totalPages - 1, cur + 1); p++) {
            if (pages.indexOf(p) === -1) pages.push(p);
        }
        if (cur < totalPages - 2) pages.push('…');
        if (totalPages > 1 && pages.indexOf(totalPages) === -1) pages.push(totalPages);

        var html = '<button type="button" class="admin-table-page-btn" data-admin-page="' + (cur - 1) + '" ' +
            (cur <= 1 ? 'disabled' : '') + '><i class="bi bi-chevron-left"></i></button>';

        pages.forEach(function (p) {
            if (p === '…') {
                html += '<span class="admin-table-page-ellipsis">…</span>';
            } else {
                html += '<button type="button" class="admin-table-page-btn' + (p === cur ? ' active' : '') +
                    '" data-admin-page="' + p + '">' + p + '</button>';
            }
        });

        html += '<button type="button" class="admin-table-page-btn" data-admin-page="' + (cur + 1) + '" ' +
            (cur >= totalPages ? 'disabled' : '') + '><i class="bi bi-chevron-right"></i></button>';

        this.paginationEl.innerHTML = html;
    };

    function initAll() {
        document.querySelectorAll('[data-admin-table]').forEach(function (el) {
            if (el._adminTable) return;
            var hasRows = el.querySelector('table.admin-table tbody tr');
            var hasSearch = el.querySelector('[data-admin-table-search]');
            if (!hasRows && !hasSearch) return;
            el._adminTable = new AdminTable(el);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAll);
    } else {
        initAll();
    }

    window.AdminTable = { init: initAll, AdminTable: AdminTable };
})();
