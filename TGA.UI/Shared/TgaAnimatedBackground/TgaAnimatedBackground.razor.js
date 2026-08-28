import {
    animate,
    stagger
} from "https://cdn.jsdelivr.net/npm/motion@13.1.1/+esm";


const SYMBOLS = [
    "·", "•", "+", "×", "◇", "○", ":", "∷", "✦"
];

const SYMBOL_COUNT = 46; // меньше символов — фон больше не перегружен

const LIGHT_COLORS = [
    "rgb(43 123 228)",
    "rgb(70 90 115)",
    "rgb(120 135 150)"
];

const DARK_COLORS = [
    "rgb(0 131 255)",
    "rgb(120 135 160)",
    "rgb(190 200 215)"
];


// Параметры блобов авроры: позиция (%), размер, задержка волны
const BLOBS = [
    { x: 18, y: 22, size: 46, hueLight: "43,123,228", hueDark: "0,131,255" },
    { x: 78, y: 30, size: 38, hueLight: "70,90,115",  hueDark: "120,135,160" },
    { x: 55, y: 78, size: 52, hueLight: "120,135,150", hueDark: "190,200,215" },
    { x: 12, y: 82, size: 34, hueLight: "43,123,228", hueDark: "0,131,255" }
];


const instances = new WeakMap();


export function initialize(root, isDarkMode) {

    if (!root || instances.has(root)) {
        return;
    }

    const field =
        root.querySelector(".tga-symbol-field");

    const auroraLayer =
        root.querySelector(".tga-aurora-layer");

    const gridLayer =
        root.querySelector(".tga-grid-layer");

    if (!field || !auroraLayer || !gridLayer) {
        return;
    }

    root.dataset.theme =
        isDarkMode ? "dark" : "light";

    const reducedMotion =
        window.matchMedia(
            "(prefers-reduced-motion: reduce)"
        ).matches;

    // --- Аврора: блурные пятна ---
    const blobs = BLOBS.map(cfg => {

        const el = document.createElement("div");
        el.className = "tga-aurora-blob";

        el.style.left = `${cfg.x}%`;
        el.style.top = `${cfg.y}%`;
        el.style.width = `${cfg.size}vmax`;
        el.style.height = `${cfg.size}vmax`;

        auroraLayer.appendChild(el);

        return { element: el, cfg };
    });

    applyBlobColors(blobs, isDarkMode);

    // --- Символьное поле (как раньше, но реже) ---
    const symbols = [];

    for (let i = 0; i < SYMBOL_COUNT; i++) {

        const element = document.createElement("span");
        element.className = "tga-background-symbol";

        element.textContent =
            SYMBOLS[Math.floor(Math.random() * SYMBOLS.length)];

        const x = Math.random() * 100;
        const y = Math.random() * 100;
        const size = 8 + Math.random() * 8;
        const rotation = -25 + Math.random() * 50;

        element.style.left = `${x}%`;
        element.style.top = `${y}%`;
        element.style.fontSize = `${size}px`;
        element.style.transform =
            `translate3d(0, 0, 0) rotate(${rotation}deg)`;
        element.style.color = getColor(isDarkMode);

        field.appendChild(element);

        symbols.push({ element, x, y, size, rotation });
    }

    const state = {
        root,
        field,
        auroraLayer,
        gridLayer,
        blobs,
        symbols,

        reducedMotion,
        isDarkMode,

        animations: [],
        destroyed: false,

        pointerX: 0.5,
        pointerY: 0.5,
        targetPointerX: 0.5,
        targetPointerY: 0.5,

        onPointerMove: null,
        rafId: null
    };

    instances.set(root, state);

    if (reducedMotion) {

        symbols.forEach(item => {
            item.element.style.opacity = getBaseOpacity(item);
        });

        blobs.forEach(({ element }) => {
            element.style.opacity = "0.5";
        });

        gridLayer.style.opacity = "0.35";

        return;
    }

    playEntrance(state);
    playAmbientMotion(state);
    playAuroraMotion(state);
    startSpotlight(state);

    const onPointerMove = event => {

        if (state.destroyed) {
            return;
        }

        state.targetPointerX = event.clientX / window.innerWidth;
        state.targetPointerY = event.clientY / window.innerHeight;
    };

    window.addEventListener(
        "pointermove",
        onPointerMove,
        { passive: true }
    );

    state.onPointerMove = onPointerMove;
}


function playEntrance(state) {

    const elements =
        state.symbols.map(item => item.element);

    elements.forEach(element => {
        element.style.opacity = "0";
    });

    const symbolsAnim = animate(
        elements,
        { opacity: [0, 1] },
        {
            delay: stagger(0.025, { from: "center", ease: "easeOut" }),
            duration: 1.8,
            ease: "easeOut"
        }
    );

    state.blobs.forEach(({ element }) => {
        element.style.opacity = "0";
    });

    const auroraAnim = animate(
        state.blobs.map(b => b.element),
        { opacity: [0, 1] },
        { duration: 2.2, ease: "easeOut" }
    );

    state.gridLayer.style.opacity = "0";

    const gridAnim = animate(
        state.gridLayer,
        { opacity: [0, 1] },
        { duration: 1.6, ease: "easeOut" }
    );

    state.animations.push(symbolsAnim, auroraAnim, gridAnim);
}


function playAmbientMotion(state) {

    state.symbols.forEach(item => {

        const element = item.element;

        const x = -20 + Math.random() * 40;
        const y = -25 + Math.random() * 50;
        const rotate = -15 + Math.random() * 30;
        const scale = 0.85 + Math.random() * 0.3;
        const duration = 7 + Math.random() * 9;
        const opacity = getBaseOpacity(item);

        const animation = animate(
            element,
            {
                x: [0, x, 0],
                y: [0, y, 0],
                rotate: [
                    item.rotation,
                    item.rotation + rotate,
                    item.rotation
                ],
                scale: [1, scale, 1],
                opacity: [
                    opacity * 0.55,
                    opacity,
                    opacity * 0.55
                ]
            },
            {
                duration,
                delay: Math.random() * 5,
                repeat: Infinity,
                ease: "easeInOut"
            }
        );

        state.animations.push(animation);
    });
}


function playAuroraMotion(state) {

    state.blobs.forEach(({ element }, index) => {

        const dx = -6 + Math.random() * 12;
        const dy = -6 + Math.random() * 12;
        const scale = 0.9 + Math.random() * 0.25;
        const duration = 16 + Math.random() * 10;

        const animation = animate(
            element,
            {
                x: [0, `${dx}vmax`, 0],
                y: [0, `${dy}vmax`, 0],
                scale: [1, scale, 1]
            },
            {
                duration,
                delay: index * 1.4,
                repeat: Infinity,
                ease: "easeInOut"
            }
        );

        state.animations.push(animation);
    });
}


// Плавный "прожектор" вокруг курсора через grid-слой (CSS-переменные)
function startSpotlight(state) {

    const step = () => {

        if (state.destroyed) {
            return;
        }

        // Инерция — сглаживаем движение курсора
        state.pointerX +=
            (state.targetPointerX - state.pointerX) * 0.06;

        state.pointerY +=
            (state.targetPointerY - state.pointerY) * 0.06;

        state.gridLayer.style.setProperty(
            "--pointer-x",
            `${state.pointerX * 100}%`
        );

        state.gridLayer.style.setProperty(
            "--pointer-y",
            `${state.pointerY * 100}%`
        );

        state.rafId =
            window.requestAnimationFrame(step);
    };

    state.rafId =
        window.requestAnimationFrame(step);
}


export function setTheme(root, isDarkMode) {

    const state = instances.get(root);

    if (!state) {
        return;
    }

    state.isDarkMode = isDarkMode;

    root.dataset.theme =
        isDarkMode ? "dark" : "light";

    state.symbols.forEach(item => {

        animate(
            item.element,
            { color: getColor(isDarkMode) },
            { duration: 0.65, ease: "easeInOut" }
        );
    });

    applyBlobColors(state.blobs, isDarkMode, true);
}


function applyBlobColors(blobs, isDarkMode, transition = false) {

    blobs.forEach(({ element, cfg }) => {

        const rgb =
            isDarkMode ? cfg.hueDark : cfg.hueLight;

        const gradient =
            `radial-gradient(circle, rgba(${rgb}, 0.32) 0%, rgba(${rgb}, 0) 70%)`;

        if (transition) {
            animate(
                element,
                { backgroundImage: gradient },
                { duration: 0.65, ease: "easeInOut" }
            );
        } else {
            element.style.backgroundImage = gradient;
        }
    });
}


function getColor(isDarkMode) {

    const palette =
        isDarkMode ? DARK_COLORS : LIGHT_COLORS;

    return palette[
        Math.floor(Math.random() * palette.length)
        ];
}


function getBaseOpacity(item) {
    return 0.45 + Math.random() * 0.45;
}


export function dispose(root) {

    const state = instances.get(root);

    if (!state) {
        return;
    }

    state.destroyed = true;

    state.animations.forEach(animation => {
        animation?.stop();
    });

    if (state.rafId) {
        window.cancelAnimationFrame(state.rafId);
    }

    if (state.onPointerMove) {
        window.removeEventListener(
            "pointermove",
            state.onPointerMove
        );
    }

    instances.delete(root);
}