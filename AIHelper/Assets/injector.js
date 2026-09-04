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
