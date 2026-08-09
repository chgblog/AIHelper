(function() {
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

    window.AiHelperInjector = {
        inject: function(text, autoSubmit, inputSelector, submitSelector) {
            try {
                const url = window.location.href;
                
                // 1 & 2. Detect platform and login status
                let platform = "generic";
                if (url.includes("claude.ai")) {
                    platform = "claude";
                    if (url.includes("/login") || document.querySelector('a[href*="/login"]')) {
                        return { success: false, reason: "NOT_LOGGED_IN" };
                    }
                } else if (url.includes("gemini.google.com")) {
                    platform = "gemini";
                    if (url.includes("accounts.google.com")) {
                        return { success: false, reason: "NOT_LOGGED_IN" };
                    }
                } else if (url.includes("deepseek.com")) {
                    platform = "deepseek";
                    if (url.includes("/sign_in") || url.includes("/login")) {
                        return { success: false, reason: "NOT_LOGGED_IN" };
                    }
                }

                // 3. Find input element - prioritize custom selector
                let inputEl = null;

                if (inputSelector) {
                    try {
                        inputEl = document.querySelector(inputSelector);
                    } catch(e) {}
                }

                if (!inputEl) {
                    if (platform === "claude") {
                        inputEl = document.querySelector('div.ProseMirror[contenteditable="true"]') || document.querySelector('[contenteditable="true"]');
                    } else if (platform === "gemini") {
                        inputEl = document.querySelector('rich-textarea div[contenteditable="true"]') || document.querySelector('div[contenteditable="true"]');
                    } else if (platform === "deepseek") {
                        inputEl = document.querySelector('textarea#chat-input') || document.querySelector('textarea');
                    } else {
                        inputEl = document.querySelector('textarea') || document.querySelector('[contenteditable="true"]');
                    }
                }

                if (!inputEl) {
                    return { success: false, reason: "INPUT_NOT_FOUND" };
                }

                // 4. Inject text
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
                } else {
                    // ContentEditable
                    document.execCommand('selectAll', false, null);
                    document.execCommand('insertText', false, text);
                    if (!inputEl.textContent) {
                        inputEl.textContent = text;
                    }
                    inputEl.dispatchEvent(new Event('input', { bubbles: true, cancelable: true }));
                }

                // 5. Auto-submit
                if (autoSubmit) {
                    setTimeout(function() {
                        try {
                            window.AiHelperInjector.submit(inputEl, platform, submitSelector);
                        } catch (e) {
                            console.error("Auto submit error:", e);
                        }
                    }, 300);
                }

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
