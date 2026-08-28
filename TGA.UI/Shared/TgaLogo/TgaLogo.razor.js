import { animate, stagger } from "https://cdn.jsdelivr.net/npm/motion@13.1.1/+esm";

const LOGO = [
    "###..###..##.",
    ".#..#....#..#",
    ".#..#..#.####",
    ".#...###.#..#"
];

const COLUMNS = 13;
const ROWS = 4;

const instances = new WeakMap();

export function initialize(root) {
    if (!root) {
        return;
    }

    if (instances.has(root)) {
        return;
    }

    const grid = root.querySelector(".tga-logo-grid");

    if (!grid) {
        return;
    }

    const pixels = [];

    for (let row = 0; row < ROWS; row++) {
        for (let col = 0; col < COLUMNS; col++) {

            const cell = document.createElement("span");

            cell.className =
                LOGO[row][col] === "#"
                    ? "tga-pixel"
                    : "tga-cell";

            cell.dataset.row = row;
            cell.dataset.col = col;

            grid.appendChild(cell);

            if (LOGO[row][col] === "#") {
                pixels.push(cell);
            }
        }
    }

    pixels.forEach(pixel => {
        pixel.style.opacity = "0";
        pixel.style.transform = "scale(0) translateY(6px)";
    });

    const entrance = animate(
        pixels,
        {
            opacity: [0, 1],
            scale: [0, 1.35, 0.9, 1],
            y: [6, -1, 0]
        },
        {
            delay: stagger(0.035, {
                from: "center"
            }),

            duration: 0.55,

            ease: [
                "easeOut",
                "easeInOut",
                "easeOut"
            ]
        }
    );
    
    const onPointerEnter = () => {

        animate(
            pixels,
            {
                scale: [1, 1.12, 1]
            },
            {
                delay: stagger(0.018, {
                    from: "center"
                }),

                duration: 0.28,

                ease: "easeOut"
            }
        );
    };


    const onClick = () => {

        animate(
            pixels,
            {
                opacity: [1, 0],
                scale: [1, 0.25],
                y: [0, 5]
            },
            {
                delay: stagger(0.018, {
                    from: "edges"
                }),

                duration: 0.25,

                ease: "easeIn"
            }
        ).then(() => {

            animate(
                pixels,
                {
                    opacity: [0, 1],
                    scale: [0, 1.35, 0.9, 1],
                    y: [5, -1, 0]
                },
                {
                    delay: stagger(0.035, {
                        from: "center"
                    }),

                    duration: 0.55,

                    ease: "easeOut"
                }
            );
        });
    };

    root.addEventListener(
        "pointerenter",
        onPointerEnter
    );

    root.addEventListener(
        "click",
        onClick
    );

    instances.set(root, {
        pixels,
        entrance,
        onPointerEnter,
        onClick
    });
}


export function dispose(root) {

    const instance = instances.get(root);

    if (!instance) {
        return;
    }

    instance.entrance?.stop();

    root.removeEventListener(
        "pointerenter",
        instance.onPointerEnter
    );

    root.removeEventListener(
        "click",
        instance.onClick
    );

    instances.delete(root);
}