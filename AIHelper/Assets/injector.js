// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
(function () {
    function isFileUploadElement(el) {
        if (!el) return false;
        if (el.tagName === 'INPUT' && el.type === 'file') return true;
        if (el.querySelector && el.querySelector('input[type="file"]')) return true;

        const aria = (el.getAttribute('aria-label') || '').toLowerCase();
        const cls = (el.className || '').toString().toLowerCase();
        const id = (el.id || '').toLowerCase();

        if (aria.includes('attach') || aria.includes('upload') || aria.includes('file') || aria.includes('附件') ||
            cls.includes('attach') || cls.includes('upload') || cls.includes('file') ||
            id.includes('attach') || id.includes('upload') || id.includes('file')) {
            return true;
        }
        return false;
    }

    function setCursorToEnd(el) {
        if (!el) return;
        try {
            el.focus();
            const tag = (el.tagName || '').toLowerCase();
            if (tag === 'textarea' || tag === 'input') {
                const len = (el.value || '').length;
                el.setSelectionRange(len, len);
                el.scrollTop = el.scrollHeight;
            } else {
                // ContentEditable
                const selection = window.getSelection();
                if (selection) {
                    const range = document.createRange();
                    let lastNode = el;
                    while (lastNode.lastChild) {
                        lastNode = lastNode.lastChild;
                    }
                    if (lastNode && lastNode.nodeType === 3) { // TEXT_NODE
                        range.setStart(lastNode, lastNode.textContent.length);
                        range.setEnd(lastNode, lastNode.textContent.length);
                    } else {
                        range.selectNodeContents(el);
                        range.collapse(false);
                    }
                    selection.removeAllRanges();
                    selection.addRange(range);
                }
                el.scrollTop = el.scrollHeight;
            }
        } catch (e) {
            console.error("setCursorToEnd error:", e);
        }
    }

    function doInjectText(inputEl, text) {
        if (!inputEl) return;
        inputEl.focus();
        const isTextarea = inputEl.tagName.toLowerCase() === 'textarea';

        if (isTextarea) {
            // Reset React value tracker if present so React detects value change
            if (inputEl._valueTracker) {
                inputEl._valueTracker.setValue('');
            }

            const nativeInputValueSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, "value")?.set;
            if (nativeInputValueSetter) {
                nativeInputValueSetter.call(inputEl, text);
            } else {
                inputEl.value = text;
            }

            // Dispatch standard input & change events
            inputEl.dispatchEvent(new Event('input', { bubbles: true, cancelable: true }));
            inputEl.dispatchEvent(new Event('change', { bubbles: true, cancelable: true }));

            // Dispatch InputEvent for React 17/18 compatibility
            try {
                inputEl.dispatchEvent(new InputEvent('input', {
                    bubbles: true,
                    cancelable: true,
                    inputType: 'insertText',
                    data: text
                }));
            } catch (e) {}

            setCursorToEnd(inputEl);
        } else {
            // ContentEditable
            document.execCommand('selectAll', false, null);
            document.execCommand('insertText', false, text);
            if (!inputEl.textContent) {
                inputEl.textContent = text;
            }
            inputEl.dispatchEvent(new Event('input', { bubbles: true, cancelable: true }));
            setCursorToEnd(inputEl);
        }
    }

    // Reads back what is currently inside the input element
    function readText(el) {
        if (!el) return '';
        const tag = (el.tagName || '').toLowerCase();
        if (tag === 'textarea' || tag === 'input') return el.value || '';
        return el.textContent || '';
    }

    // Rough count of attachment chips/thumbnails currently shown by the composer.
    // Used only as a delta: a growing count after Ctrl+V means the paste landed even
    // when the page consumed the paste event before the watcher could see it.
    function countAttachments() {
        try {
            const nodes = document.querySelectorAll(
                'img[src^="blob:"], img[src^="data:image"], ' +
                '[data-testid*="file" i], [data-testid*="attach" i], ' +
                '[class*="attachment" i], [class*="thumbnail" i], ' +
                '[aria-label*="remove" i], [aria-label*="删除" i]');
            return nodes ? nodes.length : 0;
        } catch (e) {
            return 0;
        }
    }

    // Checks whether an attachment is actively uploading or processing in the composer
    function isAttachmentUploading(container) {
        const root = container || document;
        try {
            // 1. Check for active progress bars, spinners, or loading indicators
            const progressEls = root.querySelectorAll(
                '[role="progressbar"], [aria-busy="true"], ' +
                '[data-status="uploading"], [data-state="uploading"], [data-uploading="true"], ' +
                '[class*="uploading" i], [class*="upload-progress" i], ' +
                '[class*="loading-spinner" i], [class*="loading" i] svg, ' +
                'svg[class*="spin" i], svg[class*="loading" i], svg[class*="animate-spin" i], ' +
                '.ds-loading, .ds-icon-loading, mat-progress-spinner, ' +
                'circle[stroke-dashoffset]'
            );
            for (let i = 0; i < progressEls.length; i++) {
                const el = progressEls[i];
                if (isUsable(el) && el.offsetWidth > 0 && el.offsetHeight > 0) {
                    return true;
                }
            }

            // 2. Check for pending or uploading attachment items
            const pendingCards = root.querySelectorAll(
                '[class*="attachment" i][class*="pending" i], ' +
                '[class*="file" i][class*="pending" i], ' +
                '[class*="thumbnail" i][class*="loading" i]'
            );
            for (let i = 0; i < pendingCards.length; i++) {
                if (isUsable(pendingCards[i])) return true;
            }

            return false;
        } catch (e) {
            return false;
        }
    }

    function detectPlatform() {
        const url = window.location.href;
        if (url.includes("claude.ai")) return "claude";
        if (url.includes("gemini.google.com")) return "gemini";
        if (url.includes("deepseek.com")) return "deepseek";
        return "generic";
    }

    // Returns a failure reason string when the page is a login page, otherwise null
    function checkLogin(platform) {
        const url = window.location.href;
        if (platform === "claude") {
            if (url.includes("/login") || document.querySelector('a[href*="/login"]')) return "NOT_LOGGED_IN";
        } else if (platform === "gemini") {
            if (url.includes("accounts.google.com")) return "NOT_LOGGED_IN";
        } else if (platform === "deepseek") {
            if (url.includes("/sign_in") || url.includes("/login")) return "NOT_LOGGED_IN";
        }
        return null;
    }

    function findInput(platform, inputSelector) {
        let el = null;
        if (inputSelector) {
            try {
                el = document.querySelector(inputSelector);
            } catch(e) {}
        }
        if (!el) {
            if (platform === "claude") {
                el = document.querySelector('div.ProseMirror[contenteditable="true"]') || document.querySelector('[contenteditable="true"]');
            } else if (platform === "gemini") {
                el = document.querySelector('rich-textarea div[contenteditable="true"]') || document.querySelector('div[contenteditable="true"]');
            } else if (platform === "deepseek") {
                el = document.querySelector('textarea#chat-input') || document.querySelector('textarea');
            } else {
                el = document.querySelector('textarea') || document.querySelector('[contenteditable="true"]');
            }
        }
        return el;
    }

    function findNewChatButton(platform, newChatSelector) {
        let btn = null;
        if (newChatSelector) {
            try {
                btn = document.querySelector(newChatSelector);
            } catch(e) {}
        }
        if (btn) return btn;

        if (platform === "deepseek") {
            btn = document.querySelector('div[class*="new-chat"]') ||
                  document.querySelector('div[class*="sidebar"] div[class*="button"]') ||
                  Array.from(document.querySelectorAll('div, button, a')).find(el => el.textContent && (el.textContent.trim() === '开启新对话' || el.textContent.trim() === '新对话' || el.textContent.trim() === '新建对话'));
        } else if (platform === "claude") {
            btn = document.querySelector('button[aria-label*="New chat"]') ||
                  document.querySelector('button[aria-label*="新对话"]') ||
                  document.querySelector('a[href="/new"]');
        } else if (platform === "gemini") {
            btn = document.querySelector('button[aria-label*="New chat"]') ||
                  document.querySelector('button[aria-label*="新对话"]') ||
                  document.querySelector('a[aria-label*="New chat"]') ||
                  document.querySelector('a[aria-label*="新对话"]');
        }
        return btn || null;
    }

    function findSubmitButton(platform, inputEl, submitSelector) {
        let container = inputEl ? inputEl.closest('form') : null;
        if (!container && inputEl) {
            let parent = inputEl.parentElement;
            for (let i = 0; i < 6; i++) {
                if (!parent || parent === document.body) break;
                if (parent.querySelector('button, [role="button"], div[class*="button"]')) {
                    container = parent;
                    break;
                }
                parent = parent.parentElement;
            }
        }
        if (!container) container = (inputEl && inputEl.parentElement) || document.body;

        let submitBtn = null;

        if (submitSelector) {
            try {
                submitBtn = document.querySelector(submitSelector);
            } catch(e) {}
        }

        if (!submitBtn || submitBtn.offsetWidth === 0) {
            if (platform === "deepseek") {
                submitBtn = document.querySelector('#chat-input-send-button') ||
                            container.querySelector('#chat-input-send-button') ||
                            container.querySelector('div[class*="send-button"]') ||
                            container.querySelector('div[class*="sendButton"]') ||
                            container.querySelector('div[class*="_send_button"]');
            } else if (platform === "claude") {
                submitBtn = container.querySelector('button[aria-label*="Send" i]') ||
                            container.querySelector('button[aria-label*="发送"]') ||
                            container.querySelector('button[data-testid*="send" i]');
            } else if (platform === "gemini") {
                submitBtn = container.querySelector('button[aria-label*="Send" i]') ||
                            container.querySelector('button[aria-label*="发送"]') ||
                            container.querySelector('.send-button');
            }
        }

        if (!submitBtn || submitBtn.offsetWidth === 0 || isFileUploadElement(submitBtn)) {
            submitBtn = container.querySelector('button[data-testid*="send" i]') ||
                        container.querySelector('button[type="submit"]') ||
                        container.querySelector('button[aria-label*="Send" i]') ||
                        container.querySelector('button[aria-label*="发送"]') ||
                        container.querySelector('div[role="button"][aria-label*="Send" i]') ||
                        container.querySelector('div[role="button"][aria-label*="发送"]');
        }

        if (!submitBtn || submitBtn.offsetWidth === 0 || isFileUploadElement(submitBtn)) {
            const candidates = Array.from(container.querySelectorAll('button, div[role="button"], div, a'))
                .filter(el => {
                    if (inputEl && el.contains(inputEl)) return false;
                    if (isFileUploadElement(el)) return false;
                    if (!el.querySelector('svg')) return false;

                    const rect = el.getBoundingClientRect();
                    if (rect.width === 0 || rect.height === 0) return false;
                    if (rect.width > 120 || rect.height > 120) return false;

                    const aria = (el.getAttribute('aria-label') || '').toLowerCase();
                    const cls = (el.className || '').toString().toLowerCase();
                    if (aria.includes('menu') || aria.includes('sidebar') || aria.includes('history') ||
                        cls.includes('menu') || cls.includes('sidebar')) {
                        return false;
                    }
                    return true;
                });

            if (candidates.length > 0) {
                candidates.sort((a, b) => b.getBoundingClientRect().right - a.getBoundingClientRect().right);
                submitBtn = candidates[0];
            }
        }

        if (submitBtn) {
            const actualBtn = submitBtn.closest('button, [role="button"], div[class*="button"]') || submitBtn;
            submitBtn = actualBtn;
        }

        return submitBtn || null;
    }

    function isButtonReady(btn) {
        if (!btn || !isUsable(btn) || isFileUploadElement(btn)) return false;
        if (btn.disabled) return false;
        if (btn.hasAttribute && btn.hasAttribute('disabled')) return false;

        const ariaDisabled = (btn.getAttribute('aria-disabled') || '').toLowerCase();
        if (ariaDisabled === 'true') return false;
        const dataDisabled = (btn.getAttribute('data-disabled') || '').toLowerCase();
        if (dataDisabled === 'true') return false;
        const dataIsDisabled = (btn.getAttribute('data-is-disabled') || '').toLowerCase();
        if (dataIsDisabled === 'true') return false;

        const cls = (btn.className || '').toString().toLowerCase();
        if (cls.includes('disabled') || cls.includes('not-allowed')) return false;

        try {
            const style = window.getComputedStyle(btn);
            if (style) {
                if (style.pointerEvents === 'none' || style.visibility === 'hidden' || style.display === 'none') return false;
                if (style.cursor === 'not-allowed') return false;
                if (style.opacity !== '' && parseFloat(style.opacity) < 0.5) return false;
            }
        } catch (e) {}

        if (btn.querySelector && btn.querySelector('[class*="spin" i], [class*="loading" i], [role="progressbar"], circle[stroke-dashoffset]')) {
            return false;
        }

        if (btn.parentElement) {
            const parentCls = (btn.parentElement.className || '').toString().toLowerCase();
            if (parentCls.includes('disabled') || parentCls.includes('send-button-disabled')) return false;
            try {
                const parentStyle = window.getComputedStyle(btn.parentElement);
                if (parentStyle && (parentStyle.pointerEvents === 'none' || parentStyle.cursor === 'not-allowed')) return false;
            } catch (e) {}
        }

        return true;
    }

    // Deliberately style based rather than geometry based: the host window can be
    // hidden in the tray while this runs, and then every rect measures 0x0.
    function isUsable(el) {
        if (!el) return false;
        if (el.disabled) return false;
        try {
            const style = window.getComputedStyle(el);
            if (style && (style.display === 'none' || style.visibility === 'hidden')) return false;
        } catch (e) {}
        return true;
    }

    // The composer area around the input (form, or the closest ancestor holding buttons).
    // Kept tight on purpose: generated images must never fall inside it.
    function findComposer(inputEl) {
        if (!inputEl) return null;
        const form = inputEl.closest('form');
        if (form) return form;
        let parent = inputEl.parentElement;
        for (let i = 0; i < 6; i++) {
            if (!parent || parent === document.body) break;
            if (parent.querySelector('button, [role="button"]')) return parent;
            parent = parent.parentElement;
        }
        return inputEl.parentElement;
    }

    const STOP_LABEL = /(^|[^a-z])stop([^a-z]|$)|停止|中止/;

    // True while the platform is still answering: the send button turns into a
    // stop button on every supported platform. Attributes are checked page wide,
    // class names only inside the composer (class names are too noisy elsewhere).
    function isGeneratingNow(inputEl) {
        const composer = findComposer(inputEl);
        const nodes = document.querySelectorAll('button, [role="button"]');
        for (let i = 0; i < nodes.length; i++) {
            const el = nodes[i];
            if (!isUsable(el)) continue;
            const attrs = ((el.getAttribute('aria-label') || '') + ' ' +
                           (el.getAttribute('data-testid') || '') + ' ' +
                           (el.getAttribute('title') || '')).toLowerCase();
            if (STOP_LABEL.test(attrs)) return true;
            if (composer && composer.contains(el)) {
                const cls = (el.className || '').toString().toLowerCase();
                if (STOP_LABEL.test(cls)) return true;
            }
        }
        return false;
    }

    // Text of the newest reply, used to explain why no image came back
    function lastReplyText() {
        const selectors = [
            '[data-message-author-role="assistant"]', 'model-response', 'message-content',
            '[class*="markdown" i]', '[class*="message-content" i]', '[class*="answer" i]'
        ];
        for (let i = 0; i < selectors.length; i++) {
            let list;
            try { list = document.querySelectorAll(selectors[i]); } catch (e) { continue; }
            if (list && list.length) {
                const text = (list[list.length - 1].textContent || '').replace(/\s+/g, ' ').trim();
                if (text) return text.slice(-200);
            }
        }
        return '';
    }

    // ---- Page preset: recording and replaying the setup clicks (model, ratio, ...) ----

    // Classes that only describe a transient state; a selector built on them stops matching
    const STATE_CLASS = /^(is-|has-)?(active|selected|checked|current|open|opened|expanded|show|shown|visible|focus|focused|focus-visible|hover|hovered|pressed|disabled|on|off)$/i;
    const ON_CLASS = /^(is-|has-)?(active|selected|checked|current|on)$/i;
    const CLICKABLE = 'button, a, summary, label, [role="button"], [role="option"], [role="menuitem"], [role="menuitemradio"], ' +
                      '[role="menuitemcheckbox"], [role="tab"], [role="radio"], [role="checkbox"], [role="switch"], [role="combobox"], [role="treeitem"]';
    const RECORDER_BADGE_ID = '__aihelper_recorder_badge';

    function normalizeText(s) {
        return (s || '').replace(/\s+/g, ' ').trim();
    }

    // Shown, as opposed to merely present: closed menus often stay in the DOM hidden
    function isShown(el) {
        return !!el && isUsable(el) && el.getClientRects().length > 0;
    }

    // Text pieces joined with spaces: "GPT Image 2.5<span>生图</span>" reads "GPT Image 2.5 生图"
    function spacedText(el) {
        const parts = [];
        try {
            const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
            let node;
            while ((node = walker.nextNode()) && parts.length < 40) {
                const t = normalizeText(node.nodeValue);
                if (t) parts.push(t);
            }
        } catch (e) {
            return normalizeText(el.textContent);
        }
        return parts.join(' ');
    }

    function elementText(el) {
        if (!el || !el.getAttribute) return '';
        let t = normalizeText(el.getAttribute('aria-label'));
        if (!t) t = spacedText(el);
        if (!t) t = normalizeText(el.getAttribute('title') || el.getAttribute('alt') || el.getAttribute('placeholder'));
        if (!t && el.querySelector) {
            const img = el.querySelector('img[alt]');
            if (img) t = normalizeText(img.getAttribute('alt'));
        }
        return t.slice(0, 80);
    }

    // Label of a form control (its own text would be all of a select's options)
    function controlLabel(el) {
        if (!el || !el.getAttribute) return '';
        let t = '';
        try {
            if (el.labels && el.labels.length) t = normalizeText(el.labels[0].textContent);
        } catch (e) {}
        if (!t) {
            t = normalizeText(el.getAttribute('aria-label') || el.getAttribute('title') ||
                              el.getAttribute('placeholder') || el.getAttribute('name') || el.id);
        }
        return t.slice(0, 80);
    }

    function stepText(el, kind) {
        return kind === 'click' ? elementText(el) : controlLabel(el);
    }

    function hasPointerCursor(el) {
        try {
            const c = window.getComputedStyle(el).cursor;
            return c === 'pointer' || c === 'zoom-in';
        } catch (e) {
            return false;
        }
    }

    // The element a click actually activates: the nearest interactive ancestor, else the
    // outermost ancestor that still shows a pointer cursor (div based buttons and options)
    function clickRoot(el) {
        if (el && el.nodeType !== 1) el = el.parentElement;
        if (!el || !el.closest) return null;
        const semantic = el.closest(CLICKABLE);
        if (semantic) return semantic;

        let best = null;
        for (let p = el; p && p !== document.body && p !== document.documentElement; p = p.parentElement) {
            if (hasPointerCursor(p)) best = p;
            else if (best) break;
        }
        return best || el.closest('li, [onclick], [tabindex]') || el;
    }

    // "on" / "off" / "" (no signal). Recorded after a click, so replay can leave an element
    // alone when it is already in that state instead of toggling it back.
    function elementState(el) {
        if (!el || !el.isConnected || !el.getAttribute) return '';
        let off = false;
        const attrs = ['aria-pressed', 'aria-checked', 'aria-selected', 'aria-expanded'];
        for (let i = 0; i < attrs.length; i++) {
            const v = (el.getAttribute(attrs[i]) || '').toLowerCase();
            if (v === 'true' || v === 'mixed') return 'on';
            if (v === 'false') off = true;
        }
        const ds = (el.getAttribute('data-state') || '').toLowerCase();
        if (/^(on|checked|active|open|selected)$/.test(ds)) return 'on';
        if (/^(off|unchecked|inactive|closed)$/.test(ds)) off = true;
        const classes = el.classList ? Array.from(el.classList) : [];
        if (classes.some(c => ON_CLASS.test(c))) return 'on';
        return off ? 'off' : '';
    }

    function stableClasses(el) {
        const classes = el.classList ? Array.from(el.classList) : [];
        return classes.filter(c => c && !STATE_CLASS.test(c) && !/[:\[\]\/@!%]/.test(c));
    }

    // Generated ids (React useId, Radix, Headless UI, hashes) change on every render
    function isStableId(id) {
        return !!id && !/[:\s]/.test(id) &&
               !/^(radix|headlessui|mui|rc[-_]|react|ember|_r_|r[-_]\d)/i.test(id) &&
               !/\d{3,}/.test(id) && !/[0-9a-f]{8,}/i.test(id);
    }

    function countMatches(sel) {
        try {
            return document.querySelectorAll(sel).length;
        } catch (e) {
            return 0;
        }
    }

    function isUniqueSelector(sel) {
        return countMatches(sel) === 1;
    }

    function cssAttrValue(v) {
        return String(v).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
    }

    // A selector that survives re-renders. It may still match several elements (a group of
    // ratio buttons, the options of a menu): the recorded text picks the right one on replay.
    function buildSelector(el) {
        const tag = el.tagName.toLowerCase();
        if (isStableId(el.id)) {
            const byId = '#' + CSS.escape(el.id);
            if (isUniqueSelector(byId)) return byId;
        }

        const attrs = ['data-testid', 'data-test', 'data-test-id', 'data-qa', 'name', 'aria-label', 'title', 'data-value', 'value'];
        for (let i = 0; i < attrs.length; i++) {
            const v = el.getAttribute(attrs[i]);
            if (!v || v.length > 80) continue;
            const sel = tag + '[' + attrs[i] + '="' + cssAttrValue(v) + '"]';
            if (isUniqueSelector(sel)) return sel;
        }

        let own = tag + stableClasses(el).slice(0, 3).map(c => '.' + CSS.escape(c)).join('');
        if (isUniqueSelector(own)) return own;

        // Without text nothing else can tell siblings apart, so fall back to position
        if (!elementText(el) && el.parentElement) {
            const siblings = Array.from(el.parentElement.children).filter(c => c.tagName === el.tagName);
            if (siblings.length > 1) own += ':nth-of-type(' + (siblings.indexOf(el) + 1) + ')';
        }

        // Ancestors are only prepended while they narrow the match: every extra class in the
        // chain is one more thing a site update can break
        let sel = own;
        let count = countMatches(sel);
        let cur = el.parentElement;
        for (let depth = 0; count > 1 && cur && cur !== document.body && cur !== document.documentElement && depth < 6; depth++, cur = cur.parentElement) {
            let segment = null;
            if (isStableId(cur.id)) {
                segment = '#' + CSS.escape(cur.id);
            } else {
                const classes = stableClasses(cur).slice(0, 2);
                if (classes.length) segment = cur.tagName.toLowerCase() + classes.map(c => '.' + CSS.escape(c)).join('');
            }
            if (!segment) continue;

            const candidate = segment + ' ' + sel;
            const candidateCount = countMatches(candidate);
            if (candidateCount > 0 && candidateCount < count) {
                sel = candidate;
                count = candidateCount;
            }
        }
        return sel;
    }

    // Opens a file chooser; a recorded click on it would be useless on replay
    function isFileTrigger(el) {
        if (!el) return false;
        if (el.tagName === 'INPUT' && el.type === 'file') return true;
        if (el.querySelector && el.querySelector('input[type="file"]')) return true;
        const label = ((el.getAttribute('aria-label') || '') + ' ' + (el.getAttribute('title') || '')).toLowerCase();
        return /upload|attach|上传|附件/.test(label);
    }

    // Typing the prompt, sending it and starting a new chat are the batch run's own steps
    function isIgnoredForPreset(el, ctx) {
        if (!el || el === document.body || el === document.documentElement) return true;
        if (el.closest && el.closest('#' + RECORDER_BADGE_ID)) return true;
        if (isFileTrigger(el)) return true;

        const inputEl = findInput(ctx.platform, ctx.inputSelector);
        if (inputEl && (inputEl === el || inputEl.contains(el) || el.contains(inputEl))) return true;
        const submitBtn = inputEl ? findSubmitButton(ctx.platform, inputEl, ctx.submitSelector) : null;
        if (submitBtn && (submitBtn === el || submitBtn.contains(el))) return true;
        const newChatBtn = findNewChatButton(ctx.platform, ctx.newChatSelector);
        if (newChatBtn && (newChatBtn === el || newChatBtn.contains(el))) return true;
        return false;
    }

    function findStepElement(step) {
        const kind = step.kind || 'click';
        const want = normalizeText(step.text);
        // Form controls are often visually hidden behind a styled label, so only clicks need to be visible
        const usable = kind === 'click' ? isShown : (el => el.isConnected && !el.disabled);

        let candidates = [];
        if (step.selector) {
            try { candidates = Array.from(document.querySelectorAll(step.selector)).filter(usable); } catch (e) {}
        }
        if (want) {
            const exact = candidates.filter(c => stepText(c, kind) === want);
            if (exact.length) return exact[0];
        }
        // A unique match whose text changed, e.g. a model picker that shows the current model
        if (candidates.length === 1) return candidates[0];
        if (!want) return candidates[0] || null;
        if (kind !== 'click') return null;

        // The selector broke (class names changed): look the element up by its text
        let pool = [];
        try { pool = document.querySelectorAll(step.tag || '*'); } catch (e) { return null; }
        for (let i = 0; i < pool.length; i++) {
            const el = pool[i];
            if (elementText(el) === want && isShown(el) && clickRoot(el) === el) return el;
        }
        return null;
    }

    // Full pointer sequence: several component libraries open menus on pointerdown, not click
    function fireClick(el) {
        if (!el) return;
        try { el.scrollIntoView({ block: 'nearest', inline: 'nearest' }); } catch (e) {}
        let x = 0, y = 0;
        try {
            const r = el.getBoundingClientRect();
            x = r.left + r.width / 2;
            y = r.top + r.height / 2;
        } catch (e) {}
        const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y, button: 0 };
        const pointer = { pointerId: 1, pointerType: 'mouse', isPrimary: true };
        try {
            el.dispatchEvent(new PointerEvent('pointerdown', Object.assign({ buttons: 1 }, pointer, opts)));
            el.dispatchEvent(new MouseEvent('mousedown', Object.assign({ buttons: 1 }, opts)));
            el.dispatchEvent(new PointerEvent('pointerup', Object.assign({}, pointer, opts)));
            el.dispatchEvent(new MouseEvent('mouseup', opts));
            el.dispatchEvent(new MouseEvent('click', opts));
        } catch (e) {
            try { el.click(); } catch (e2) {}
        }
    }

    function setControlValue(el, value) {
        const proto = el.tagName === 'SELECT' ? window.HTMLSelectElement.prototype
            : (el.tagName === 'TEXTAREA' ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype);
        if (el._valueTracker) {
            try { el._valueTracker.setValue(''); } catch (e) {}
        }
        const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
        if (setter) setter.call(el, value); else el.value = value;
        el.dispatchEvent(new Event('input', { bubbles: true, cancelable: true }));
        el.dispatchEvent(new Event('change', { bubbles: true, cancelable: true }));
    }

    // Returns true when something was done, false when the element already matched the step
    function performStep(el, step) {
        const kind = step.kind || 'click';
        if (kind === 'select' || kind === 'input') {
            const value = step.value || '';
            if (String(el.value) === value) return false;
            setControlValue(el, value);
            return true;
        }
        if (kind === 'check') {
            const want = step.value === 'true';
            if (!!el.checked === want) return false;
            fireClick(el);
            if (!!el.checked !== want) {
                const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'checked')?.set;
                if (setter) setter.call(el, want); else el.checked = want;
                el.dispatchEvent(new Event('input', { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
            }
            return true;
        }
        if (step.state && elementState(el) === step.state) return false;
        fireClick(el);
        return true;
    }

    // ---- Full-size image viewer ----

    function looksLikeImageUrl(url) {
        return /^(blob:|data:image\/)/i.test(url) || /\.(png|jpe?g|webp|gif|avif|bmp)(\?|#|$)/i.test(url);
    }

    function fixedAncestor(el) {
        for (let p = el; p && p !== document.body && p !== document.documentElement; p = p.parentElement) {
            try {
                if (window.getComputedStyle(p).position === 'fixed') return p;
            } catch (e) {}
        }
        return null;
    }

    // For viewers that draw without an <img> (canvas, background-image): the large fixed
    // element that appeared after the click
    function addedOverlay(nodes) {
        const minArea = window.innerWidth * window.innerHeight * 0.4;
        for (let i = 0; i < nodes.length; i++) {
            const n = nodes[i];
            if (!n.isConnected || !isShown(n)) continue;
            try {
                const r = n.getBoundingClientRect();
                if (window.getComputedStyle(n).position === 'fixed' && r.width * r.height >= minArea) return n;
            } catch (e) {}
        }
        return null;
    }

    const CLOSE_LABEL = /close|关闭|dismiss|退出/i;
    const CLOSE_TEXT = /^(×|✕|✖|╳|x|X|关闭|close|Close)$/;

    function findCloseButton(root) {
        if (!root || !root.querySelectorAll) return null;
        const groups = ['button, [role="button"], a', 'span, div, i'];
        for (let g = 0; g < groups.length; g++) {
            const nodes = root.querySelectorAll(groups[g]);
            for (let i = 0; i < nodes.length; i++) {
                const el = nodes[i];
                if (!isShown(el)) continue;
                const label = (el.getAttribute('aria-label') || '') + ' ' + (el.getAttribute('title') || '');
                const cls = (typeof el.className === 'string' ? el.className : '').toLowerCase();
                if (CLOSE_LABEL.test(label) || CLOSE_TEXT.test(normalizeText(el.textContent)) ||
                    /(^|[\s_-])close([\s_-]|$)/.test(cls)) {
                    return el;
                }
            }
        }
        return null;
    }

    const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

    // Poll interval used by waitReady, and how long the input element must stay
    // the same before the page is considered hydrated (SPA frameworks swap the
    // node while mounting, and text injected before that gets discarded).
    const READY_POLL_MS = 200;
    const READY_STABLE_MS = 400;

    window.AiHelperInjector = {
        /// WebView2's ExecuteScriptAsync does not await promises — an async function
        /// comes back as an empty object. So async work is started as a job here and
        /// its result is parked on window.__aiHelperJob for the host to poll.
        run: function(id, method, args) {
            try {
                const fn = window.AiHelperInjector[method];
                if (typeof fn !== 'function') return { started: false, reason: "NO_METHOD" };

                // Supersede whatever was running: an abandoned job would keep polling the DOM
                const myRun = (window.__aiHelperRunId = (window.__aiHelperRunId || 0) + 1);
                const job = { id: id, run: myRun, done: false, result: null };
                window.__aiHelperJob = job;

                Promise.resolve()
                    .then(() => fn.apply(window.AiHelperInjector, args || []))
                    .then(r => { job.result = r; job.done = true; })
                    .catch(e => {
                        job.result = { ready: false, success: false, reason: "EXCEPTION", message: String((e && e.message) || e) };
                        job.done = true;
                    });

                return { started: true, reason: "STARTED" };
            } catch (err) {
                return { started: false, reason: "EXCEPTION", message: err.message };
            }
        },

        /// Waits until the document is parsed and a usable input element has been
        /// present and unchanged for READY_STABLE_MS. Called before inject() so the
        /// caller never writes into a page that is still booting.
        waitReady: async function(inputSelector, timeoutMs) {
            try {
                const deadline = Date.now() + (timeoutMs || 20000);
                const platform = detectPlatform();
                const myRun = window.__aiHelperRunId;
                let lastEl = null;
                let stableSince = 0;

                while (Date.now() < deadline) {
                    // A newer request took over — stop polling the DOM for the old one
                    if (window.__aiHelperRunId !== myRun) return { ready: false, reason: "SUPERSEDED" };

                    const loginReason = checkLogin(platform);
                    if (loginReason) return { ready: false, reason: loginReason };

                    if (document.readyState !== 'loading') {
                        const el = findInput(platform, inputSelector);
                        if (el && isUsable(el)) {
                            if (el === lastEl) {
                                if (Date.now() - stableSince >= READY_STABLE_MS) {
                                    return { ready: true, reason: "READY" };
                                }
                            } else {
                                lastEl = el;
                                stableSince = Date.now();
                            }
                        } else {
                            lastEl = null;
                        }
                    }
                    await sleep(READY_POLL_MS);
                }
                return { ready: false, reason: "TIMEOUT" };
            } catch (err) {
                return { ready: false, reason: "EXCEPTION", message: err.message };
            }
        },

        /// Clicks the new chat button. The click may reload the whole page, which
        /// destroys this script context, so the caller does the waiting: the token
        /// written on window is the marker it polls to detect that reload.
        startNewChat: function(newChatSelector, token) {
            try {
                window.__aiHelperToken = token;

                const platform = detectPlatform();
                const btn = findNewChatButton(platform, newChatSelector);
                if (!btn) return { clicked: false, reason: "NOT_FOUND" };

                btn.click();
                const svg = btn.querySelector('svg');
                if (svg) {
                    svg.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
                }
                return { clicked: true, reason: "CLICKED" };
            } catch (err) {
                return { clicked: false, reason: "EXCEPTION", message: err.message };
            }
        },

        /// Focuses the input element so a simulated Ctrl+V lands inside it.
        /// Without this the paste goes to whatever the page focused last (often
        /// document.body) and Chromium drops the clipboard payload.
        focusInput: function(inputSelector) {
            try {
                const platform = detectPlatform();
                const inputEl = findInput(platform, inputSelector);
                if (!inputEl) return { focused: false, reason: "INPUT_NOT_FOUND" };

                setCursorToEnd(inputEl);
                const active = document.activeElement;
                const ok = active === inputEl || (inputEl.contains && inputEl.contains(active));
                return { focused: !!ok, reason: ok ? "FOCUSED" : "FOCUS_REFUSED" };
            } catch (err) {
                return { focused: false, reason: "EXCEPTION", message: err.message };
            }
        },

        /// Arms a watcher before the host simulates Ctrl+V, and focuses the input on
        /// the way in. An image/file paste changes no text, so the paste event itself
        /// is the primary signal; attachment nodes appearing is the fallback for pages
        /// that swallow the event before it reaches the input.
        beginPasteWatch: function(inputSelector) {
            try {
                const platform = detectPlatform();
                const inputEl = findInput(platform, inputSelector);
                if (!inputEl) return { started: false, reason: "INPUT_NOT_FOUND" };

                if (window.__aiHelperPasteWatch && typeof window.__aiHelperPasteWatch.cleanup === 'function') {
                    try { window.__aiHelperPasteWatch.cleanup(); } catch (e) {}
                }

                setCursorToEnd(inputEl);

                const initialText = readText(inputEl);
                const initialAttachments = countAttachments();
                const token = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
                const watch = {
                    token: token,
                    pasted: false,
                    reason: "WAITING",
                    initialText: initialText,
                    cleanup: null
                };

                const markPasted = (reason) => {
                    if (watch.pasted) return;
                    watch.pasted = true;
                    watch.reason = reason || "PASTED";
                };

                const onPaste = () => markPasted("PASTE_EVENT");
                const onInput = (e) => {
                    if (e && e.inputType === 'insertFromPaste') {
                        markPasted("INPUT_EVENT");
                        return;
                    }
                    if (!watch.pasted && readText(inputEl) !== initialText) {
                        markPasted("TEXT_CHANGED");
                    }
                };
                const onChange = () => {
                    if (!watch.pasted && readText(inputEl) !== initialText) {
                        markPasted("CHANGE_EVENT");
                    }
                };

                // The paste event is captured on document as well: several platforms
                // handle it on an ancestor and stop it before the input sees it.
                document.addEventListener('paste', onPaste, true);
                inputEl.addEventListener('paste', onPaste, true);
                inputEl.addEventListener('input', onInput, true);
                inputEl.addEventListener('change', onChange, true);

                const attachmentTimer = setInterval(() => {
                    if (watch.pasted) return;
                    if (countAttachments() > initialAttachments) {
                        markPasted("ATTACHMENT_ADDED");
                    }
                }, 200);

                watch.cleanup = () => {
                    try { clearInterval(attachmentTimer); } catch (e) {}
                    try { document.removeEventListener('paste', onPaste, true); } catch (e) {}
                    try { inputEl.removeEventListener('paste', onPaste, true); } catch (e) {}
                    try { inputEl.removeEventListener('input', onInput, true); } catch (e) {}
                    try { inputEl.removeEventListener('change', onChange, true); } catch (e) {}
                };

                window.__aiHelperPasteWatch = watch;
                return { started: true, token: token, reason: "STARTED" };
            } catch (err) {
                return { started: false, reason: "EXCEPTION", message: err.message };
            }
        },

        waitForPaste: async function(token, timeoutMs) {
            const release = () => {
                const w = window.__aiHelperPasteWatch;
                if (w && w.token === token && typeof w.cleanup === 'function') {
                    try { w.cleanup(); } catch (e) {}
                }
            };

            try {
                const deadline = Date.now() + (timeoutMs || 5000);
                const myRun = window.__aiHelperRunId;

                while (Date.now() < deadline) {
                    if (window.__aiHelperRunId !== myRun) return { pasted: false, reason: "SUPERSEDED" };
                    const watch = window.__aiHelperPasteWatch;
                    if (!watch || watch.token !== token) return { pasted: false, reason: "LOST" };
                    if (watch.pasted) {
                        release();
                        return { pasted: true, reason: watch.reason || "PASTED" };
                    }
                    await sleep(120);
                }
                release();
                return { pasted: false, reason: "TIMEOUT" };
            } catch (err) {
                release();
                return { pasted: false, reason: "EXCEPTION", message: err.message };
            }
        },

        waitForSubmitReady: async function(inputSelector, submitSelector, timeoutMs) {
            try {
                const deadline = Date.now() + (timeoutMs || 300000);
                const platform = detectPlatform();
                const myRun = window.__aiHelperRunId;
                const REQUIRED_STABLE_MS = 600;
                let stableSince = 0;

                // Initial brief wait for upload states to mount if paste just finished
                await sleep(300);

                while (Date.now() < deadline) {
                    if (window.__aiHelperRunId !== myRun) return { ready: false, reason: "SUPERSEDED" };
                    const loginReason = checkLogin(platform);
                    if (loginReason) return { ready: false, reason: loginReason };

                    const inputEl = findInput(platform, inputSelector);
                    if (!inputEl) {
                        stableSince = 0;
                        await sleep(200);
                        continue;
                    }

                    const container = (inputEl && inputEl.closest('form')) ||
                                      (inputEl && inputEl.parentElement?.parentElement) ||
                                      document.body;

                    const uploading = isAttachmentUploading(container);
                    const submitBtn = findSubmitButton(platform, inputEl, submitSelector);
                    const btnReady = isButtonReady(submitBtn);

                    if (!uploading && btnReady) {
                        if (stableSince === 0) {
                            stableSince = Date.now();
                        } else if (Date.now() - stableSince >= REQUIRED_STABLE_MS) {
                            return { ready: true, reason: "READY" };
                        }
                    } else {
                        stableSince = 0;
                    }

                    await sleep(200);
                }

                return { ready: false, reason: "TIMEOUT" };
            } catch (err) {
                return { ready: false, reason: "EXCEPTION", message: err.message };
            }
        },

        /// Injects the prompt and optionally submits it. New chat handling happens
        /// before this call (see startNewChat), so the page is already settled here.
        inject: async function(text, autoSubmit, inputSelector, submitSelector) {
            try {
                const platform = detectPlatform();

                const loginReason = checkLogin(platform);
                if (loginReason) return { success: false, reason: loginReason };

                // 1. Find input element (with retries if the DOM is still settling)
                let inputEl = findInput(platform, inputSelector);
                if (!inputEl) {
                    for (let i = 0; i < 40; i++) {
                        await sleep(250);
                        inputEl = findInput(platform, inputSelector);
                        if (inputEl) break;
                    }
                }

                if (!inputEl) {
                    return { success: false, reason: "INPUT_NOT_FOUND" };
                }

                // 2. Inject text
                doInjectText(inputEl, text);

                // 3. Verify the text survived: a late re-render can wipe it, and the
                //    input node itself may have been replaced meanwhile.
                await sleep(300);
                let currentInput = findInput(platform, inputSelector) || inputEl;
                if (!readText(currentInput).trim()) {
                    doInjectText(currentInput, text);
                    await sleep(300);
                    currentInput = findInput(platform, inputSelector) || currentInput;
                    if (!readText(currentInput).trim()) {
                        return { success: false, reason: "INJECT_LOST" };
                    }
                }

                // 4. Auto-submit or focus to end
                if (autoSubmit) {
                    await sleep(200);
                    try {
                        await window.AiHelperInjector.submit(inputSelector, submitSelector);
                    } catch (e) {
                        console.error("Auto submit error:", e);
                    }
                } else {
                    // When not submitting (e.g. prompt injected from status bar), focus and move cursor to end
                    setCursorToEnd(currentInput);
                }

                return { success: true, reason: "SUCCESS" };
            } catch (err) {
                return { success: false, reason: "EXCEPTION", message: err.message };
            }
        },

        /// Marks every image already on the page so probeImages only reports the
        /// images produced by the prompt that is about to be sent.
        snapshotImages: function() {
            try {
                const imgs = document.querySelectorAll('img');
                const srcs = [];
                for (let i = 0; i < imgs.length; i++) {
                    imgs[i].__aiHelperBaseline = true;
                    const src = imgs[i].currentSrc || imgs[i].src;
                    if (src) srcs.push(src);
                }
                window.__aiHelperImageBaseline = srcs;
                return { ok: true, count: imgs.length };
            } catch (err) {
                return { ok: false, reason: "EXCEPTION", message: err.message };
            }
        },

        /// One-shot report of the generation state. Deliberately synchronous: the host
        /// does the waiting, because page timers get throttled while the window is hidden.
        probeImages: function(inputSelector, minSize) {
            try {
                const platform = detectPlatform();
                const loginReason = checkLogin(platform);
                if (loginReason) return { ok: false, reason: loginReason };

                const inputEl = findInput(platform, inputSelector);
                const composer = findComposer(inputEl);
                const baseline = new Set(window.__aiHelperImageBaseline || []);
                const min = minSize || 256;
                const images = [];
                const seen = new Set();
                let pending = 0;

                const imgs = document.querySelectorAll('img');
                for (let i = 0; i < imgs.length; i++) {
                    const img = imgs[i];
                    if (img.__aiHelperBaseline) continue;
                    const src = img.currentSrc || img.src || '';
                    if (!src || baseline.has(src) || seen.has(src)) continue;
                    if (/^data:image\/svg/i.test(src) || /\.svg(\?|#|$)/i.test(src)) continue;
                    if (composer && composer.contains(img)) continue;
                    if (!isUsable(img) || img.getClientRects().length === 0) continue;

                    let style = null;
                    try { style = window.getComputedStyle(img); } catch (e) {}
                    if (style && style.opacity !== '' && parseFloat(style.opacity) < 0.05) continue;

                    if (!img.complete) {
                        // Lazy images never load while the window is hidden or the image is
                        // off screen — force them, otherwise the wait would never end.
                        if (img.loading === 'lazy') { try { img.loading = 'eager'; } catch (e) {} }
                        try { img.scrollIntoView({ block: 'nearest' }); } catch (e) {}
                        pending++;
                        continue;
                    }
                    if (img.naturalWidth < min || img.naturalHeight < min) continue;

                    // A blurred image is a progressive preview that is still being refined
                    if (style && /blur\(/i.test(style.filter || '')) {
                        pending++;
                        continue;
                    }

                    // Avatars and icons ship large files but are drawn small. The drawn size
                    // is only judged when layout is available (hidden window measures 0x0).
                    const rect = img.getBoundingClientRect();
                    if (rect.width > 0 && rect.height > 0 && (rect.width < 64 || rect.height < 64)) continue;

                    seen.add(src);
                    images.push({ src: src, w: img.naturalWidth, h: img.naturalHeight });
                }

                return {
                    ok: true,
                    reason: "OK",
                    generating: isGeneratingNow(inputEl),
                    images: images,
                    pending: pending,
                    textLength: document.body ? (document.body.textContent || '').length : 0,
                    snippet: images.length === 0 ? lastReplyText() : ''
                };
            } catch (err) {
                return { ok: false, reason: "EXCEPTION", message: err.message };
            }
        },

        /// Reads an image as base64 from inside the page, so blob:/data: URLs and
        /// same-origin images (sent with the page's cookies) work. Cross-origin images
        /// without CORS fail here; the host falls back to its network capture.
        fetchImage: async function(src) {
            const toBase64 = blob => new Promise((resolve, reject) => {
                const reader = new FileReader();
                reader.onload = () => {
                    const s = String(reader.result || '');
                    const comma = s.indexOf(',');
                    resolve(comma >= 0 ? s.substring(comma + 1) : '');
                };
                reader.onerror = () => reject(reader.error || new Error('READ_FAILED'));
                reader.readAsDataURL(blob);
            });

            let lastReason = "FETCH_FAILED";
            const attempts = [{}, { credentials: 'include' }];
            for (let i = 0; i < attempts.length; i++) {
                try {
                    const resp = await fetch(src, attempts[i]);
                    if (!resp.ok) {
                        lastReason = "HTTP_" + resp.status;
                        continue;
                    }
                    const blob = await resp.blob();
                    if (!blob || blob.size === 0) {
                        lastReason = "EMPTY";
                        continue;
                    }
                    const data = await toBase64(blob);
                    return { ok: true, reason: "OK", mime: blob.type || '', data: data };
                } catch (err) {
                    lastReason = "FETCH_FAILED";
                }
            }
            return { ok: false, reason: lastReason };
        },

        /// Starts recording the user's own clicks and form changes as page preset steps.
        /// Steps are queued on window.__aiHelperRecorder and drained by the host; a page
        /// reload drops the recorder, which the host notices and starts it again.
        startRecording: function(inputSelector, submitSelector, newChatSelector, badgeText) {
            try {
                const old = window.__aiHelperRecorder;
                if (old && typeof old.cleanup === 'function') {
                    try { old.cleanup(); } catch (e) {}
                }

                const ctx = { platform: detectPlatform(), inputSelector, submitSelector, newChatSelector };
                const rec = { active: true, steps: [], cleanup: null };

                const onClick = (e) => {
                    if (!rec.active || !e.isTrusted) return;
                    const target = e.target;
                    if (!target || target.nodeType !== 1) return;

                    // Native controls are recorded by their change event instead
                    const tag = target.tagName.toLowerCase();
                    if (tag === 'input' || tag === 'select' || tag === 'option' || tag === 'textarea') return;
                    const label = target.closest('label');
                    if (label && label.control) return;

                    const el = clickRoot(target);
                    if (!el || el.isContentEditable || isIgnoredForPreset(el, ctx)) return;

                    // Built now, while the element (e.g. a menu option) is still in the DOM
                    const step = { kind: 'click', selector: buildSelector(el), tag: el.tagName.toLowerCase(), text: elementText(el), state: '', value: '' };
                    // The state the click leads to is read once the page has reacted. Frameworks
                    // that re-create the buttons on render leave el detached: read its replacement.
                    setTimeout(() => {
                        const live = el.isConnected ? el : findStepElement(step);
                        step.state = live ? elementState(live) : '';
                        if (rec.active) rec.steps.push(step);
                    }, 300);
                };

                const onChange = (e) => {
                    if (!rec.active || !e.isTrusted) return;
                    const el = e.target;
                    if (!el || !el.tagName || isIgnoredForPreset(el, ctx)) return;

                    const tag = el.tagName.toLowerCase();
                    let step = null;
                    if (tag === 'select') {
                        const opt = el.options[el.selectedIndex];
                        step = { kind: 'select', value: el.value, valueText: opt ? normalizeText(opt.textContent) : el.value };
                    } else if (tag === 'input' && (el.type === 'checkbox' || el.type === 'radio')) {
                        step = { kind: 'check', value: el.checked ? 'true' : 'false' };
                    } else if ((tag === 'input' && !/^(file|password|hidden|submit|button|image|reset)$/i.test(el.type)) || tag === 'textarea') {
                        step = { kind: 'input', value: String(el.value || '').slice(0, 500) };
                    }
                    if (!step) return;

                    step.selector = buildSelector(el);
                    step.tag = tag;
                    step.text = controlLabel(el);
                    step.state = '';
                    rec.steps.push(step);
                };

                document.addEventListener('click', onClick, true);
                document.addEventListener('change', onChange, true);

                const badge = document.createElement('div');
                badge.id = RECORDER_BADGE_ID;
                badge.textContent = badgeText || '● REC';
                Object.assign(badge.style, {
                    position: 'fixed', top: '8px', left: '50%', transform: 'translateX(-50%)',
                    zIndex: '2147483647', pointerEvents: 'none',
                    background: 'rgba(220, 38, 38, 0.92)', color: '#fff', borderRadius: '12px',
                    padding: '2px 12px', font: '12px/1.6 "Segoe UI", "Microsoft YaHei", sans-serif',
                    boxShadow: '0 2px 8px rgba(0, 0, 0, 0.3)'
                });
                (document.body || document.documentElement).appendChild(badge);

                rec.cleanup = () => {
                    rec.active = false;
                    try { document.removeEventListener('click', onClick, true); } catch (e) {}
                    try { document.removeEventListener('change', onChange, true); } catch (e) {}
                    try { if (badge.parentNode) badge.parentNode.removeChild(badge); } catch (e) {}
                };

                window.__aiHelperRecorder = rec;
                return { started: true, reason: "STARTED" };
            } catch (err) {
                return { started: false, reason: "EXCEPTION", message: err.message };
            }
        },

        /// Hands the steps recorded since the last call to the host
        takeRecordedSteps: function() {
            const rec = window.__aiHelperRecorder;
            if (!rec || !rec.active) return { active: false, steps: [] };
            return { active: true, steps: rec.steps.splice(0, rec.steps.length) };
        },

        stopRecording: function() {
            const rec = window.__aiHelperRecorder;
            window.__aiHelperRecorder = null;
            if (!rec) return { active: false, steps: [] };
            const steps = rec.steps.splice(0, rec.steps.length);
            try { rec.cleanup(); } catch (e) {}
            return { active: false, steps: steps };
        },

        /// Replays page preset steps in order. Each step waits for its element, because
        /// menu options only appear once the previous step opened the menu.
        applySetupSteps: async function(steps, stepTimeoutMs) {
            try {
                const myRun = window.__aiHelperRunId;
                const list = steps || [];
                let applied = 0;
                let skipped = 0;

                for (let i = 0; i < list.length; i++) {
                    const step = list[i] || {};
                    const deadline = Date.now() + (stepTimeoutMs || 6000);
                    let el = null;
                    while (true) {
                        if (window.__aiHelperRunId !== myRun) return { success: false, reason: "SUPERSEDED", index: i };
                        el = findStepElement(step);
                        if (el || Date.now() >= deadline) break;
                        await sleep(200);
                    }
                    if (!el) {
                        return { success: false, reason: "STEP_NOT_FOUND", index: i, text: step.text || step.selector || '' };
                    }

                    if (performStep(el, step)) {
                        applied++;
                        await sleep(450);
                    } else {
                        skipped++;
                        await sleep(60);
                    }
                }
                return { success: true, reason: "APPLIED", applied: applied, skipped: skipped };
            } catch (err) {
                return { success: false, reason: "EXCEPTION", message: err.message };
            }
        },

        /// Clicks a generated image and waits for the viewer to show the full-size version.
        /// The viewer is remembered on window so closeImageViewer can close it afterwards.
        openLargeImage: async function(src, minSize, timeoutMs) {
            try {
                const myRun = window.__aiHelperRunId;
                const min = minSize || 256;
                window.__aiHelperViewer = null;

                const imgs = document.querySelectorAll('img');
                let thumb = null;
                for (let i = imgs.length - 1; i >= 0; i--) {
                    if (!imgs[i].__aiHelperBaseline && (imgs[i].currentSrc || imgs[i].src || '') === src) {
                        thumb = imgs[i];
                        break;
                    }
                }
                if (!thumb) return { ok: false, clicked: false, reason: "THUMB_NOT_FOUND" };

                // A link around the image normally points at the full-size file itself
                const link = thumb.closest('a[href]');
                if (link) {
                    const href = link.href || '';
                    if (href && (href === src || looksLikeImageUrl(href))) {
                        return { ok: true, clicked: false, reason: "LINK", src: href };
                    }
                    // Clicking would leave the chat page
                    return { ok: false, clicked: false, reason: "LINK_NOT_IMAGE" };
                }

                const before = new Map();
                for (let i = 0; i < imgs.length; i++) before.set(imgs[i], imgs[i].currentSrc || imgs[i].src || '');
                const url = location.href;
                const added = [];
                const observer = new MutationObserver(list => {
                    for (let i = 0; i < list.length; i++) {
                        const nodes = list[i].addedNodes;
                        for (let j = 0; j < nodes.length; j++) {
                            if (nodes[j].nodeType === 1) added.push(nodes[j]);
                        }
                    }
                });
                observer.observe(document.body || document.documentElement, { childList: true, subtree: true });

                try { thumb.scrollIntoView({ block: 'center' }); } catch (e) {}
                const clickedAt = Date.now();
                fireClick(thumb);

                const deadline = clickedAt + (timeoutMs || 15000);
                let best = null;
                let bestSrc = '';
                let stableSince = 0;
                try {
                    while (Date.now() < deadline) {
                        if (window.__aiHelperRunId !== myRun) return { ok: false, clicked: true, reason: "SUPERSEDED" };
                        await sleep(250);

                        let cand = null;
                        let candArea = 0;
                        let loading = false;
                        const now = document.querySelectorAll('img');
                        for (let i = 0; i < now.length; i++) {
                            const img = now[i];
                            const s = img.currentSrc || img.src || '';
                            if (!s || /^data:image\/svg/i.test(s) || /\.svg(\?|#|$)/i.test(s)) continue;
                            // New images, or an existing one whose source was swapped (in-place zoom)
                            if (before.has(img) && before.get(img) === s) continue;
                            if (!isShown(img)) continue;
                            if (!img.complete) {
                                loading = true;
                                continue;
                            }
                            if (img.naturalWidth < min || img.naturalHeight < min) continue;
                            const area = img.naturalWidth * img.naturalHeight;
                            if (area > candArea) {
                                cand = img;
                                candArea = area;
                            }
                        }

                        if (cand) {
                            const s = cand.currentSrc || cand.src;
                            if (s !== bestSrc) {
                                best = cand;
                                bestSrc = s;
                                stableSince = Date.now();
                            } else if (!loading && Date.now() - stableSince >= 800) {
                                break;
                            }
                        } else if (!loading && Date.now() - clickedAt >= 5000) {
                            // Nothing is opening: the click does not show a viewer on this page
                            break;
                        }
                    }
                } finally {
                    observer.disconnect();
                }

                // In-place zoom (the thumbnail itself got the bigger source) has no overlay to close
                const overlay = best ? fixedAncestor(best) : addedOverlay(added);
                window.__aiHelperViewer = { overlay: overlay, url: url };
                if (!best) return { ok: false, clicked: true, reason: "NO_LARGE_IMAGE" };
                return { ok: true, clicked: true, reason: "OPENED", src: bestSrc, w: best.naturalWidth, h: best.naturalHeight };
            } catch (err) {
                return { ok: false, clicked: true, reason: "EXCEPTION", message: err.message };
            }
        },

        /// Closes the viewer opened by openLargeImage: Escape, then a close button, then
        /// the backdrop, then history back if the viewer changed the URL. Buttons and the
        /// backdrop are only clicked inside a detected overlay, never elsewhere on the page.
        closeImageViewer: async function() {
            try {
                const viewer = window.__aiHelperViewer;
                window.__aiHelperViewer = null;
                if (!viewer) return { closed: true, reason: "NO_VIEWER" };

                const isOpen = () => {
                    if (location.href !== viewer.url) return true;
                    return !!viewer.overlay && viewer.overlay.isConnected && isShown(viewer.overlay);
                };
                const settled = async () => {
                    await sleep(400);
                    return !isOpen();
                };
                const pressEscape = () => {
                    const keyTarget = document.activeElement || document.body;
                    ['keydown', 'keyup'].forEach(type => {
                        try {
                            keyTarget.dispatchEvent(new KeyboardEvent(type, {
                                key: 'Escape', code: 'Escape', keyCode: 27, which: 27, bubbles: true, cancelable: true
                            }));
                        } catch (e) {}
                    });
                };

                if (!isOpen()) {
                    // No overlay was found (in-place zoom, or nothing opened): Escape is the only safe move
                    if (!viewer.overlay) pressEscape();
                    return { closed: true, reason: viewer.overlay ? "ALREADY_CLOSED" : "NO_OVERLAY" };
                }

                pressEscape();
                if (await settled()) return { closed: true, reason: "ESCAPE" };

                if (viewer.overlay && viewer.overlay.isConnected) {
                    const closeBtn = findCloseButton(viewer.overlay);
                    if (closeBtn) {
                        fireClick(closeBtn);
                        if (await settled()) return { closed: true, reason: "CLOSE_BUTTON" };
                    }

                    // Dispatched on the backdrop itself, so "click outside" handlers see it as their own target
                    fireClick(viewer.overlay);
                    if (await settled()) return { closed: true, reason: "BACKDROP" };
                }

                if (location.href !== viewer.url) {
                    history.back();
                    if (await settled()) return { closed: true, reason: "HISTORY_BACK" };
                }
                return { closed: !isOpen(), reason: "STILL_OPEN" };
            } catch (err) {
                return { closed: false, reason: "EXCEPTION", message: err.message };
            }
        },

        /// Focuses the input element and places cursor at the end
        focusToEnd: function(inputSelector) {
            try {
                const platform = detectPlatform();
                const inputEl = findInput(platform, inputSelector);
                if (!inputEl) return { success: false, reason: "INPUT_NOT_FOUND" };
                setCursorToEnd(inputEl);
                return { success: true, reason: "SUCCESS" };
            } catch (err) {
                return { success: false, reason: "EXCEPTION", message: err.message };
            }
        },

        submit: async function(inputSelector, submitSelector) {
            try {
                const platform = detectPlatform();
                const inputEl = findInput(platform, inputSelector);
                if (!inputEl) return { success: false, reason: "INPUT_NOT_FOUND" };

                let submitBtn = findSubmitButton(platform, inputEl, submitSelector);
                if (!submitBtn || !isButtonReady(submitBtn)) {
                    return { success: false, reason: "SUBMIT_NOT_READY" };
                }

                const targetBtn = submitBtn.closest('button, [role="button"], div[class*="button"]') || submitBtn;
                const initialText = readText(inputEl);
                const initialAttachments = countAttachments();

                const triggerClick = (el) => {
                    if (!el) return;
                    try {
                        const opts = { bubbles: true, cancelable: true, view: window };
                        el.dispatchEvent(new PointerEvent('pointerdown', opts));
                        el.dispatchEvent(new MouseEvent('mousedown', opts));
                        el.dispatchEvent(new PointerEvent('pointerup', opts));
                        el.dispatchEvent(new MouseEvent('mouseup', opts));
                        el.click();
                    } catch (e) {
                        try { el.click(); } catch(e2) {}
                    }
                };

                const triggerEnter = (el) => {
                    if (!el) return;
                    try {
                        el.focus();
                        const opts = {
                            key: 'Enter',
                            code: 'Enter',
                            keyCode: 13,
                            which: 13,
                            bubbles: true,
                            cancelable: true,
                            composed: true
                        };
                        el.dispatchEvent(new KeyboardEvent('keydown', opts));
                        el.dispatchEvent(new KeyboardEvent('keypress', opts));
                        el.dispatchEvent(new KeyboardEvent('keyup', opts));
                    } catch (e) {}
                };

                const isSubmitted = () => {
                    const currentText = readText(inputEl);
                    const currentAttachments = countAttachments();
                    const currentBtn = findSubmitButton(platform, inputEl, submitSelector);

                    // 1. Text has been cleared (if there was text)
                    if (initialText.trim().length > 0 && currentText.trim().length === 0) return true;
                    // 2. Attachments have been consumed/cleared (if there were attachments)
                    if (initialAttachments > 0 && currentAttachments < initialAttachments) return true;
                    // 3. Submit button became disabled or switched to stop/generating
                    if (currentBtn) {
                        if (!isButtonReady(currentBtn)) return true;
                        const btnAria = (currentBtn.getAttribute('aria-label') || '').toLowerCase();
                        const btnCls = (currentBtn.className || '').toString().toLowerCase();
                        if (btnAria.includes('stop') || btnAria.includes('停止') || btnCls.includes('stop')) return true;
                    }
                    return false;
                };

                // Attempt 1: Full pointer/mouse click
                triggerClick(targetBtn);
                if (targetBtn !== submitBtn) {
                    triggerClick(submitBtn);
                }

                await sleep(350);
                if (isSubmitted()) {
                    return { success: true, reason: "CLICKED" };
                }

                // Attempt 2: Enter key fallback
                triggerEnter(inputEl);
                await sleep(350);
                if (isSubmitted()) {
                    return { success: true, reason: "ENTER_KEY" };
                }

                // Attempt 3: Retry click
                triggerClick(targetBtn);
                await sleep(300);
                if (isSubmitted()) {
                    return { success: true, reason: "RETRY_CLICKED" };
                }

                // If button state changed or at least click completed without throwing
                const finalBtn = findSubmitButton(platform, inputEl, submitSelector);
                if (!finalBtn || !isButtonReady(finalBtn)) {
                    return { success: true, reason: "CLICKED_NOT_READY" };
                }

                return { success: false, reason: "SUBMIT_UNACKNOWLEDGED" };
            } catch (err) {
                return { success: false, reason: "EXCEPTION", message: err.message };
            }
        }
    };
})();
