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
        } else {
            // ContentEditable
            document.execCommand('selectAll', false, null);
            document.execCommand('insertText', false, text);
            if (!inputEl.textContent) {
                inputEl.textContent = text;
            }
            inputEl.dispatchEvent(new Event('input', { bubbles: true, cancelable: true }));
        }
    }

    const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

    window.AiHelperInjector = {
        inject: async function(text, autoSubmit, inputSelector, submitSelector, newChatSelector) {
            try {
                const url = window.location.href;
                
                // 1. Detect platform and login status
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

                // 2. Click New Chat button if selector specified or auto-detected
                let newChatBtn = null;
                if (newChatSelector) {
                    try {
                        newChatBtn = document.querySelector(newChatSelector);
                    } catch(e) {}
                }
                if (!newChatBtn) {
                    if (platform === "deepseek") {
                        newChatBtn = document.querySelector('div[class*="new-chat"]') || 
                                     document.querySelector('div[class*="sidebar"] div[class*="button"]') ||
                                     Array.from(document.querySelectorAll('div, button, a')).find(el => el.textContent && (el.textContent.trim() === '开启新对话' || el.textContent.trim() === '新对话' || el.textContent.trim() === '新建对话'));
                    } else if (platform === "claude") {
                        newChatBtn = document.querySelector('button[aria-label*="New chat"]') || 
                                     document.querySelector('button[aria-label*="新对话"]') || 
                                     document.querySelector('a[href="/new"]');
                    } else if (platform === "gemini") {
                        newChatBtn = document.querySelector('button[aria-label*="New chat"]') || 
                                     document.querySelector('button[aria-label*="新对话"]') || 
                                     document.querySelector('a[aria-label*="New chat"]') || 
                                     document.querySelector('a[aria-label*="新对话"]');
                    }
                }

                if (newChatBtn) {
                    try {
                        newChatBtn.click();
                        const svg = newChatBtn.querySelector('svg');
                        if (svg) {
                            svg.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
                        }
                    } catch (e) {
                        console.error("New chat click error:", e);
                    }
                    // Delay 500ms after clicking new session before proceeding to prompt injection
                    await sleep(500);
                }

                // Helper to locate input element
                function findInput() {
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

                // 3. Find input element (with retries if needed for DOM to stabilize)
                let inputEl = findInput();
                if (!inputEl) {
                    for (let i = 0; i < 10; i++) {
                        await sleep(100);
                        inputEl = findInput();
                        if (inputEl) break;
                    }
                }

                if (!inputEl) {
                    return { success: false, reason: "INPUT_NOT_FOUND" };
                }

                // 4. Inject text
                doInjectText(inputEl, text);

                // 5. Auto-submit after 500ms delay
                if (autoSubmit) {
                    // Delay 500ms after injecting prompt before clicking submit
                    await sleep(500);
                    try {
                        let currentInput = findInput() || inputEl;
                        if (currentInput) {
                            const isTextarea = currentInput.tagName.toLowerCase() === 'textarea';
                            const currentText = isTextarea ? currentInput.value : currentInput.textContent;
                            if (!currentText || currentText.trim() === '') {
                                doInjectText(currentInput, text);
                            }
                        }
                        window.AiHelperInjector.submit(currentInput || inputEl, platform, submitSelector);
                    } catch (e) {
                        console.error("Auto submit error:", e);
                    }
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
