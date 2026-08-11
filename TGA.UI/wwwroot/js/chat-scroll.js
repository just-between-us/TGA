window.chatScroll = {
    _listeners: {},

    attach: function (elementId, dotnetRef) {
        const el = document.getElementById(elementId);
        if (!el) return;

        if (this._listeners[elementId]) {
            el.removeEventListener('scroll', this._listeners[elementId]);
        }

        const handler = () => {
            if (el.scrollTop < 100) {
                dotnetRef.invokeMethodAsync('OnScrolledToTop');
            }
        };

        this._listeners[elementId] = handler;
        el.addEventListener('scroll', handler);
    },

    getScrollHeight: function (elementId) {
        const el = document.getElementById(elementId);
        return el ? el.scrollHeight : 0;
    },

    restoreScroll: function (elementId, previousHeight) {
        const el = document.getElementById(elementId);
        if (!el) return;
        const delta = el.scrollHeight - previousHeight;
        el.scrollTop = delta;
    },

    scrollToBottom: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) el.scrollTop = el.scrollHeight;
    }
};