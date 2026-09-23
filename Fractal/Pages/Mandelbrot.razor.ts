const controlsPositionKey = "fractal.mandelbrot.controlsPosition";

export function initControlsPanel(panel: HTMLElement, handle: HTMLElement) {
  if (!panel || !handle || panel.dataset.dragBound === "true") {
    return;
  }

  panel.dataset.dragBound = "true";
  restoreControlsPosition(panel);

  let pointerId: number | null = null;
  let offsetX = 0;
  let offsetY = 0;

  const onResize = () => {
    if (!panel.isConnected) {
      window.removeEventListener("resize", onResize);
      return;
    }

    if (!panel.style.left) {
      return;
    }

    placePanel(panel, parseFloat(panel.style.left), parseFloat(panel.style.top));
  };

  window.addEventListener("resize", onResize);

  handle.addEventListener("pointerdown", (event: PointerEvent) => {
    if (event.button !== 0) {
      return;
    }

    const rect = panel.getBoundingClientRect();
    pointerId = event.pointerId;
    offsetX = event.clientX - rect.left;
    offsetY = event.clientY - rect.top;
    placePanel(panel, rect.left, rect.top);
    handle.setPointerCapture(event.pointerId);
    event.preventDefault();
  });

  handle.addEventListener("pointermove", (event: PointerEvent) => {
    if (pointerId !== event.pointerId) {
      return;
    }

    placePanel(panel, event.clientX - offsetX, event.clientY - offsetY);
  });

  const endDrag = (event: PointerEvent) => {
    if (pointerId !== event.pointerId) {
      return;
    }

    pointerId = null;
    const rect = panel.getBoundingClientRect();
    try {
      localStorage.setItem(controlsPositionKey, JSON.stringify({ x: rect.left, y: rect.top }));
    } catch {
      // The panel can still be moved for this visit when storage is unavailable.
    }
  };

  handle.addEventListener("pointerup", endDrag);
  handle.addEventListener("pointercancel", endDrag);
}

function restoreControlsPosition(panel: HTMLElement) {
  let raw: string | null = null;
  try {
    raw = localStorage.getItem(controlsPositionKey);
  } catch {
    return;
  }

  if (!raw) {
    return;
  }

  try {
    const parsed = JSON.parse(raw) as { x?: number, y?: number };
    if (typeof parsed.x !== "number" || typeof parsed.y !== "number") {
      return;
    }

    placePanel(panel, parsed.x, parsed.y);
  } catch {
    try {
      localStorage.removeItem(controlsPositionKey);
    } catch {
      // Ignore storage failures.
    }
  }
}

function placePanel(panel: HTMLElement, x: number, y: number) {
  const nav = document.querySelector(".top-row");
  const minY = nav ? nav.getBoundingClientRect().bottom + 8 : 56;
  const maxX = Math.max(0, window.innerWidth - panel.offsetWidth);
  const maxY = Math.max(minY, window.innerHeight - panel.offsetHeight);
  panel.style.left = `${clamp(x, 0, maxX)}px`;
  panel.style.top = `${clamp(y, minY, maxY)}px`;
  panel.style.right = "auto";
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

export function getSize(): { width?: number, height?: number } {
  var e = document.getElementById("mandelbrot-tiles");
  if (!e) {
    return { width: undefined, height: undefined };
  }

  // console.log("getSize", e.clientWidth, e.clientHeight);

  return { width: e.clientWidth, height: e.clientHeight };
};
