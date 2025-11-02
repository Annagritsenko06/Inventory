// Simple AutoSave with localStorage backup
class SimpleAutoSave {
    constructor() {
        this.saveInterval = 10000; // 10 seconds
        this.timeouts = new Map();
        this.init();
    }

    init() {
        // Track form changes
        document.addEventListener('input', (e) => {
            if (e.target.matches('input, textarea, select')) {
                this.debounceSave(e.target);
            }
        });

        // Auto-save timer
        setInterval(() => {
            this.autoSave();
        }, this.saveInterval);

        // Save before page unload
        window.addEventListener('beforeunload', () => {
            this.autoSave(true);
        });

        // Restore from localStorage on page load
        this.restoreFromLocalStorage();
    }

    debounceSave(element) {
        const form = element.closest('form');
        if (!form) return;

        const formId = form.id || 'default';
        const key = `${formId}_${element.name || element.id}`;
        
        if (this.timeouts.has(key)) {
            clearTimeout(this.timeouts.get(key));
        }

        this.timeouts.set(key, setTimeout(() => {
            this.saveToLocalStorage(form);
        }, 2000)); // 2 second debounce
    }

    saveToLocalStorage(form) {
        const formData = new FormData(form);
        const data = Object.fromEntries(formData.entries());
        const formId = form.id || 'default';
        
        localStorage.setItem(`autosave_${formId}`, JSON.stringify({
            data: data,
            timestamp: Date.now()
        }));
    }

    restoreFromLocalStorage() {
        const forms = document.querySelectorAll('form');
        forms.forEach(form => {
            const formId = form.id || 'default';
            const saved = localStorage.getItem(`autosave_${formId}`);
            
            if (saved) {
                try {
                    const { data, timestamp } = JSON.parse(saved);
                    const age = Date.now() - timestamp;
                    
                    // Only restore if less than 1 hour old
                    if (age < 3600000) {
                        Object.entries(data).forEach(([key, value]) => {
                            const element = form.querySelector(`[name="${key}"]`);
                            if (element && element.type !== 'password') {
                                element.value = value;
                            }
                        });
                    }
                } catch (e) {
                    console.error('Failed to restore form data:', e);
                }
            }
        });
    }

    async autoSave(immediate = false) {
        const forms = document.querySelectorAll('form[data-autosave]');
        
        for (const form of forms) {
            try {
                await this.saveForm(form, immediate);
            } catch (error) {
                console.error('Auto-save error:', error);
            }
        }
    }

    async saveForm(form, immediate = false) {
        const formData = new FormData(form);
        const data = Object.fromEntries(formData.entries());
        const endpoint = form.dataset.autosaveEndpoint || form.action;
        
        if (!endpoint) return;

        const response = await fetch(endpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
            },
            body: JSON.stringify(data)
        });

        if (response.ok) {
            // Clear localStorage on successful save
            const formId = form.id || 'default';
            localStorage.removeItem(`autosave_${formId}`);
            
            if (immediate) {
                this.showStatus('success', 'Saved');
            } else {
                this.showStatus('info', 'Auto-saved');
            }
        }
    }

    showStatus(type, message) {
        const existing = document.querySelector('.autosave-status');
        if (existing) existing.remove();

        const status = document.createElement('div');
        status.className = `autosave-status alert alert-${type === 'error' ? 'danger' : type === 'success' ? 'success' : 'info'} alert-dismissible fade show`;
        status.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            z-index: 9999;
            min-width: 200px;
        `;
        
        status.innerHTML = `
            <i class="fas fa-${type === 'error' ? 'exclamation-triangle' : type === 'success' ? 'check' : 'info-circle'}"></i>
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;

        document.body.appendChild(status);

        setTimeout(() => {
            if (status.parentNode) {
                status.remove();
            }
        }, 3000);
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.simpleAutoSave = new SimpleAutoSave();
});
