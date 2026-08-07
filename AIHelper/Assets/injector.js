(function() {
    window.AiHelperInjector = {
        inject: async function(text, autoSubmit) {
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

            // 3. Find input element
            let inputEl = null;
            if (platform === "claude") {
                inputEl = document.querySelector('div.ProseMirror[contenteditable="true"]') || document.querySelector('[contenteditable="true"]');
            } else if (platform === "gemini") {
                inputEl = document.querySelector('rich-textarea div[contenteditable="true"]') || document.querySelector('div[contenteditable="true"]');
            } else if (platform === "deepseek") {
                inputEl = document.querySelector('textarea#chat-input') || document.querySelector('textarea');
            } else {
                inputEl = document.querySelector('textarea') || document.querySelector('[contenteditable="true"]');
            }

            if (!inputEl) {
                return { success: false, reason: "INPUT_NOT_FOUND" };
            }

            // 4. Inject text
            inputEl.focus();
            const isTextarea = inputEl.tagName.toLowerCase() === 'textarea';

            if (isTextarea) {
                const nativeInputValueSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, "value")?.set;
                if (nativeInputValueSetter) {
                    nativeInputValueSetter.call(inputEl, text);
                } else {
                    inputEl.value = text;
                }
                inputEl.dispatchEvent(new Event('input', { bubbles: true }));
                inputEl.dispatchEvent(new Event('change', { bubbles: true }));
            } else {
                // ContentEditable
                document.execCommand('selectAll', false, null);
                document.execCommand('insertText', false, text);
                inputEl.dispatchEvent(new Event('input', { bubbles: true }));
            }

            // 5. Auto-submit
            if (autoSubmit) {
                await new Promise(r => setTimeout(r, 300));
                
                let submitBtn = null;
                if (platform === "claude") {
                    submitBtn = document.querySelector('button[aria-label="Send Message"]') || document.querySelector('button:has(svg)');
                } else if (platform === "gemini") {
                    submitBtn = document.querySelector('button[aria-label="Send message"]') || document.querySelector('.send-button');
                } else if (platform === "deepseek") {
                    submitBtn = document.querySelector('.send-button') || document.querySelector('div[role="button"]:has(svg)');
                }
                
                if (submitBtn && !submitBtn.disabled && !submitBtn.hasAttribute('disabled')) {
                    submitBtn.click();
                } else {
                    // Fallback to Enter key
                    const enterEvent = new KeyboardEvent('keydown', {
                        key: 'Enter',
                        code: 'Enter',
                        keyCode: 13,
                        which: 13,
                        bubbles: true,
                        cancelable: true
                    });
                    inputEl.dispatchEvent(enterEvent);
                }
            }

            return { success: true, reason: "SUCCESS" };
        }
    };
})();
