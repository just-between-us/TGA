import {
    animate,
    stagger
} from "https://cdn.jsdelivr.net/npm/motion@13.1.1/+esm";


const LOGO = [
    "########....######....####..",
    "########....######....####..",
    "...##.....##........##....##",
    "...##.....##....##..##....##",
    "...##.....##....##..########",
    "...##.....##....##..########",
    "...##.......######..##....##",
    "...##.......######..##....##"
];


const ROWS = LOGO.length;
const COLUMNS = LOGO[0].length;


const instances = new WeakMap();

export function initialize(root) {

    if (!root || instances.has(root)) {
        return;
    }

    const grid =
        root.querySelector(".tga-logo-grid");

    if (!grid) {
        return;
    }


    const pixels = [];

    for (let row = 0; row < ROWS; row++) {

        for (let col = 0; col < COLUMNS; col++) {

            const cell =
                document.createElement("span");

            const isPixel =
                LOGO[row][col] === "#";

            cell.className =
                isPixel
                    ? "tga-pixel"
                    : "tga-cell";

            cell.dataset.row = row;
            cell.dataset.col = col;

            cell.style.setProperty("--row", row);
            cell.style.setProperty("--col", col);

            if (isPixel) {
                for (let i = 0; i < 4; i++) {
                    const dot =
                        document.createElement("span");

                    dot.className = "tga-dot";
                    cell.appendChild(dot);
                }
            }

            grid.appendChild(cell);

            if (isPixel) {
                pixels.push(cell);
            }
        }
    }

    const reducedMotion =
        window.matchMedia(
            "(prefers-reduced-motion: reduce)"
        ).matches;

    const state = {
        root,
        grid,
        pixels,

        entrance: null,
        activeAnimations: [],

        destroyed: false,
        isAnimating: false,

        reducedMotion
    };


    instances.set(root, state);


    if (reducedMotion) {

        pixels.forEach(pixel => {

            pixel.style.opacity = "1";
            pixel.style.transform =
                "translate3d(0, 0, 0) scale(1)";
        });

    } else {

        playEntrance(state);
    }
    

    const onPointerEnter = () => {

        if (state.destroyed ||
            state.reducedMotion ||
            state.isAnimating) {

            return;
        }

        playHover(state);
    };


    const onClick = () => {

        if (state.destroyed ||
            state.reducedMotion) {

            return;
        }

        playRebuild(state);
    };


    root.addEventListener(
        "pointerenter",
        onPointerEnter
    );

    root.addEventListener(
        "click",
        onClick
    );


    state.onPointerEnter =
        onPointerEnter;

    state.onClick =
        onClick;
}


function playEntrance(state) {

    const {
        pixels
    } = state;


    state.isAnimating = true;


    pixels.forEach(pixel => {

        pixel.style.opacity = "0";

        pixel.style.transform =
            "translate3d(0, 10px, 0) " +
            "scale(0.05) " +
            "rotateZ(-12deg)";
    });

    const animation = animate(
        pixels,
        {
            opacity: [0, 1],
            y: [10, -2, 0],
            scale: [0.05, 1.18, 0.94, 1],
            rotateZ: [-12, 4, 0]
        },
        {
            delay: stagger(0.035, {
                from: "center",
                ease: "easeOut"
            }),

            duration: 0.72,

            ease: "easeOut"
        }
    );


    state.entrance = animation;


    animation.then(() => {

        if (!state.destroyed) {
            state.isAnimating = false;
        }
    });
}

function playHover(state) {

    const {
        pixels
    } = state;
    

    animate(
        state.grid,
        {
            scale: [1, 1.025, 1]
        },
        {
            duration: 0.45,
            ease: "easeOut"
        }
    );


    animate(
        pixels,
        {
            scale: [1, 1.22, 0.96, 1],
            y: [0, -3, 0]
        },
        {
            delay: stagger(0.022, {
                from: "center",
                ease: "easeInOut"
            }),

            duration: 0.42,

            ease: "easeOut"
        }
    );
}



async function playRebuild(state) {

    if (state.isAnimating) {
        return;
    }


    state.isAnimating = true;


    const {
        pixels
    } = state;


    stopAnimations(state);




    await animate(
        state.grid,
        {
            scale: [1, 0.96, 1.03]
        },
        {
            duration: 0.22,
            ease: "easeInOut"
        }
    );



    const explosionAnimations = [];


    pixels.forEach(pixel => {

        const row =
            Number(pixel.dataset.row);

        const col =
            Number(pixel.dataset.col);

        const centerX =
            (COLUMNS - 1) / 2;

        const centerY =
            (ROWS - 1) / 2;

        let dx = col - centerX;
        let dy = row - centerY;


        const distance =
            Math.sqrt(
                dx * dx +
                dy * dy
            );

        if (distance > 0) {

            dx /= distance;
            dy /= distance;
        }

        const variation =
            ((row * 17 + col * 31) % 20) - 10;


        const x =
            dx * (38 + variation);

        const y =
            dy * (30 + variation);


        const rotate =
            ((row * 43 + col * 19) % 40) - 20;


        explosionAnimations.push(
            animate(
                pixel,
                {
                    opacity: [1, 1, 0],
                    x: [0, x],
                    y: [0, y],
                    scale: [1, 1.15, 0.25],
                    rotateZ: [0, rotate]
                },
                {
                    delay:
                        ((row + col) % 5) * 0.018,

                    duration: 0.48,

                    ease: "easeIn"
                }
            )
        );
    });


    await Promise.all(
        explosionAnimations.map(
            animation => animation
        )
    );

    

    const rebuildAnimations = [];


    pixels.forEach(pixel => {

        rebuildAnimations.push(
            animate(
                pixel,
                {
                    opacity: [0, 1],
                    x: 0,
                    y: 0,
                    scale: [0.25, 1.18, 0.96, 1],
                    rotateZ: 0
                },
                {
                    delay: stagger(
                        0.028,
                        {
                            from: "center"
                        }
                    ),

                    type: "spring",

                    stiffness: 420,
                    damping: 18,
                    mass: 0.7
                }
            )
        );
    });


    await Promise.all(
        rebuildAnimations.map(
            animation => animation
        )
    );



    await animate(
        state.grid,
        {
            scale: [1.03, 0.995, 1]
        },
        {
            type: "spring",

            stiffness: 300,
            damping: 16,

            duration: 0.5
        }
    );


    state.isAnimating = false;
}




function stopAnimations(state) {

    if (state.entrance) {

        state.entrance.stop();

        state.entrance = null;
    }


    for (const animation
        of state.activeAnimations) {

        animation?.stop();
    }


    state.activeAnimations = [];
}



export function dispose(root) {

    const state =
        instances.get(root);

    if (!state) {
        return;
    }


    state.destroyed = true;


    stopAnimations(state);


    root.removeEventListener(
        "pointerenter",
        state.onPointerEnter
    );

    root.removeEventListener(
        "click",
        state.onClick
    );


    instances.delete(root);
}
