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
                        window.AiHelperInjector.submit(currentInput, platform, submitSelector);
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

        submit: function(inputEl, platform, submitSelector) {
            if (!inputEl) return;

            // Find input container box
            let container = inputEl.closest('form');
            if (!container) {
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
            if (!container) container = inputEl.parentElement || document.body;

            // Try custom submit selector first
            let submitBtn = null;

            if (submitSelector) {
                try {
                    submitBtn = document.querySelector(submitSelector);
                } catch(e) {}
            }

            // Fallback to platform-specific selectors
            if (!submitBtn || submitBtn.offsetWidth === 0) {
                if (platform === "deepseek") {
                    submitBtn = container.querySelector('#chat-input-send-button') ||
                                container.querySelector('div[class*="send-button"]') ||
                                container.querySelector('div[class*="sendButton"]') ||
                                container.querySelector('div[class*="_send_button"]');
                } else if (platform === "claude") {
                    submitBtn = container.querySelector('button[aria-label*="Send"]') ||
                                container.querySelector('button[aria-label*="发送"]');
                } else if (platform === "gemini") {
                    submitBtn = container.querySelector('button[aria-label*="Send"]') ||
                                container.querySelector('button[aria-label*="发送"]') ||
                                container.querySelector('.send-button');
                }
            }

            if (!submitBtn || submitBtn.offsetWidth === 0 || isFileUploadElement(submitBtn)) {
                submitBtn = container.querySelector('button[type="submit"]') ||
                            container.querySelector('button[aria-label*="Send"]') ||
                            container.querySelector('button[aria-label*="发送"]') ||
                            container.querySelector('div[role="button"][aria-label*="Send"]') ||
                            container.querySelector('div[role="button"][aria-label*="发送"]');
            }

            // Fallback strategy: Find all SVG-containing elements inside container, exclude file uploads, and pick the rightmost one
            if (!submitBtn || submitBtn.offsetWidth === 0 || isFileUploadElement(submitBtn)) {
                const candidates = Array.from(container.querySelectorAll('button, div[role="button"], div, a'))
                    .filter(el => {
                        if (el.contains(inputEl)) return false;
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

            if (submitBtn && !isFileUploadElement(submitBtn)) {
                submitBtn.click();
                const svg = submitBtn.querySelector('svg');
                if (svg) {
                    svg.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
                }
            }
        }
    };
})();
