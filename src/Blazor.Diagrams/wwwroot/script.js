// PERFORMANCE OPTIMIZATION NEEDED:
// Каждый pointermove вызывает полный JS→.NET→JS цикл через endInvokeJSFromDotNet
// Это создает 200ms задержку при отрисовке схемы
//
// Возможные решения:
// 1. Throttle pointermove events до 60fps (16ms)
// 2. Использовать passive event listeners
// 3. Синхронизировать с requestAnimationFrame
// 4. Перейти на Canvas-based rendering для интерактивных элементов

function createDocUpHandler() {
    return function (e) {
        for (let id in s.canvases) {
            const c = s.canvases[id];
            if (!c.elem || c.activePointerId !== e.pointerId) continue;
            if (c.elem.contains(e.target)) continue;
            // pointerup/pointercancel вне канваса — вызываем .NET напрямую (dispatchEvent
            // не срабатывает в Blazor Server). Завершает перетаскивание/пан.
            c.ref.invokeMethodAsync('OnPointerUpOutside', e.clientX, e.clientY, e.button, e.buttons, e.pointerId);
            c.activePointerId = null;
            break;
        }
    };
}

function isInteractiveTarget(target) {
    if (!target || !(target instanceof Element)) return false;
    return !!target.closest(
        'button, input, textarea, select, option, label, a, [contenteditable="true"], [role="button"], ' +
        '.mud-button-root, .mud-icon-button, .mud-switch, .mud-input, .mud-input-control, .mud-select, .mud-autocomplete'
    );
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

            // Оптимизированные обработчики с throttle и RAF синхронизацией
            const captureHandler = (e) => {
                // Не захватываем pointer для интерактивных контролов внутри нодов:
                // иначе клики/ввод в MudBlazor-элементах могут "ломаться" из-за глобального capture.
                if (isInteractiveTarget(e.target)) {
                    return;
                }
                // Средняя кнопка мыши (button===1): блокируем браузерный "autoscroll" режим
                // (крестовый курсор со стрелками). Без этого browser перехватывает mousemove
                // под свою прокрутку, и наш pan не работает.
                // Дополнительно уведомляем .NET прямо из capture-фазы — до того, как вложенные
                // элементы (напр. StockCell) вызовут e.stopPropagation() в bubble-фазе.
                // Это гарантирует, что PanBehavior.StartMiddleButtonPan всегда получит событие.
                if (e.button === 1) {
                    e.preventDefault();
                    c.ref.invokeMethodAsync('OnMiddleButtonPointerDownCapture', e.clientX, e.clientY, e.pointerId);
                }
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