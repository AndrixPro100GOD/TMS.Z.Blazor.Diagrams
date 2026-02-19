function createDocUpHandler() {
    return function (e) {
        for (let id in s.canvases) {
            const c = s.canvases[id];
            if (!c.elem || c.activePointerId !== e.pointerId) continue;
            if (c.elem.contains(e.target)) continue;
            // pointerup/pointercancel вне канваса — диспатчим синтетический pointerup на канвас,
            // чтобы Blazor получил событие и завершил перетаскивание/пан.
            c.elem.dispatchEvent(new PointerEvent(e.type, {
                pointerId: e.pointerId,
                clientX: e.clientX,
                clientY: e.clientY,
                button: e.button,
                buttons: e.buttons,
                bubbles: true,
                cancelable: true
            }));
            c.activePointerId = null;
            break;
        }
    };
}
var s = {
    canvases: {},
    tracked: {},
    docUpHandler: null,
    getBoundingClientRect: el => {
        return el.getBoundingClientRect();
    },
    mo: new MutationObserver(() => {
        for (id in s.canvases) {
            const canvas = s.canvases[id];
            const lastBounds = canvas.lastBounds;
            const bounds = canvas.elem.getBoundingClientRect();
            if (lastBounds.left !== bounds.left || lastBounds.top !== bounds.top || lastBounds.width !== bounds.width ||
                lastBounds.height !== bounds.height) {
                canvas.lastBounds = bounds;
                canvas.ref.invokeMethodAsync('OnResize', bounds);
            }
        }
    }),
    ro: new ResizeObserver(entries => {
        for (const entry of entries) {
            let id = Array.from(entry.target.attributes).find(e => e.name.startsWith('_bl')).name.substring(4);
            let element = s.tracked[id];
            if (element) {
                element.ref.invokeMethodAsync('OnResize', entry.target.getBoundingClientRect());
            }
        }
    }),
    observe: (element, ref, id) => {
        if (!element) return;
        s.ro.observe(element);
        s.tracked[id] = {
            ref: ref
        };
        if (element.classList.contains("diagram-canvas")) {
            s.canvases[id] = {
                elem: element,
                ref: ref,
                lastBounds: element.getBoundingClientRect(),
                activePointerId: null
            };
            const captureHandler = (e) => {
                element.setPointerCapture(e.pointerId);
                s.canvases[id].activePointerId = e.pointerId;
            };
            const clearPointer = () => {
                if (s.canvases[id]) s.canvases[id].activePointerId = null;
            };
            s.canvases[id].captureHandler = captureHandler;
            s.canvases[id].clearPointer = clearPointer;
            element.addEventListener('pointerdown', captureHandler, true);
            element.addEventListener('pointerup', clearPointer);
            element.addEventListener('pointercancel', clearPointer);
            if (!s.docUpHandler) {
                s.docUpHandler = createDocUpHandler();
                document.addEventListener('pointerup', s.docUpHandler, true);
                document.addEventListener('pointercancel', s.docUpHandler, true);
            }
        }
    },
    unobserve: (element, id) => {
        const canvas = s.canvases[id];
        if (canvas && canvas.elem) {
            if (canvas.captureHandler) canvas.elem.removeEventListener('pointerdown', canvas.captureHandler, true);
            if (canvas.clearPointer) {
                canvas.elem.removeEventListener('pointerup', canvas.clearPointer);
                canvas.elem.removeEventListener('pointercancel', canvas.clearPointer);
            }
        }
        if (element) {
            s.ro.unobserve(element);
        }
        delete s.tracked[id];
        delete s.canvases[id];
    }
};
window.ZBlazorDiagrams = s;
window.addEventListener('scroll', () => {
    for (id in s.canvases) {
        const canvas = s.canvases[id];
        canvas.lastBounds = canvas.elem.getBoundingClientRect();
        canvas.ref.invokeMethodAsync('OnResize', canvas.lastBounds);
    }
});
s.mo.observe(document.body, {childList: true, subtree: true});