// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
(function () {
    // Prevent re-injection
    if (window.__aihelper_picker_active) return;
    window.__aihelper_picker_active = true;

    let hoveredEl = null;

    // Create info tooltip
    const tooltip = document.createElement('div');
    tooltip.id = '__aihelper_picker_tooltip';
    Object.assign(tooltip.style, {
        position: 'fixed',
        bottom: '0',
        left: '0',
        right: '0',
        zIndex: '2147483647',
        background: 'rgba(10, 10, 30, 0.92)',
        color: '#e0e0e0',
        fontFamily: 'Consolas, "Courier New", monospace',
        fontSize: '13px',
        padding: '10px 16px',
        borderTop: '2px solid #7c3aed',
        pointerEvents: 'none',
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: 'none'
    });
    document.body.appendChild(tooltip);

    // Create overlay highlight
    const overlay = document.createElement('div');
    overlay.id = '__aihelper_picker_overlay';
    Object.assign(overlay.style, {
        position: 'fixed',
        zIndex: '2147483646',
        pointerEvents: 'none',
        border: '2px solid #7c3aed',
        backgroundColor: 'rgba(124, 58, 237, 0.12)',
        borderRadius: '3px',
        transition: 'all 0.08s ease',
        display: 'none'
    });
    document.body.appendChild(overlay);

    function isPickerElement(el) {
        return el === tooltip || el === overlay || tooltip.contains(el) || overlay.contains(el);
    }

    function getElementInfo(el) {
        if (!el) return '';
        let tag = el.tagName.toLowerCase();
        let info = tag;
        if (el.id) info += '#' + el.id;
        if (el.className && typeof el.className === 'string') {
            const classes = el.className.trim().split(/\s+/).filter(c => c.length > 0).slice(0, 4);
            if (classes.length > 0) info += '.' + classes.join('.');
        }
        const rect = el.getBoundingClientRect();
        info += '  [' + Math.round(rect.width) + '\u00d7' + Math.round(rect.height) + ']';
        return info;
    }

    function generateSelector(el) {
        if (!el || el === document.body || el === document.documentElement) return '';

        // 1. By ID (if unique)
        if (el.id) {
            var byId = '#' + CSS.escape(el.id);
            try {
                if (document.querySelectorAll(byId).length === 1) return byId;
            } catch(e) {}
        }

        // 2. By unique attribute combinations
        var tag = el.tagName.toLowerCase();
        var attrChecks = ['aria-label', 'placeholder', 'name', 'type', 'role', 'data-testid', 'contenteditable'];
        for (var i = 0; i < attrChecks.length; i++) {
            var attr = attrChecks[i];
            var val = el.getAttribute(attr);
            if (val) {
                var sel = tag + '[' + attr + '="' + CSS.escape(val) + '"]';
                try {
                    if (document.querySelectorAll(sel).length === 1) return sel;
                } catch(e) {}
            }
        }

        // 3. By tag + class combination
        if (el.className && typeof el.className === 'string') {
            var classes = el.className.trim().split(/\s+/).filter(function(c) { return c.length > 0; });
            if (classes.length > 0) {
                var allClassSel = tag + '.' + classes.map(function(c) { return CSS.escape(c); }).join('.');
                try {
                    if (document.querySelectorAll(allClassSel).length === 1) return allClassSel;
                } catch(e) {}

                for (var j = 0; j < classes.length; j++) {
                    var clsSel = tag + '.' + CSS.escape(classes[j]);
                    try {
                        if (document.querySelectorAll(clsSel).length === 1) return clsSel;
                    } catch(e) {}
                }
            }
        }

        // 4. Build path from closest ancestor with ID
        var path = [];
        var current = el;
        while (current && current !== document.body && current !== document.documentElement) {
            var segment = current.tagName.toLowerCase();

            if (current.id) {
                segment = '#' + CSS.escape(current.id);
                path.unshift(segment);
                break;
            }

            var parent = current.parentElement;
            if (parent) {
                var siblings = Array.from(parent.children).filter(function(c) { return c.tagName === current.tagName; });
                if (siblings.length > 1) {
                    var index = siblings.indexOf(current) + 1;
                    segment += ':nth-of-type(' + index + ')';
                }
            }

            if (current.className && typeof current.className === 'string') {
                var curClasses = current.className.trim().split(/\s+/).filter(function(c) { return c.length > 0; });
                if (curClasses.length > 0) {
                    segment = current.tagName.toLowerCase() + '.' + curClasses.slice(0, 2).map(function(c) { return CSS.escape(c); }).join('.');
                }
            }

            path.unshift(segment);
            current = current.parentElement;

            if (path.length > 6) break;
        }

        return path.join(' > ');
    }

    function updateOverlay(el) {
        if (!el) {
            overlay.style.display = 'none';
            return;
        }
        var rect = el.getBoundingClientRect();
        Object.assign(overlay.style, {
            display: 'block',
            top: rect.top + 'px',
            left: rect.left + 'px',
            width: rect.width + 'px',
            height: rect.height + 'px'
        });
    }

    function onMouseOver(e) {
        var el = e.target;
        if (isPickerElement(el)) return;
        if (hoveredEl === el) return;
        hoveredEl = el;

        updateOverlay(el);

        var info = getElementInfo(el);
        var selector = generateSelector(el);
        tooltip.textContent = info + '  \u2192  ' + selector;
        tooltip.style.display = 'block';
    }

    function onMouseOut(e) {
        if (isPickerElement(e.relatedTarget)) return;
    }

    function onClick(e) {
        e.preventDefault();
        e.stopPropagation();
        e.stopImmediatePropagation();

        var el = e.target;
        if (isPickerElement(el)) return;

        var selector = generateSelector(el);
        cleanup();

        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'element_picked',
                selector: selector,
                tag: el.tagName.toLowerCase(),
                id: el.id || '',
                className: (typeof el.className === 'string') ? el.className : ''
            }));
        }
    }

    function onKeyDown(e) {
        if (e.key === 'Escape') {
            e.preventDefault();
            cleanup();
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'element_pick_cancelled'
                }));
            }
        }
    }

    function cleanup() {
        document.removeEventListener('mouseover', onMouseOver, true);
        document.removeEventListener('mouseout', onMouseOut, true);
        document.removeEventListener('click', onClick, true);
        document.removeEventListener('keydown', onKeyDown, true);
        if (tooltip.parentNode) tooltip.parentNode.removeChild(tooltip);
        if (overlay.parentNode) overlay.parentNode.removeChild(overlay);
        window.__aihelper_picker_active = false;
    }

    document.addEventListener('mouseover', onMouseOver, true);
    document.addEventListener('mouseout', onMouseOut, true);
    document.addEventListener('click', onClick, true);
    document.addEventListener('keydown', onKeyDown, true);
})();
