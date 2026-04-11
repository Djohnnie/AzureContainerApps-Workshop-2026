let dotNetRef = null;

export function init(ref) {
    dotNetRef = ref;
    window.addEventListener('keydown', onKey);
}

export function dispose() {
    window.removeEventListener('keydown', onKey);
    dotNetRef = null;
}

function onKey(e) {
    const handled = [
        'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight',
        'w', 'W', 'a', 'A', 's', 'S', 'd', 'D',
        'z', 'Z', 'q', 'Q'
    ];
    if (handled.includes(e.key)) {
        e.preventDefault();
        dotNetRef.invokeMethodAsync('HandleKey', e.key);
    }
}
